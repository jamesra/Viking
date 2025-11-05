"""
Image Cache Module

This module implements an in-memory cache for images with LRU eviction,
TTL (time-to-live) expiration, and memory limit management.
"""

import asyncio
import time
import sys
from typing import Dict, Optional, Tuple
from dataclasses import dataclass


@dataclass
class CachedImage:
    """
    Represents a cached image entry.
    
    Attributes:
        image_data: The image data as bytes
        width: Image width in pixels
        height: Image height in pixels
        last_access_time: Timestamp of last access (for LRU)
        upload_time: Timestamp of upload (for TTL)
        size_bytes: Size of image data in bytes
    """
    image_data: bytes
    width: int
    height: int
    last_access_time: float
    upload_time: float
    size_bytes: int


class ImageCache:
    """
    Thread-safe image cache with LRU eviction, TTL expiration, and memory limits.
    
    This cache stores uploaded images in memory and automatically evicts the least
    recently used images when the memory limit is exceeded. Images also expire
    after a configurable TTL period.
    """
    
    def __init__(self, 
                 max_memory_bytes: int = 1073741824,  # 1 GB default
                 ttl_seconds: int = 300):  # 5 minutes default
        """
        Initialize the image cache.
        
        Args:
            max_memory_bytes: Maximum total memory for cached images (default: 1 GB)
            ttl_seconds: Time-to-live for cached images in seconds (default: 5 minutes)
        """
        self._cache: Dict[int, CachedImage] = {}
        self._next_id: int = 1
        self._lock = asyncio.Lock()
        self._max_memory_bytes = max_memory_bytes
        self._ttl_seconds = ttl_seconds
        self._current_memory_bytes = 0
        
        print(f"ImageCache initialized with max_memory={max_memory_bytes} bytes "
              f"({max_memory_bytes / (1024**3):.2f} GB), TTL={ttl_seconds}s")
    
    async def upload_image(self, image_data: bytes, width: int, height: int) -> int:
        """
        Upload an image to the cache and return a unique ID.
        
        Args:
            image_data: The image data as bytes
            width: Image width in pixels
            height: Image height in pixels
            
        Returns:
            Unique image ID (int)
        """
        async with self._lock:
            # Clean up expired images first
            await self._cleanup_expired()
            
            # Calculate size
            size_bytes = sys.getsizeof(image_data)
            
            # Evict old images if necessary
            while (self._current_memory_bytes + size_bytes > self._max_memory_bytes 
                   and len(self._cache) > 0):
                await self._evict_oldest()
            
            # Generate new ID
            image_id = self._next_id
            self._next_id += 1
            
            # Create cache entry
            current_time = time.time()
            cached_image = CachedImage(
                image_data=image_data,
                width=width,
                height=height,
                last_access_time=current_time,
                upload_time=current_time,
                size_bytes=size_bytes
            )
            
            # Store in cache
            self._cache[image_id] = cached_image
            self._current_memory_bytes += size_bytes
            
            print(f"Image uploaded: ID={image_id}, size={size_bytes} bytes, "
                  f"total_cache={self._current_memory_bytes} bytes "
                  f"({self._current_memory_bytes / (1024**2):.2f} MB), "
                  f"count={len(self._cache)}")
            
            return image_id
    
    async def get_image(self, image_id: int) -> Optional[Tuple[bytes, int, int]]:
        """
        Retrieve an image from the cache by ID.
        
        Args:
            image_id: The unique image ID
            
        Returns:
            Tuple of (image_data, width, height) if found, None otherwise
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
            
            print(f"Image retrieved: ID={image_id}, size={cached_image.size_bytes} bytes")
            
            return (cached_image.image_data, cached_image.width, cached_image.height)
    
    async def delete_image(self, image_id: int) -> bool:
        """
        Delete an image from the cache.
        
        Args:
            image_id: The unique image ID to delete
            
        Returns:
            True if the image was deleted, False if not found
        """
        async with self._lock:
            if image_id not in self._cache:
                print(f"Image deletion failed (not found): ID={image_id}")
                return False
            
            cached_image = self._cache[image_id]
            del self._cache[image_id]
            self._current_memory_bytes -= cached_image.size_bytes
            
            print(f"Image deleted: ID={image_id}, freed={cached_image.size_bytes} bytes, "
                  f"total_cache={self._current_memory_bytes} bytes "
                  f"({self._current_memory_bytes / (1024**2):.2f} MB), "
                  f"count={len(self._cache)}")
            
            return True
    
    async def _evict_oldest(self):
        """
        Evict the least recently used image from the cache (internal method).
        """
        if not self._cache:
            return
        
        # Find the image with the oldest last_access_time
        oldest_id = min(self._cache.keys(), 
                       key=lambda k: self._cache[k].last_access_time)
        
        cached_image = self._cache[oldest_id]
        del self._cache[oldest_id]
        self._current_memory_bytes -= cached_image.size_bytes
        
        print(f"Image evicted (LRU): ID={oldest_id}, freed={cached_image.size_bytes} bytes, "
              f"total_cache={self._current_memory_bytes} bytes "
              f"({self._current_memory_bytes / (1024**2):.2f} MB), "
              f"count={len(self._cache)}")
    
    async def _cleanup_expired(self):
        """
        Remove expired images from the cache (internal method).
        Images are expired if they haven't been accessed within the TTL period.
        """
        current_time = time.time()
        expired_ids = []
        
        for image_id, cached_image in self._cache.items():
            # Check if image has expired based on last access time
            time_since_access = current_time - cached_image.last_access_time
            if time_since_access > self._ttl_seconds:
                expired_ids.append(image_id)
        
        # Remove expired images
        for image_id in expired_ids:
            cached_image = self._cache[image_id]
            del self._cache[image_id]
            self._current_memory_bytes -= cached_image.size_bytes
            
            print(f"Image expired (TTL): ID={image_id}, "
                  f"age={current_time - cached_image.last_access_time:.1f}s, "
                  f"freed={cached_image.size_bytes} bytes")
        
        if expired_ids:
            print(f"Cleanup complete: removed {len(expired_ids)} expired images, "
                  f"total_cache={self._current_memory_bytes} bytes "
                  f"({self._current_memory_bytes / (1024**2):.2f} MB), "
                  f"count={len(self._cache)}")
    
    async def get_stats(self) -> Dict[str, any]:
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

