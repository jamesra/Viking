"""
Segmentation Service Implementation

This module implements the core logic for segmenting images using SAM2.
It adapts the SAM2 model to work with the gRPC service interface.
"""

import os
import numpy as np
import torch
from PIL import Image
import io
import cv2
import asyncio
import tempfile
import shutil
from typing import List, Tuple, Dict, Any, Optional
from numpy.typing import NDArray

# Import SAM2 modules
import sam2
from sam2.build_sam import build_sam2
from sam2.sam2_image_predictor import SAM2ImagePredictor
from sam2.automatic_mask_generator import SAM2AutomaticMaskGenerator


class SegmentationModel:
    """
    A wrapper around the SAM2 model for image segmentation.
    
    This class handles the initialization of the SAM2 model and provides
    methods for segmenting images based on input coordinates.
    """
    
    def __init__(self):
        """Initialize the SAM2 model."""
        # Cache the temp directory path and clear it at startup
        self.temp_dir = tempfile.gettempdir()
        self._clear_temp_folder()
        
        # Select the device for computation
        if torch.cuda.is_available():
            self.device = torch.device("cuda")
            # Use bfloat16 for better performance on CUDA
            torch.autocast("cuda", dtype=torch.bfloat16).__enter__()
            # Turn on tfloat32 for Ampere GPUs
            if torch.cuda.get_device_properties(0).major >= 8:
                torch.backends.cuda.matmul.allow_tf32 = True
                torch.backends.cudnn.allow_tf32 = True
        elif torch.backends.mps.is_available():
            self.device = torch.device("mps")
            print(
                "Support for MPS devices is preliminary. SAM2 is trained with CUDA and might "
                "give numerically different outputs and sometimes degraded performance on MPS."
            )
        else:
            self.device = torch.device("cpu")
            
        print(f"Using device: {self.device}")
        
        # Set up paths for the SAM2 model
        root_path = os.path.dirname(sam2.__file__)
        root_path = os.path.dirname(root_path)
        
        sam2_checkpoint = f"/{root_path}/checkpoints/sam2.1_hiera_large.pt"
        model_cfg = f"/{root_path}/sam2/configs/sam2.1/sam2.1_hiera_l.yaml"
        
        # Build the SAM2 model
        self.sam2_model = build_sam2(model_cfg, sam2_checkpoint, device=self.device)
        
        # Create the image predictor
        self.predictor = SAM2ImagePredictor(self.sam2_model)

        self.mask_generator = SAM2AutomaticMaskGenerator(
            model=self.sam2_model,
            points_per_side=32,
            points_per_batch=256,
            pred_iou_thresh=0.7,
            stability_score_thresh=0.92,
            stability_score_offset=0.7,
            crop_n_layers=1,
            box_nms_thresh=0.7,
            crop_n_points_downscale_factor=2,
            min_mask_region_area=25.0,
            use_m2m=True,
        )

    def _clear_temp_folder(self):
        """Clear the temp folder at startup to remove any leftover files."""
        try:
            # Create a temp subdirectory for our service to avoid conflicts
            self.service_temp_dir = os.path.join(self.temp_dir, "segmentation_service")
            
            # Remove the directory if it exists and recreate it
            if os.path.exists(self.service_temp_dir):
                shutil.rmtree(self.service_temp_dir)
            
            # Create the directory
            os.makedirs(self.service_temp_dir, exist_ok=True)
            print(f"Cleared and created temp directory: {self.service_temp_dir}")
            
        except Exception as e:
            print(f"Warning: Failed to clear temp folder: {e}")
            # Fallback to using the main temp directory
            self.service_temp_dir = self.temp_dir

    @staticmethod
    def mask_to_polygons(mask: NDArray[np.bool_]) -> List[np.ndarray]:
        """
        Convert a boolean mask to a list of polygons representing the contours.

        Args:
            mask: A boolean numpy array where True represents the masked region

        Returns:
            A list of polygons, where each polygon is a numpy array of shape (N, 2)
            containing the (x, y) coordinates of the contour vertices
        """
        # Make sure mask is boolean and convert to uint8 for OpenCV
        mask_uint8 = mask.astype(np.uint8) * 255

        # Find contours in the mask
        contours, _ = cv2.findContours(mask_uint8, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        # Convert contours to simplified polygons
        polygons = []
        for contour in contours:
            # Simplify the contour to reduce the number of points
            epsilon = 0.005 * cv2.arcLength(contour, True)
            approx = cv2.approxPolyDP(contour, epsilon, True)

            # Reshape from (N, 1, 2) to (N, 2)
            polygon = approx.reshape(-1, 2)

            # Only include polygons with a minimum number of points
            if len(polygon) >= 3:
                polygons.append(polygon)

        return polygons

    @staticmethod
    def get_mask_bounds(mask: NDArray[np.bool_]) -> Tuple[int, int, int, int]:
        """
        Calculate the bounding box of a boolean mask.

        Args:
            mask: A boolean numpy array where True represents the masked region

        Returns:
            A tuple containing (x, y, width, height) where (x, y) is the top-left corner
            and width/height are the dimensions of the bounding box
        """
        # Find the coordinates where the mask is True
        rows, cols = np.where(mask)
        
        if len(rows) == 0:
            # Empty mask
            return 0, 0, 0, 0
        
        # Calculate bounding box
        min_row, max_row = np.min(rows), np.max(rows)
        min_col, max_col = np.min(cols), np.max(cols)
        
        x = int(min_col)
        y = int(min_row)
        width = int(max_col - min_col + 1)
        height = int(max_row - min_row + 1)
        
        return x, y, width, height

    @staticmethod
    def create_labeled_image(anns, borders=True) -> NDArray[np.uint16]:
        """Given a set of masks, creates a labeled image."""

        if len(anns) == 0:
            return

        #Start with the largest mask, and work towards the smallest
        sorted_anns = sorted(anns, key=(lambda x: x['area']), reverse=True)
        #ax = plt.gca()
        #ax.set_autoscale_on(False)

        img = np.zeros((sorted_anns[0]['segmentation'].shape[0], sorted_anns[0]['segmentation'].shape[1]), dtype=np.uint16)
        for i, ann in enumerate(sorted_anns):
            m = ann['segmentation']
            img[m] = i

        return img
    
    def segment_image(self,
                           image_data: bytes, 
                           width: int, 
                           height: int, 
                           coordinates: List[Tuple[int, int]], 
                           labels: List[int], 
                           multimask_output: bool = True) -> Tuple[np.ndarray, List[Dict[str, Any]]]:
        """
        Segment an image based on input coordinates.
        
        Args:
            image_data: The grayscale image data as bytes
            width: The width of the image
            height: The height of the image
            coordinates: List of (x, y) coordinates to use as prompts
            labels: List of labels for each coordinate (1 for foreground, 0 for background)
            multimask_output: Whether to output multiple masks per point
            
        Returns:
            A tuple containing:
            - labeled_image: A 2D numpy array where each pixel value corresponds to a segment index
            - segments: A list of dictionaries containing information about each segment
        """
        # Convert image bytes to numpy array
        image = Image.open(io.BytesIO(image_data))
        
        # Debug: Print original image mode and shape
        print(f"Debug: Original image mode: {image.mode}")
        
        # Convert to RGB format (SAM2 expects RGB)
        if image.mode != 'RGB':
            image = image.convert('RGB')
        
        # Convert to numpy array
        image_np = np.array(image)
        
        # Debug: Print numpy array shape
        print(f"Debug: Image numpy shape: {image_np.shape}")
        
        # Ensure image is RGB format for SAM2 (3 channels)
        if len(image_np.shape) == 3 and image_np.shape[2] == 4:  # RGBA image
            print("Debug: Converting RGBA to RGB")
            image_np = image_np[:, :, :3]  # Remove alpha channel, keep RGB
        elif len(image_np.shape) == 3 and image_np.shape[2] == 1:  # Grayscale
            print("Debug: Converting grayscale to RGB")
            image_np = np.repeat(image_np, 3, axis=2)  # Convert to RGB
        elif len(image_np.shape) == 2:  # 2D grayscale
            print("Debug: Converting 2D grayscale to RGB")
            image_np = np.repeat(image_np[:, :, np.newaxis], 3, axis=2)  # Convert to RGB
        
        # Final validation
        print(f"Debug: Final image shape: {image_np.shape}")
        if len(image_np.shape) != 3 or image_np.shape[2] != 3:
            raise ValueError(f"Expected RGB image with 3 channels, got shape: {image_np.shape}")
        
        
        # Debug: Save the image before processing
        
        try:
            debug_path = os.path.join(self.service_temp_dir, "Segmentation_Service_Image.png")
            # Ensure the directory exists
            os.makedirs(os.path.dirname(debug_path), exist_ok=True)
            # Save the image
            debug_image = Image.fromarray(image_np)
            debug_image.save(debug_path)
            print(f"Debug: Image saved to {debug_path}")
        except Exception as e:
            print(f"Debug: Failed to save image to {debug_path}: {e}")

        # Convert coordinates to numpy array
        point_coords = np.array(coordinates)
        point_labels = np.array(labels)

        # Set the image for the
        with torch.inference_mode(), torch.autocast("cuda", dtype=torch.bfloat16):
            self.predictor.set_image(image_np)

            masks, scores, logits = self.predictor.predict(
                    point_coords=point_coords,
                    point_labels=point_labels,
                    multimask_output=multimask_output,
                )

            #full_masks = self.mask_generator.generate(image_np)

        #labeled_image = self.create_labeled_image(full_masks)
        labeled_image = np.zeros((height, width), dtype=np.uint16)

        # Sort masks by score
        sorted_ind = np.argsort(scores)[::-1]
        masks = masks[sorted_ind].astype(bool)
        scores = scores[sorted_ind]
        logits = logits[sorted_ind]
        
        # Create a labeled image where each pixel value corresponds to a segment index
        #labeled_image = np.zeros((height, width), dtype=np.uint16)
        
        # List to store segment information
        segments = []
         
        # Process each mask and create segment information
        for i, mask in enumerate(masks):
            # Calculate mask bounds
            x, y, width, height = self.get_mask_bounds(mask)
            
            # Encode mask to bytes
            mask_bytes = cv2.imencode('.png', mask.astype(np.uint8) * 255)[1].tobytes()
            
            # Create segment dictionary
            segment = {
                'index': i,
                'score': float(scores[i]),
                'mask': mask_bytes,
                'x': x,
                'y': y,
                'width': width,
                'height': height
            }
            segments.append(segment)
            
            # Debug: Save each mask to temp folder
            mask_path = os.path.join(self.service_temp_dir, f'mask_{i}.png')
            cv2.imwrite(mask_path, mask.astype(np.uint8) * 255)
        
        return labeled_image, segments