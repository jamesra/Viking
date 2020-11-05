using AnnotationService.Types;
using System.ServiceModel;

namespace AnnotationService.Interfaces
{
    [ServiceContract]
    interface IVolumeMeta
    {
        [OperationContract]
        Scale GetScale();
    }
}
