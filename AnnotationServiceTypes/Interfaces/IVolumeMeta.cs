using System;
using System.ServiceModel;
using AnnotationService.Types;

namespace AnnotationService.Interfaces
{
    [ServiceContract]
    interface IVolumeMeta
    {
        [OperationContract]
        Scale GetScale();
    }
}
