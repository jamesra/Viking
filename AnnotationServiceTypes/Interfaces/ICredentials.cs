using System.ServiceModel;

namespace AnnotationService.Interfaces
{
    [ServiceContract]
    public interface ICredentials
    {
        [OperationContract]
        bool CanRead();

        [OperationContract]
        bool CanWrite();

        [OperationContract]
        bool CanAdmin();

        [OperationContract]
        string Roles();
    }
}
