using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using Viking.AnnotationServiceTypes;

namespace WebAnnotationModel
{
    public class ConcurrentObservablePermittedStructureLinkSet : ConcurrentObservableSet<PermittedStructureLinkObj>
    { 
        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created.
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        public override async Task<bool> AddAsync(PermittedStructureLinkObj ID)
        {
            try
            {
                await LinkLock.WaitAsync();
                var existing =
                    Observable.FirstOrDefault(link => link.SourceTypeID == ID.SourceTypeID && link.TargetTypeID == ID.TargetTypeID);
                if (existing != null)
                    return false;

                Observable.Add(ID);
                return true;
            }
            finally
            {
                LinkLock.Release();
            }
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        public override Task<bool> RemoveAsync(PermittedStructureLinkObj link)
        {
            return RemoveAsync(link.ID);
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        public async Task<bool> RemoveAsync(PermittedStructureLinkKey key)
        {
            try
            {
                await LinkLock.WaitAsync();
                PermittedStructureLinkObj LinkToRemove = Observable.FirstOrDefault(link => link.SourceTypeID == key.SourceTypeID && link.TargetTypeID == key.TargetTypeID);
                if (LinkToRemove == null)
                    return false;

                Observable.Remove(LinkToRemove);
                return true;
            }
            finally
            {
                LinkLock.Release();
            }
        }
    }
}