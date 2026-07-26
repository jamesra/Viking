
namespace AnnotationService.Interfaces
{
    /* Recoded [ServiceContract] */
    interface ICredentials
    {
        /* Recoded [OperationContract] */
        bool CanRead();

        /* Recoded [OperationContract] */
        bool CanWrite();

        /* Recoded [OperationContract] */
        bool CanAdmin();
    }
}

