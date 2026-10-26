using System;
using System.Collections.Generic;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace WebAnnotationModel.gRPC
{
    /// <summary>
    /// Channel options shared by every annotation gRPC channel: message limits sized for region and tile payloads, and an automatic
    /// retry of read calls that fail with <see cref="StatusCode.Unavailable"/> before any response arrives (a dropped connection
    /// or a server restart). Mutations are never retried. A stream that has already delivered a message is not retried either;
    /// the caller opens a new one.
    /// </summary>
    public static class GrpcChannelDefaults
    {
        /// <summary>Largest message sent or received; region and tile payloads exceed the 4 MB default.</summary>
        public const int MaxMessageBytes = 128 * 1024 * 1024;

        private const int GrpcDefaultReceiveBytes = 4 * 1024 * 1024;

        private const string ServicePackage = "Viking.AnnotationServiceTypes.gRPC.V1.";

        /// <summary>Read-only methods, by service, that are safe to repeat after a connection failure.</summary>
        private static readonly (string Service, string[] Methods)[] ReadMethods =
        {
            ("AnnotateLocations", new[]
            {
                "GetLocationByID", "GetLocationsByID", "GetLastModifiedLocation", "GetLinkedLocations", "GetLocationsForSection",
                "GetLocationChangesInMosaicRegion", "GetAnnotationsInMosaicRegion", "StreamLocationChangesInMosaicRegion",
                "StreamAnnotationsInMosaicRegion", "GetLocationChanges", "GetLocationLinksForSection",
                "GetLocationLinksForSectionInMosaicRegion", "GetLocationChangeLog", "GetStructureLocations",
            }),
            ("AnnotateStructures", new[]
            {
                "GetStructures", "GetStructuresForSection", "GetStructuresInMosaicRegion", "GetStructuresInVolumeRegion",
                "GetStructureByID", "GetStructuresByID", "GetLinkedStructures", "GetNetworkedStructures",
                "GetChildStructuresInNetwork", "GetStructureLinksInNetwork", "NumberOfLocations", "GetUnfinishedLocations",
                "GetUnfinishedLocationsWithPosition", "GetStructuresOfType",
            }),
            ("PermittedStructureLinks", new[] { "GetPermittedStructureLinks" }),
        };

        /// <summary>
        /// Fills in the message limits and read-retry policy on <paramref name="options"/>, leaving any limit or policy the caller already
        /// chose. Returns the same instance so it can be used inline.
        /// </summary>
        public static GrpcChannelOptions Apply(GrpcChannelOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            // GrpcChannelOptions starts the receive limit at gRPC's 4 MB default (the send limit starts unset), so a value still
            // at the default is raised too; a caller that chose any other limit keeps it.
            if (options.MaxReceiveMessageSize is null || options.MaxReceiveMessageSize == GrpcDefaultReceiveBytes)
                options.MaxReceiveMessageSize = MaxMessageBytes;
            if (options.MaxSendMessageSize is null)
                options.MaxSendMessageSize = MaxMessageBytes;
            if (options.ServiceConfig is null)
                options.ServiceConfig = CreateServiceConfig();
            return options;
        }

        /// <summary>New options with the defaults applied and the given HTTP handler (null keeps the platform default).</summary>
        public static GrpcChannelOptions CreateOptions(System.Net.Http.HttpMessageHandler httpHandler = null)
        {
            var options = new GrpcChannelOptions { HttpHandler = httpHandler };
            return Apply(options);
        }

        /// <summary>The retry policy for <see cref="ReadMethods"/>: up to four attempts with exponential backoff on Unavailable.</summary>
        internal static ServiceConfig CreateServiceConfig()
        {
            var retry = new RetryPolicy
            {
                MaxAttempts = 4,
                InitialBackoff = TimeSpan.FromMilliseconds(200),
                MaxBackoff = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 2,
                RetryableStatusCodes = { StatusCode.Unavailable },
            };

            var methodConfig = new MethodConfig { RetryPolicy = retry };
            foreach (var read in ReadMethods)
            {
                foreach (string method in read.Methods)
                    methodConfig.Names.Add(new MethodName { Service = ServicePackage + read.Service, Method = method });
            }

            return new ServiceConfig { MethodConfigs = { methodConfig } };
        }
    }
}
