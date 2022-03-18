using System;
using System.ServiceModel.Configuration;



namespace Viking.Tokens
{
    public class TokenInjectionBehaviorExtension : BehaviorExtensionElement
    {
        public override Type BehaviorType => typeof(TokenInjectionEndpointBehavior);

        protected override object CreateBehavior()
        {
            // Create the  endpoint behavior that will insert the message  
            // inspector into the client runtime  
            return new TokenInjectionEndpointBehavior();
        }
    }
}
