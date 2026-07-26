using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebAnnotationModel.Objects;
using Viking.AnnotationServiceTypes;

namespace WebAnnotationModel
{
    public class ConcurrentObservableStructureLinkSet : ConcurrentObservableSet<StructureLinkObj>
    {

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created.
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        public override async Task<bool> AddAsync(StructureLinkObj ID)
        {
            try
            {
                await LinkLock.WaitAsync();
                var existing =
                    Observable.FirstOrDefault(link => link.SourceID == ID.SourceID && link.TargetID == ID.TargetID);
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
        public override Task<bool> RemoveAsync(StructureLinkObj link)
        {
            return RemoveAsync(link.ID);
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// Because Links is an observable collection all modifications must be syncronized
        /// </summary>
        /// <param name="ID"></param>
        public async Task<bool> RemoveAsync(StructureLinkKey key)
        {
            try
            {
                await LinkLock.WaitAsync();
                StructureLinkObj LinkToRemove = Observable.FirstOrDefault(link => link.SourceID == key.SourceID && link.TargetID == key.TargetID);
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