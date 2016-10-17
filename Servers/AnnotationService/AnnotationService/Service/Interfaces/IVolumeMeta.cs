using System;
using System.ServiceModel;

namespace Annotation.Service.Interfaces
{
    [ServiceContract]
    interface IVolumeMeta
    {
        [OperationContract]
        Scale GetScale();
    }
}
