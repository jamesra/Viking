// Annotation.Identity.IdentityServerBehaviorExtension.cs
using System;
using System.ServiceModel.Configuration;

namespace Annotation.Identity
{
    public class IdentityServerBehaviorExtension : BehaviorExtensionElement
    {
        protected override object CreateBehavior()
        {
            return new IdentityServerBehavior();
        }

        public override Type BehaviorType => typeof(IdentityServerBehavior);
    }
}