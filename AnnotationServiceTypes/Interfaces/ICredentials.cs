using System;
using System.ServiceModel;
using AnnotationService.Types;

namespace AnnotationService.Interfaces
{
    [ServiceContract]
    interface ICredentials
    {
        [OperationContract]
        bool CanRead();

        [OperationContract]
        bool CanWrite();

        [OperationContract]
        bool CanAdmin();
    }
}
