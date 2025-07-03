using AnnotationService.Types;
using System.ServiceModel;

namespace AnnotationService.Interfaces
{
    [ServiceContract]
    public interface IVolumeMeta
    {
        [OperationContract]
        Scale GetScale();
    }
}
