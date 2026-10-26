using System.Linq;
using Grpc.Core;
using Grpc.Net.Client;
using NUnit.Framework;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>Pins the message limits and read-only retry policy every annotation channel gets.</summary>
    [TestFixture]
    public class GrpcChannelDefaultsTests
    {
        [Test]
        public void Apply_SetsMessageLimitsAndRetryPolicy()
        {
            GrpcChannelOptions options = GrpcChannelDefaults.Apply(new GrpcChannelOptions());

            Assert.That(options.MaxReceiveMessageSize, Is.EqualTo(GrpcChannelDefaults.MaxMessageBytes));
            Assert.That(options.MaxSendMessageSize, Is.EqualTo(GrpcChannelDefaults.MaxMessageBytes));
            Assert.That(options.ServiceConfig, Is.Not.Null);
        }

        [Test]
        public void Apply_KeepsValuesTheCallerAlreadySet()
        {
            var serviceConfig = new Grpc.Net.Client.Configuration.ServiceConfig();
            GrpcChannelOptions options = GrpcChannelDefaults.Apply(new GrpcChannelOptions
            {
                MaxReceiveMessageSize = 1024,
                ServiceConfig = serviceConfig,
            });

            Assert.That(options.MaxReceiveMessageSize, Is.EqualTo(1024));
            Assert.That(options.ServiceConfig, Is.SameAs(serviceConfig));
        }

        [Test]
        public void RetryPolicy_RetriesUnavailableOnReadMethodsOnly()
        {
            var methodConfig = GrpcChannelDefaults.CreateServiceConfig().MethodConfigs.Single();
            var names = methodConfig.Names.Select(n => n.Method).ToList();

            Assert.That(methodConfig.RetryPolicy.RetryableStatusCodes, Is.EquivalentTo(new[] { StatusCode.Unavailable }));
            Assert.That(names, Does.Contain("GetLocationsForSection"));
            Assert.That(names, Does.Contain("GetStructuresByID"));
            foreach (string mutation in new[] { "Update", "CreateLocation", "CreateStructure", "Merge", "Split", "DeleteLocationLink" })
                Assert.That(names, Does.Not.Contain(mutation), mutation + " changes data and must not be retried");
        }
    }
}
