// Annotation.Identity.IdentityServerBehaviorExtension.cs
using System;
using System.Configuration;
using System.ServiceModel.Configuration;

namespace Annotation.Identity
{
    public class IdentityServerBehaviorExtension : BehaviorExtensionElement
    {
        [ConfigurationProperty("authority", IsRequired = true)]
        public string Authority
        {
            get { return (string)base["authority"]; }
            set { base["authority"] = value; }
        }

        [ConfigurationProperty("audience", IsRequired = true)]
        public string Audience
        {
            get { return (string)base["audience"]; }
            set { base["audience"] = value; }
        }

        [ConfigurationProperty("requireHttps", DefaultValue = true)]
        public bool RequireHttps
        {
            get { return (bool)base["requireHttps"]; }
            set { base["requireHttps"] = value; }
        }

        [ConfigurationProperty("validateIssuer", DefaultValue = true)]
        public bool ValidateIssuer
        {
            get { return (bool)base["validateIssuer"]; }
            set { base["validateIssuer"] = value; }
        }

        [ConfigurationProperty("validateAudience", DefaultValue = true)]
        public bool ValidateAudience
        {
            get { return (bool)base["validateAudience"]; }
            set { base["validateAudience"] = value; }
        }

        [ConfigurationProperty("validateLifetime", DefaultValue = true)]
        public bool ValidateLifetime
        {
            get { return (bool)base["validateLifetime"]; }
            set { base["validateLifetime"] = value; }
        }

        [ConfigurationProperty("clockSkew", DefaultValue = "00:05:00")]
        public string ClockSkew
        {
            get { return (string)base["clockSkew"]; }
            set { base["clockSkew"] = value; }
        }

        protected override object CreateBehavior()
        {
            var behavior = new IdentityServerBehavior();
            behavior.Authority = this.Authority;
            behavior.Audience = this.Audience;
            behavior.RequireHttps = this.RequireHttps;
            behavior.ValidateIssuer = this.ValidateIssuer;
            behavior.ValidateAudience = this.ValidateAudience;
            behavior.ValidateLifetime = this.ValidateLifetime;
            behavior.ClockSkew = TimeSpan.Parse(this.ClockSkew);
            return behavior;
        }

        public override Type BehaviorType => typeof(IdentityServerBehavior);
    }
}