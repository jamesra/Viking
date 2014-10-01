using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using Common.DataStructures;
using Microsoft.Xna.Framework.Graphics;

namespace Viking.Threading
{
    /*
    class TextureEntry : CacheEntry<Texture2D>
    {


    }

    class TextureCache : TimeQueueCache<Uri, TextureEntry, Texture2D, Texture2D>
    {

    }


    /// <summary>
    /// Data required to perform a texture request
    /// </summary>
    public class TextureRequestData : IEqualityComparer<TextureRequestData>
    {
        /// <summary>
        /// Name of texture on server
        /// </summary>
        public readonly Uri TextureUri;

        /// <summary>
        /// Name of texture in local cache
        /// </summary>
        public readonly string CacheFilename = null; 

        /// <summary>
        /// Indicates # of mipmap levels texture should be created with
        /// </summary>
        public int MipMapLevels = 1; 

        public TextureRequestData(Uri textureUri, string cacheFilename, int mipMapLevels)
            : this(textureUri, mipMapLevels)
        { 
            this.CacheFilename = cacheFilename; 
        }

        public override string ToString()
        {
            return TextureUri.ToString();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <summary>
        /// This texture reader is used when we don't have a cachepath to check before making the request
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="filename"></param>
        /// <param name="downsample"></param>
        public TextureRequestData(Uri textureURI, int mipMapLevels)
        {
            this.TextureUri = textureURI;
            this.MipMapLevels = mipMapLevels; 
        }

        public bool Equals(TextureRequestData x, TextureRequestData y)
        {
            return x.TextureUri == y.TextureUri;
        }

        public int GetHashCode(TextureRequestData obj)
        {
            return obj.TextureUri.GetHashCode();
        }
    }


    class TextureRequests
    {
        //ConcurrentStack<TextureRequestData> Requests = new ConcurrentStack<TextureRequestData>();
        Stack<TextureRequestData> Requests = new Stack<TextureRequestData>();
        List<TextureRequestData> InFlightRequests = new List<TextureRequestData>();

        TextureCache textureCache = new TextureCache<TextureData, TextureCacheEntry, Texture2D, Texture2D>();

        System.Threading.AutoResetEvent ItemQueuedEvent = new AutoResetEvent(false);
        System.Threading.ReaderWriterLockSlim RWLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);


        public bool GetOrRequestTexture(TextureRequestData textureData)
        { 
            
        }

        /// <summary>
        /// Adds a texture to request, returns false if the texture is already requested
        /// </summary>
        /// <param name="textureData"></param>
        /// <returns></returns>
        public bool TryAddTextureRequest(TextureRequestData textureData)
        {
            try
            {
                RWLock.EnterUpgradeableReadLock();

                if (Requests.Contains(textureData))
                {
                    return false;
                }
                else
                {
                    try
                    {
                        RWLock.EnterWriteLock();
                        Requests.Push(textureData);
                        ItemQueuedEvent.Set();
                        return true;
                    }
                    finally
                    {
                        RWLock.ExitWriteLock();
                    }
                    
                }
            }
            finally
            {
                RWLock.ExitUpgradeableReadLock();
            }
        }


        public TextureRequests()
        {


        }


        /// <summary>
        /// Removes Uri's from the request list.  If the list is empty wait on an event until a Uri is queued.
        /// </summary>
        public void DequeueThread()
        {
            while(true)
            {
                try
                {
                    RWLock.EnterWriteLock();
                    ItemQueuedEvent.Reset();

                    while(Requests.Count > 0)
                    {
                        TextureRequestData textureData = Requests.Pop();

                        InFlightRequests.Add(textureData);

                        LoadTexture(textureData);
                    }
                    
                    ItemQueuedEvent.WaitOne();
                }
                finally
                {
                    RWLock.ExitWriteLock();
                }
            }
        }


        /// <summary>
        /// Kicks off a request to read a texture
        /// </summary>
        /// <param name="textureData"></param>
        void LoadTexture(TextureRequestData textureData)
        {
            
        }

    }
     * 
     */
}
