using System;

namespace WebAnnotationModel.ServerInterface
{
    public interface IServerAnnotationsClientFactory<out INTERFACE>
    {
        INTERFACE GetOrCreate();
    }
}