// Annotation.Identity.IdentityServerBehavior.cs
using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using Duende.IdentityModel;

namespace Annotation.Identity
{
    public class IdentityServerBehavior : IEndpointBehavior
    {
        public string Authority { get; set; }
        public string Audience { get; set; }
        public bool RequireHttps { get; set; } = true;
        public bool ValidateIssuer { get; set; } = true;
        public bool ValidateAudience { get; set; } = true;
        public bool ValidateLifetime { get; set; } = true;
        public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
            // Add JWT token validation parameters
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            // Not needed for service side
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            // Add JWT token validation to the dispatch runtime
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(
                new JwtMessageInspector(Authority, Audience, ValidateIssuer, ValidateAudience, ValidateLifetime));
        }

        public void Validate(ServiceEndpoint endpoint)
        {
            // Validate configuration
        }
    }
}