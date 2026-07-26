using System.Collections.Generic;
using System.Linq; 
using System.Threading;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    public class ConcurrentObservableAttributeSet : ConcurrentObservableSet<ObjAttribute>
    {
        public ConcurrentObservableAttributeSet() : base()
        {
        }

        public ConcurrentObservableAttributeSet(IEnumerable<ObjAttribute> collection) : base(collection)
        {
        }

        /// <summary>
        /// Remove tag if it is in the attribute list or add tag if it is not
        /// </summary>
        /// <param name="listAttributes"></param>
        /// <param name="tag"></param>
        /// <param name="value"></param>
        /// <returns>True if the tag exists in the attributes after the function has completed.</returns>
        public async Task<bool> ToggleAttribute(string tag, string value = null)
        {
            try
            {
                await LinkLock.WaitAsync();
                var existing = Observable.FirstOrDefault(a => a.Equals(tag));
                bool FoundExisting = null != existing;
                if (FoundExisting)
                {
                    while (null != existing)
                    {
                        Observable.Remove(existing);
                        existing = Observable.FirstOrDefault(a => a.Equals(tag));
                    }

                    return false;
                }
                else
                {
                    ObjAttribute attrib = new ObjAttribute(tag, value);
                    Observable.Add(attrib);
                    return true;
                }
            }
            finally
            {
                LinkLock.Release();
            }  
        }

        /// <summary>
        /// Clear existing attributes and replace with the passed set
        /// </summary>
        /// <param name="attribs"></param>
        /// <returns></returns>
        public async Task SetAttributes(IEnumerable<ObjAttribute> attribs)
        {
            await ClearAsync();

            foreach (var a in attribs)
                await AddAsync(a);
        }

        public async Task<bool> Contains(string tag)
        {
            try
            {
                await LinkLock.WaitAsync();
                //We cannot use the built-in contains function because ObjAttribute equality comparer also checks the value of the attribute
                return Observable.Any(a => a.Equals(tag));
            }
            finally
            {
                LinkLock.Release();
            }
        } 
    }
}