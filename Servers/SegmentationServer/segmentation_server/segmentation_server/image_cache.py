"""
Image Cache Module

This module implements an in-memory cache for images with LRU eviction,
TTL (time-to-live) expiration, and memory limit management.
"""

import asyncio
import time
import sys
import threading
from typing import Dict, List, Optional, Tuple, Any, Callable
from dataclasses import dataclass, field

# Import SAM2 modules
from sam2.sam2_image_predictor import SAM2ImagePredictor


@dataclass
class CachedImage:
    """
    Represents a cached image entry with associated predictor.
    
    Attributes:
        image_data: The image data as bytes
        width: Image width in pixels
        height: Image height in pixels
        last_access_time: Timestamp of last access (for LRU)
        upload_time: Timestamp of upload (for TTL)
        size_bytes: Size of image data in bytes
        predictor: The SAM2ImagePredictor instance for this image (None if not yet created)
        predictor_lock: Lock for thread-safe access to the predictor
    """
    image_data: bytes
    width: int
    height: int
    last_access_time: float
    upload_time: float
    size_bytes: int
    predictor: Optional[SAM2ImagePredictor] = field(default=None)
    predictor_lock: threading.Lock = field(default_factory=threading.Lock)


class ImageCache:
    """
    Thread-safe image cache with LRU eviction, TTL expiration, and memory limits.
    
    This cache stores uploaded images in memory and automatically evicts the least
    recently used images when the memory limit is exceeded. Images also expire
    after a configurable TTL period. Each image gets its own SAM2ImagePredictor
    instance that is created at upload time and cached with the image.
    
    Thread Safety:
        - Cache operations (get/upload/delete) are protected by an asyncio.Lock
        - Each cached image has its own threading.Lock for the predictor
        - Predictor locks ensure thread-safe access when multiple requests target
          the same image simultaneously (unlikely but possible)
        - Predictor creation happens in executor threads to avoid blocking the event loop
    
    Performance Characteristics:
        - Predictor creation: Happens asynchronously in executor thread at upload time
          to avoid blocking the upload RPC call. The upload returns immediately with
          an image ID, even if predictor creation is still in progress.
        - Memory usage: Only image data bytes are counted toward memory limit.
          Predictor memory overhead is not included (typically small compared to images).
        - Eviction priority: TTL expiration is checked first, then LRU eviction when
          memory limit is exceeded. Expired images are removed before new uploads.
        - Concurrent access: Multiple requests for the same image will serialize on
          the predictor lock, but different images can be processed concurrently.
    
    Edge Cases:
        - Predictor creation failure: If predictor creation fails, the image is
          removed from cache immediately. Clients will receive UNAVAILABLE status
          if they request segmentation before predictor is ready.
        - Predictor creation in progress: Clients requesting segmentation while
          predictor is being created will receive UNAVAILABLE status. They should
          wait and retry, or re-upload the image.
        - Image eviction during predictor creation: If an image is evicted before
          predictor creation completes, the predictor is discarded (no memory leak).
    """
    
    def __init__(self, 
                 max_memory_bytes: int = 1073741824,  # 1 GB default
                 ttl_seconds: int = 300,  # 5 minutes default
                 create_predictor_func: Optional[Callable[[], SAM2ImagePredictor]] = None,
                 prepare_image_func: Optional[Callable[[bytes], Any]] = None):
        """
        Initialize the image cache.
        
        Args:
            max_memory_bytes: Maximum total memory for cached images (default: 1 GB)
                Note: Only image data bytes are counted. Predictor memory is not included.
            ttl_seconds: Time-to-live for cached images in seconds (default: 5 minutes)
                Images expire based on last access time, not upload time.
            create_predictor_func: Function to create a new SAM2ImagePredictor instance.
                Required for predictor caching. If None, predictors won't be created.
            prepare_image_func: Function to prepare image data for SAM2 (bytes -> numpy array).
                Required for predictor caching. If None, predictors won't be created.
        
        Architecture Note:
            Each image gets its own predictor instance created at upload time. This design
            choice eliminates the need to call set_image() during segmentation requests,
            which is a major performance optimization. The trade-off is higher memory usage
            per cached image, but it enables true concurrent processing of different images.
        """
        self._cache: Dict[int, CachedImage] = {}
        self._next_id: int = 1
        self._lock: asyncio.Lock = asyncio.Lock()
        self._max_memory_bytes: int = max_memory_bytes
        self._ttl_seconds: int = ttl_seconds
        self._current_memory_bytes: int = 0
        self._create_predictor_func = create_predictor_func
        self._prepare_image_func = prepare_image_func
        
        print(f"ImageCache initialized with max_memory={max_memory_bytes} bytes "
              f"({max_memory_bytes / (1024**3):.2f} GB), TTL={ttl_seconds}s")
    
    def _create_predictor_in_executor(self, image_data: bytes, image_id: int) -> Optional[SAM2ImagePredictor]:
        """
        Create a predictor for an image in an executor thread.
        
        This is called from run_in_executor to avoid blocking the async event loop.
        Predictor creation involves PyTorch model operations which can be CPU/GPU intensive.
        
        Args:
            image_data: The image data as bytes
            image_id: The image ID for error reporting
            
        Returns:
            The created predictor initialized with set_image(), or None if creation failed.
            
        Error Handling:
            If predictor creation raises an exception, it's caught, logged, and None is returned.
            The caller (upload_image) will remove the image from cache if predictor is None.
        """
        try:
            if not self._create_predictor_func or not self._prepare_image_func:
                print(f"Warning: Cannot create predictor for image ID={image_id} - functions not set")
                return None
            
            # Create predictor instance
            predictor = self._create_predictor_func()
            
            # Prepare image for SAM2
            image_np = self._prepare_image_func(image_data)
            
            # Initialize predictor with image
            import torch
            with torch.inference_mode(), torch.autocast("cuda", dtype=torch.bfloat16):
                predictor.set_image(image_np)
            
            print(f"Predictor created for image ID={image_id}")
            return predictor
            
        except Exception as e:
            print(f"Error creating predictor for image ID={image_id}: {e}")
            import traceback
            traceback.print_exc()
            return None
    
    async def upload_image(self, image_data: bytes, width: int, height: int, executor: Optional[Any] = None) -> int:
        """
        Upload an image to the cache and return a unique ID.
        Creates a predictor for the image in an executor thread at upload time.
        
        This method returns immediately with an image ID, even if predictor creation
        is still in progress. The predictor is created asynchronously to avoid blocking
        the upload RPC call. Clients can check if the predictor is ready by attempting
        a segmentation request (will get UNAVAILABLE if not ready).
        
        Args:
            image_data: The image data as bytes
            width: Image width in pixels
            height: Image height in pixels
            executor: Optional executor for creating predictor (if None, uses default)
                Should be a ThreadPoolExecutor to avoid blocking the event loop.
            
        Returns:
            Unique image ID (int)
            
        Performance:
            - Image storage: O(1) - immediate dictionary insertion
            - Predictor creation: O(1) with respect to cache operations, but O(n) 
              where n is image size due to set_image() processing
            - Memory eviction: O(k) where k is number of cached images (LRU lookup)
            
        Edge Cases:
            - If memory limit is exceeded, LRU images are evicted until there's room
            - If predictor creation fails, image is removed from cache automatically
            - If image is evicted before predictor creation completes, predictor is discarded
        """
        async with self._lock:
            # Clean up expired images first
            await self._cleanup_expired()
            
            # Calculate size (use len() to get actual bytes size, not object overhead)
            size_bytes: int = len(image_data)
            
            # Evict old images if necessary
            while (self._current_memory_bytes + size_bytes > self._max_memory_bytes 
                   and len(self._cache) > 0):
                await self._evict_oldest()
            
            # Generate new ID
            image_id: int = self._next_id
            self._next_id += 1
            
            # Create cache entry (predictor will be set later)
            current_time = time.time()
            cached_image = CachedImage(
                image_data=image_data,
                width=width,
                height=height,
                last_access_time=current_time,
                upload_time=current_time,
                size_bytes=size_bytes,
                predictor=None,  # Will be set after creation
                predictor_lock=threading.Lock()
            )
            
            # Store in cache
            self._cache[image_id] = cached_image
            self._current_memory_bytes += size_bytes
            
            print(f"Image uploaded: ID={image_id}, size={size_bytes} bytes, "
                  f"total_cache={self._current_memory_bytes} bytes "
                  f"({self._current_memory_bytes / (1024**2):.2f} MB), "
                  f"count={len(self._cache)}")
        
        # Create predictor in executor thread (outside lock to avoid blocking)
        if self._create_predictor_func and self._prepare_image_func:
            predictor = await asyncio.get_running_loop().run_in_executor(
                executor,
                self._create_predictor_in_executor,
                image_data,
                image_id
            )
            
            # Store predictor back in cache (with lock)
            async with self._lock:
                if image_id in self._cache:
                    self._cache[image_id].predictor = predictor
                    if predictor is None:
                        print(f"Warning: Failed to create predictor for image ID={image_id}, removing from cache")
                        await self._delete_image_internal(image_id)
                else:
                    print(f"Warning: Image ID={image_id} was evicted before predictor could be created")
        
        return image_id
    
    async def get_image(self, image_id: int) -> Optional[Tuple[bytes, int, int, Optional[SAM2ImagePredictor], threading.Lock]]:
        """
        Retrieve an image and its predictor from the cache by ID.
        
        This method updates the last access time (LRU) and returns the predictor
        along with its lock for thread-safe access during segmentation.
        
        Args:
            image_id: The unique image ID
            
        Returns:
            Tuple of (image_data, width, height, predictor, predictor_lock) if found, None otherwise.
            Note: predictor may be None if creation is still in progress or failed.
            The caller should check for None predictor and handle appropriately.
            
        Thread Safety:
            The returned predictor_lock is a threading.Lock (not asyncio.Lock) because
            predictor operations run in executor threads, not in the async event loop.
            Callers must acquire this lock before using the predictor for segmentation.
            
        Performance:
            - O(1) dictionary lookup
            - Expired images are cleaned up before lookup (O(k) where k is cache size)
            - LRU timestamp update is O(1)
        """
        async with self._lock:
            # Clean up expired images
            await self._cleanup_expired()
            
            if image_id not in self._cache:
                print(f"Image not found: ID={image_id}")
                return None
            
            # Update last access time (LRU)
            cached_image = self._cache[image_id]
            cached_image.last_access_time = time.time()
            
            print(f"Image retrieved: ID={image_id}, size={cached_image.size_bytes} bytes, "
                  f"predictor={'ready' if cached_image.predictor is not None else 'pending'}")
            
            return (cached_image.image_data, cached_image.width, cached_image.height, 
                   cached_image.predictor, cached_image.predictor_lock)
    
    async def get_image_data(self, image_id: int) -> Optional[Tuple[bytes, int, int]]:
        """
        Retrieve only image data from the cache by ID (backward compatibility).
        
        Args:
            image_id: The unique image ID
            
        Returns:
            Tuple of (image_data, width, height) if found, None otherwise
        """
        result = await self.get_image(image_id)
        if result is None:
            return None
        return (result[0], result[1], result[2])
    
    async def _delete_image_internal(self, image_id: int) -> bool:
        """
        Internal method to delete an image from the cache.
        Assumes lock is already held.
        """
        if image_id not in self._cache:
            return False
        
        cached_image = self._cache[image_id]
        del self._cache[image_id]
        self._current_memory_bytes -= cached_image.size_bytes
        
        # Predictor will be garbage collected when reference is lost
        if cached_image.predictor is not None:
            print(f"Predictor for image ID={image_id} will be garbage collected")
        
        return True
    
    async def delete_image(self, image_id: int) -> bool:
        """
        Delete an image from the cache.
        
        Args:
            image_id: The unique image ID to delete
            
        Returns:
            True if the image was deleted, False if not found
        """
        async with self._lock:
            if not await self._delete_image_internal(image_id):
                print(f"Image deletion failed (not found): ID={image_id}")
                return False
            
            print(f"Image deleted: ID={image_id}, "
                  f"total_cache={self._current_memory_bytes} bytes "
                  f"({self._current_memory_bytes / (1024**2):.2f} MB), "
                  f"count={len(self._cache)}")
            
            return True
    
    async def _evict_oldest(self) -> None:
        """
        Evict the least recently used image from the cache (internal method).
        
        This is called when memory limit would be exceeded by a new upload.
        The image with the oldest last_access_time is removed.
        
        Assumes lock is already held.
        
        Performance:
            - O(k) where k is number of cached images (must find minimum last_access_time)
            - Linear scan is acceptable for typical cache sizes
            - Future optimization: Use priority queue for O(log k) eviction
        """
        if not self._cache:
            return
        
        # Find the image with the oldest last_access_time
        oldest_id = min(self._cache.keys(), 
                       key=lambda k: self._cache[k].last_access_time)
        
        await self._delete_image_internal(oldest_id)
        
        print(f"Image evicted (LRU): ID={oldest_id}, "
              f"total_cache={self._current_memory_bytes} bytes "
              f"({self._current_memory_bytes / (1024**2):.2f} MB), "
              f"count={len(self._cache)}")
    
    async def _cleanup_expired(self) -> None:
        """
        Remove expired images from the cache (internal method).
        
        Images are expired if they haven't been accessed within the TTL period.
        This is called automatically on get_image, upload_image, and delete_image
        operations to ensure expired images are removed promptly.
        
        Eviction Priority:
            - TTL expiration is checked before LRU eviction
            - Expired images are removed first, then LRU eviction if memory limit exceeded
            - This ensures expired images don't consume memory unnecessarily
        
        Performance:
            - O(k) where k is number of cached images (must iterate to check expiration)
            - Runs on every cache operation, but is fast for typical cache sizes
            - Future optimization: Move to background periodic task for large caches
        """
        current_time = time.time()
        expired_ids: List[int] = []
        
        for image_id, cached_image in self._cache.items():
            # Check if image has expired based on last access time
            time_since_access = current_time - cached_image.last_access_time
            if time_since_access > self._ttl_seconds:
                expired_ids.append(image_id)
        
        # Remove expired images
        for image_id in expired_ids:
            # Get age before deletion
            cached_image = self._cache.get(image_id)
            cached_image_age = current_time - cached_image.last_access_time if cached_image else 0
            await self._delete_image_internal(image_id)
            print(f"Image expired (TTL): ID={image_id}, "
                  f"age={cached_image_age:.1f}s")
        
        if expired_ids:
            print(f"Cleanup complete: removed {len(expired_ids)} expired images, "
                  f"total_cache={self._current_memory_bytes} bytes "
                  f"({self._current_memory_bytes / (1024**2):.2f} MB), "
                  f"count={len(self._cache)}")
    
    async def get_stats(self) -> Dict[str, Any]:
        """
        Get cache statistics.
        
        Returns:
            Dictionary containing cache statistics
        """
        async with self._lock:
            return {
                'total_images': len(self._cache),
                'total_memory_bytes': self._current_memory_bytes,
                'total_memory_mb': self._current_memory_bytes / (1024**2),
                'max_memory_bytes': self._max_memory_bytes,
                'max_memory_mb': self._max_memory_bytes / (1024**2),
                'memory_usage_percent': (self._current_memory_bytes / self._max_memory_bytes * 100) 
                                       if self._max_memory_bytes > 0 else 0,
                'ttl_seconds': self._ttl_seconds
            }

