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
import tempfile
import shutil
import threading
from typing import Any, Dict, List, Optional, Sequence, Tuple, TypedDict
from numpy.typing import NDArray

# Import SAM2 modules
import sam2
from sam2.build_sam import build_sam2
from sam2.sam2_image_predictor import SAM2ImagePredictor
from sam2.automatic_mask_generator import SAM2AutomaticMaskGenerator


MaskArray = NDArray[np.bool_]
LabeledImage = NDArray[np.uint16]
PolygonArray = NDArray[np.int32]
Point = Tuple[int, int]


class SegmentInfo(TypedDict):
    index: int
    score: float
    mask: MaskArray
    x: int
    y: int
    width: int
    height: int


class SegmentationModel:
    """
    A wrapper around the SAM2 model for image segmentation.
    
    This class handles the initialization of the SAM2 model and provides
    methods for segmenting images based on input coordinates.
    
    Architecture:
        - Maintains a shared SAM2 model instance (expensive to create, reused for all predictors)
        - Each predictor instance (created via create_predictor()) shares the model but has
          independent state for set_image() calls
        - The shared predictor (self.predictor) is used only for backward compatibility with
          inline image segmentation requests
    
    Performance:
        - Model initialization: One-time cost at startup (~few seconds)
        - Predictor creation: Fast (just wraps the shared model)
        - set_image() call: O(n) where n is image size - this is why cached predictors are faster
        - predict() call: O(n) where n is image size, but much faster than set_image() + predict()
    
    Thread Safety:
        - The shared model is thread-safe for read-only operations (model weights)
        - Predictor instances are NOT thread-safe - each should be used by one thread at a time
        - This is why each cached image gets its own predictor with a lock
    """
    
    def __init__(self) -> None:
        """Initialize the SAM2 model."""
        # Cache the temp directory path and clear it at startup
        self.temp_dir: str = tempfile.gettempdir()
        self.service_temp_dir: str
        self._clear_temp_folder()
        
        # Select the device for computation
        if torch.cuda.is_available():
            self.device: torch.device = torch.device("cuda")
            # Turn on tfloat32 for Ampere GPUs (improves performance on A100, RTX 30xx, etc.)
            if torch.cuda.get_device_properties(0).major >= 8:
                torch.backends.cuda.matmul.allow_tf32 = True
                torch.backends.cudnn.allow_tf32 = True
            # Note: autocast is used per-operation in segment_image_with_predictor() 
            # and segment_image() methods, not as a global context manager
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
        self.sam2_model: Any = build_sam2(model_cfg, sam2_checkpoint, device=self.device)
        
        # Create the image predictor
        self.predictor: SAM2ImagePredictor = SAM2ImagePredictor(self.sam2_model)

        self.mask_generator: SAM2AutomaticMaskGenerator = SAM2AutomaticMaskGenerator(
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
    
    def create_predictor(self) -> SAM2ImagePredictor:
        """
        Create a new SAM2ImagePredictor instance.
        
        Each predictor shares the underlying SAM2 model (which contains the weights),
        but has independent state for image embedding and prediction operations.
        This allows multiple predictors to work on different images concurrently
        without interference.
        
        Performance:
            - Fast operation - just creates a wrapper around the shared model
            - Model weights are shared, so memory overhead is minimal
            - Independent state per predictor enables true concurrency
        
        Returns:
            A new SAM2ImagePredictor instance using the shared model.
            The predictor must be initialized with set_image() before use.
            
        Thread Safety:
            Each predictor instance should be used by one thread at a time.
            Use a threading.Lock if concurrent access is possible.
        """
        return SAM2ImagePredictor(self.sam2_model)
    
    @staticmethod
    def prepare_image_for_sam2(image_data: bytes) -> NDArray[np.uint8]:
        """
        Convert image bytes to numpy array in RGB format for SAM2.
        
        Args:
            image_data: The image data as bytes
            
        Returns:
            A numpy array in RGB format (H, W, 3)
        """
        # Convert image bytes to numpy array
        try:
            image = Image.open(io.BytesIO(image_data))
        except (OSError, IOError) as e:
            error_msg = str(e).lower()
            if any(keyword in error_msg for keyword in ['truncated', 'corrupted', 'invalid', 'cannot identify image file']):
                raise OSError(f"Image data is corrupted or truncated: {e}")
            else:
                raise
        
        # Convert to RGB format (SAM2 expects RGB)
        if image.mode != 'RGB':
            image = image.convert('RGB')
        
        # Convert to numpy array
        image_np: NDArray[np.uint8] = np.array(image)
        
        # Ensure image is RGB format for SAM2 (3 channels)
        if len(image_np.shape) == 3 and image_np.shape[2] == 4:  # RGBA image
            image_np = image_np[:, :, :3]  # Remove alpha channel, keep RGB
        elif len(image_np.shape) == 3 and image_np.shape[2] == 1:  # Grayscale
            image_np = np.repeat(image_np, 3, axis=2)  # Convert to RGB
        elif len(image_np.shape) == 2:  # 2D grayscale
            image_np = np.repeat(image_np[:, :, np.newaxis], 3, axis=2)  # Convert to RGB
        
        # Final validation
        if len(image_np.shape) != 3 or image_np.shape[2] != 3:
            raise ValueError(f"Expected RGB image with 3 channels, got shape: {image_np.shape}")
        
        return image_np

    def _clear_temp_folder(self) -> None:
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
    def mask_to_polygons(mask: MaskArray) -> List[PolygonArray]:
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
        polygons: List[PolygonArray] = []
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
    def get_mask_bounds(mask: MaskArray) -> Tuple[int, int, int, int]:
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
    def cleanup_mask(mask: MaskArray, hole_threshold: float = 0.03) -> MaskArray:
        """
        Clean up a mask by keeping only the largest connected region and filling small holes.
        
        Args:
            mask: A boolean numpy array where True represents the masked region
            hole_threshold: Fraction of total area below which holes are filled (default 0.03 = 3%)
        
        Returns:
            Cleaned boolean mask with only the largest connected region and small holes filled
        """
        # Convert boolean to uint8 for OpenCV operations
        mask_uint8 = mask.astype(np.uint8) * 255
        
        # Find all connected components
        num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(mask_uint8, connectivity=8)
        
        if num_labels <= 1:
            # No connected components or only background
            return mask
        
        # Find the largest component (excluding background at index 0)
        largest_component_idx = 1
        largest_area = stats[1, cv2.CC_STAT_AREA]
        
        for i in range(2, num_labels):
            if stats[i, cv2.CC_STAT_AREA] > largest_area:
                largest_area = stats[i, cv2.CC_STAT_AREA]
                largest_component_idx = i
        
        # Create mask with only the largest connected component
        cleaned_mask = (labels == largest_component_idx).astype(np.bool_)
        
        # Convert back to uint8 for hole filling
        cleaned_mask_uint8 = cleaned_mask.astype(np.uint8) * 255
        
        # Find internal holes using RETR_CCOMP to get holes
        mask_copy = cleaned_mask_uint8.copy()
        holes_contours, holes_hierarchy = cv2.findContours(
            ~cleaned_mask_uint8,  # Invert to find holes
            cv2.RETR_CCOMP,
            cv2.CHAIN_APPROX_SIMPLE
        )
        
        if holes_hierarchy is not None:
            total_mask_area = np.sum(cleaned_mask)
            
            for i, contour in enumerate(holes_contours):
                # Check if this is a hole (has a parent contour)
                if holes_hierarchy[0][i][3] >= 0:
                    hole_area = cv2.contourArea(contour)
                    
                    # Fill hole if it's smaller than threshold
                    if hole_area < (total_mask_area * hole_threshold):
                        cv2.fillPoly(mask_copy, [contour], 255)
            
            # Convert back to boolean
            cleaned_mask = (mask_copy > 0).astype(np.bool_)
        
        return cleaned_mask
    
    @staticmethod
    def _process_masks(
        masks: NDArray[np.bool_],
        scores: NDArray[np.float32],
        empty_shape: Tuple[int, int] = (0, 0)
    ) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """
        Process masks and scores to create labeled image and segment information.
        
        This helper method extracts the common mask processing logic used in both
        segment_image_with_predictor() and segment_image() methods.
        
        Args:
            masks: Boolean mask array of shape (N, H, W) where N is number of masks
            scores: Score array of shape (N,) with scores for each mask
            empty_shape: Shape to use for empty labeled image (default: (0, 0))
            
        Returns:
            Tuple of (labeled_image, segments) where:
            - labeled_image: A 2D numpy array where each pixel value is a segment index
            - segments: A list of dictionaries containing information about each segment
        """
        if masks.size == 0:
            empty_image: LabeledImage = np.zeros(empty_shape, dtype=np.uint16)
            return empty_image, []
        
        # Handle case where masks might not have proper shape
        if len(masks.shape) < 3:
            empty_image: LabeledImage = np.zeros(empty_shape, dtype=np.uint16)
            return empty_image, []
        
        mask_height, mask_width = masks.shape[1], masks.shape[2]
        labeled_image: LabeledImage = np.zeros((mask_height, mask_width), dtype=np.uint16)
        
        # List to store segment information
        segments: List[SegmentInfo] = []
         
        # Process each mask and create segment information
        for i, mask in enumerate(masks):
            if not np.any(mask):
                continue

            # Assign label ids starting at 1 to reserve 0 for background
            label_id = i + 1
            unlabeled_pixels = np.logical_and(mask, labeled_image == 0)
            labeled_image[unlabeled_pixels] = label_id
            
            # Calculate mask bounds
            x, y, width, height = SegmentationModel.get_mask_bounds(mask)
            
            # Create segment dictionary
            segment: SegmentInfo = {
                'index': i,
                'score': float(scores[i]),
                'mask': mask,  # Store as boolean numpy array
                'x': x,
                'y': y,
                'width': width,
                'height': height
            }
            segments.append(segment)
        
        return labeled_image, segments

    @staticmethod
    def create_labeled_image(anns: Sequence[Dict[str, Any]]) -> LabeledImage:
        """Given a set of masks, creates a labeled image."""

        if len(anns) == 0:
            return np.zeros((0, 0), dtype=np.uint16)

        #Start with the largest mask, and work towards the smallest
        sorted_anns = sorted(anns, key=(lambda x: x['area']), reverse=True)
        #ax = plt.gca()
        #ax.set_autoscale_on(False)

        img = np.zeros((sorted_anns[0]['segmentation'].shape[0], sorted_anns[0]['segmentation'].shape[1]), dtype=np.uint16)
        for i, ann in enumerate(sorted_anns):
            m: MaskArray = ann['segmentation']
            img[m] = i

        return img
    
    def segment_image_with_predictor(self,
                                     predictor: SAM2ImagePredictor,
                                     coordinates: Sequence[Point],
                                     labels: Sequence[int],
                                     multimask_output: bool = True) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """
        Segment an image using a pre-initialized predictor.
        
        This is the high-performance path for cached images. The predictor must already
        have been initialized with set_image() before calling this method. This eliminates
        the set_image() overhead from the segmentation request.
        
        Args:
            predictor: A SAM2ImagePredictor that has already been initialized with set_image().
                The predictor should be locked if concurrent access is possible.
            coordinates: List of (x, y) coordinates to use as prompts
            labels: List of labels for each coordinate (1 for foreground, 0 for background).
                Must match length of coordinates.
            multimask_output: Whether to output multiple masks per point
            
        Returns:
            A tuple containing:
            - labeled_image: A 2D numpy array where each pixel value corresponds to a segment index
            - segments: A list of dictionaries containing information about each segment
            
        Performance:
            - O(n) where n is image size for predict() call
            - No set_image() overhead - this is the key performance benefit
            - Mask processing is O(m * k) where m is number of masks, k is mask size
            
        Thread Safety:
            The predictor parameter must be thread-safe or protected by a lock.
            This method does not acquire any locks itself.
        """
        # Convert coordinates to numpy array
        point_coords: NDArray[np.int_] = np.array(coordinates)
        point_labels: NDArray[np.int_] = np.array(labels)

        # Use the pre-initialized predictor
        with torch.inference_mode(), torch.autocast("cuda", dtype=torch.bfloat16):
            masks, scores, _logits = predictor.predict(
                    point_coords=point_coords,
                    point_labels=point_labels,
                    multimask_output=multimask_output,
                )

        # Sort masks by score
        sorted_ind = np.argsort(scores)[::-1]
        masks = masks[sorted_ind].astype(np.bool_)
        scores = scores[sorted_ind]
        
        # Process masks using helper method
        return self._process_masks(masks, scores, empty_shape=(0, 0))
    
    def segment_image(self,
                           image_data: bytes, 
                           width: int, 
                           height: int, 
                           coordinates: Sequence[Point], 
                           labels: Sequence[int], 
                           multimask_output: bool = True) -> Tuple[LabeledImage, List[SegmentInfo]]:
        """
        Segment an image based on input coordinates.
        
        This method is kept for backward compatibility with inline image requests.
        It uses the shared predictor, which requires a set_image() call for each request.
        For better performance, use cached images with pre-initialized predictors.
        
        Args:
            image_data: The image data as bytes
            width: The width of the image
            height: The height of the image
            coordinates: List of (x, y) coordinates to use as prompts
            labels: List of labels for each coordinate (1 for foreground, 0 for background)
            multimask_output: Whether to output multiple masks per point
            
        Returns:
            A tuple containing:
            - labeled_image: A 2D numpy array where each pixel value corresponds to a segment index
            - segments: A list of dictionaries containing information about each segment
            
        Performance:
            - O(n) for prepare_image_for_sam2() where n is image size
            - O(n) for set_image() - this is the performance bottleneck
            - O(n) for predict() call
            - Total: ~2x slower than segment_image_with_predictor() due to set_image() overhead
            
        Thread Safety:
            The shared predictor (self.predictor) is NOT thread-safe. Concurrent calls
            to this method may interfere with each other. This is acceptable for backward
            compatibility use case, but cached images with per-predictor locks are preferred.
        """
        # Prepare image for SAM2
        image_np = self.prepare_image_for_sam2(image_data)

        # Convert coordinates to numpy array
        point_coords: NDArray[np.int_] = np.array(coordinates)
        point_labels: NDArray[np.int_] = np.array(labels)

        # Set the image for the shared predictor (not thread-safe for concurrent use)
        with torch.inference_mode(), torch.autocast("cuda", dtype=torch.bfloat16):
            self.predictor.set_image(image_np)

            masks, scores, _logits = self.predictor.predict(
                    point_coords=point_coords,
                    point_labels=point_labels,
                    multimask_output=multimask_output,
                )

        # Sort masks by score
        sorted_ind = np.argsort(scores)[::-1]
        masks = masks[sorted_ind].astype(np.bool_)
        scores = scores[sorted_ind]
        
        # Process masks using helper method (use image dimensions for empty case)
        return self._process_masks(masks, scores, empty_shape=(height, width))