using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>Pins the message limits and read-only retry policy every annotation channel gets.</summary>
    [TestFixture]
    public class GrpcChannelDefaultsTests
    {
        private const string ServicePackage = "Viking.AnnotationServiceTypes.gRPC.V1.";

        /// <summary>
        /// Every annotation RPC that changes data, as "Service/Method". These must never be retried: a retry after a dropped
        /// response would repeat the write. Each RPC of the annotation services is on this list or in the retry policy.
        /// </summary>
        private static readonly string[] Mutations =
        {
            "AnnotateLocations/CreateLocation", "AnnotateLocations/Update", "AnnotateLocations/CreateLocationLink",
            "AnnotateLocations/DeleteLocationLink",
            "AnnotateStructures/CreateStructure", "AnnotateStructures/CreateStructureLink", "AnnotateStructures/DeleteStructureLink",
            "AnnotateStructures/Update", "AnnotateStructures/UpdateLinks", "AnnotateStructures/Merge", "AnnotateStructures/Split",
            "AnnotateStructures/SplitAtLocationLink",
            "AnnotateStructureTypes/CreateStructureType", "AnnotateStructureTypes/Update",
            "PermittedStructureLinks/CreatePermittedStructureLink", "PermittedStructureLinks/UpdatePermittedStructureLinks",
        };

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

        /// <summary>
        /// Classifies every RPC in the generated annotation service descriptors: each is retried or is a named mutation, never
        /// both, and every retried name is a real RPC. A new RPC fails this test until it is added to one list.
        /// </summary>
        [Test]
        public void RetryPolicy_CoversEveryReadRpcAndNoMutation()
        {
            var retried = GrpcChannelDefaults.CreateServiceConfig().MethodConfigs.Single().Names
                .Select(n => n.Service + "/" + n.Method).ToHashSet();
            var mutations = Mutations.Select(m => ServicePackage + m).ToHashSet();
            var rpcs = AnnotationServices().SelectMany(s => s.Methods).Select(m => m.Service.FullName + "/" + m.Name).ToHashSet();

            Assert.That(rpcs, Has.Count.GreaterThan(mutations.Count), "descriptors were not found");
            Assert.That(retried.Intersect(mutations), Is.Empty, "mutations must not be retried");
            Assert.That(retried.Except(rpcs), Is.Empty, "retried names that are not RPCs (typo or removed RPC)");
            Assert.That(mutations.Except(rpcs), Is.Empty, "mutation names that are not RPCs");
            Assert.That(rpcs.Except(retried).Except(mutations), Is.Empty,
                "RPCs neither retried nor listed as mutations; add reads to GrpcChannelDefaults.ReadMethods");
        }

        /// <summary>Services of every generated proto file in the annotation types assembly that live in the annotation package.</summary>
        private static IEnumerable<ServiceDescriptor> AnnotationServices()
        {
            return typeof(AnnotateLocations).Assembly.GetTypes()
                .Select(t => t.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static))
                .Where(p => p != null && p.PropertyType == typeof(FileDescriptor))
                .Select(p => (FileDescriptor)p.GetValue(null))
                .SelectMany(f => f.Services)
                .Where(s => s.FullName.StartsWith(ServicePackage))
                .Distinct();
        }
    }
}
