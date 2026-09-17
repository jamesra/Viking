using System.Threading.Tasks;

namespace WebAnnotationModel.ServerInterface
{
    /// <summary>Merges UPDATE_SOURCE into an existing UPDATE_TARGET. Use IObjectConverter to construct a new instance.</summary>
    public interface IObjectUpdater<UPDATE_TARGET, UPDATE_SOURCE>
    {
        /// <summary>
        /// Update the object properties by copying the properties of the target.
        /// </summary>
        /// <returns>True if properties on obj where changed, false if no change occurred</returns>
        Task<bool> Update(UPDATE_TARGET obj, UPDATE_SOURCE update);
    }
}