using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viking.DataModel.Annotation;
using NtsEnvelope = NetTopologySuite.Geometries.Envelope;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Test-only HTTP resolve for lounge / Viking Test deep-link prototypes.
    /// Maps <c>GET /test/resolve/location/{id}</c> to volume, section, center, and bbox
    /// from AnnotationTest. Enabled only when <c>TestResolve:Enabled</c> is true;
    /// never wire this onto production Annotation hosts.
    /// </summary>
    static class TestResolveEndpoints
    {
        /// <summary>
        /// Registers the anonymous test resolve route when <paramref name="configuration"/>
        /// has <c>TestResolve:Enabled</c> set. Callers are the Jotunn/Viking Test command
        /// palette experiment; production stacks leave the flag off.
        /// </summary>
        public static void MapIfEnabled(IEndpointRouteBuilder endpoints, IConfiguration configuration)
        {
            if (!configuration.GetValue("TestResolve:Enabled", false))
                return;

            string volumeName = configuration["TestResolve:VolumeName"] ?? "AnnotationTest";

            endpoints.MapGet("/test/resolve/location/{id:long}", async (HttpContext context, long id) =>
            {
                var db = context.RequestServices.GetRequiredService<AnnotationContext>();
                var location = await db.Locations.AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == id, context.RequestAborted);
                if (location == null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsJsonAsync(new { error = $"Location {id} not found" });
                    return;
                }

                NtsEnvelope envelope = EnvelopeFrom(location.VolumeShape)
                    ?? new NtsEnvelope(location.VolumeX, location.VolumeX, location.VolumeY, location.VolumeY);

                await context.Response.WriteAsJsonAsync(new
                {
                    locationId = location.Id,
                    structureId = location.ParentId,
                    volume = volumeName,
                    section = location.Z,
                    center = new { x = location.VolumeX, y = location.VolumeY },
                    bbox = new
                    {
                        minX = envelope.MinX,
                        minY = envelope.MinY,
                        maxX = envelope.MaxX,
                        maxY = envelope.MaxY
                    }
                });
            });
        }

        static NtsEnvelope EnvelopeFrom(NtsGeometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
                return null;
            return geometry.EnvelopeInternal;
        }
    }
}
