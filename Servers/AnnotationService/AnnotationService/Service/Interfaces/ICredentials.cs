using System;
using System.ServiceModel;

namespace Annotation.Service.Interfaces
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
