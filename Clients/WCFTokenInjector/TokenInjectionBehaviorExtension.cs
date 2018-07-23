using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Configuration;



namespace Viking.Tokens
{
    public class TokenInjectionBehaviorExtension : BehaviorExtensionElement
    {
        public override Type BehaviorType
        {
            get { return typeof(TokenInjectionEndpointBehavior); }
        }

        protected override object CreateBehavior()
        {
            // Create the  endpoint behavior that will insert the message  
            // inspector into the client runtime  
            return new TokenInjectionEndpointBehavior();
        }
    }
}
