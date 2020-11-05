using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;



namespace Viking.Tokens
{
    public class TokenInjectionEndpointBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
        {
            // No implementation necessary  
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(new TokenInjector());
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            // No implementation necessary  
        }

        public void Validate(ServiceEndpoint endpoint)
        {
            // No implementation necessary  
        }
    }
}
