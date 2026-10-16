using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Runs <see cref="ODataMorphologyLoader"/> against an in-process server that answers with the OData V4 JSON the
    /// ConnectomeODataV4 service produces, so the shared paging, retry and scale helpers are exercised end to end.
    /// </summary>
    [TestClass]
    public class ODataMorphologyLoaderTests
    {
        static string Loc(long id, long parent, long z, double volX = 0) =>
            $"{{\"ID\":{id},\"ParentID\":{parent},\"Z\":{z},\"X\":1.5,\"Y\":2.5,\"VolumeX\":{volX},\"VolumeY\":0,\"Terminal\":false,\"OffEdge\":true,\"TypeCode\":1,\"LastModified\":\"2026-01-02T03:04:05Z\"}}";

        static async Task<(WebApplication App, Uri Root, List<string> Requests)> StartAsync(Func<string, string> respond)
        {
            var requests = new List<string>();
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            WebApplication app = builder.Build();
            app.Run(async context =>
            {
                string pathAndQuery = Uri.UnescapeDataString(context.Request.Path + context.Request.QueryString.ToString());
                lock (requests)
                    requests.Add(pathAndQuery);
                string body = respond(pathAndQuery);
                if (body is null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                context.Response.Headers["OData-Version"] = "4.0";
                context.Response.ContentType = "application/json;odata.metadata=minimal";
                await context.Response.WriteAsync(body);
            });
            await app.StartAsync();
            string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            return (app, new Uri(address.TrimEnd('/') + "/odata/"), requests);
        }

        [TestMethod]
        public async Task LoadRowsAsync_FollowsPaging_AndKeepsOnlyLinksAmongCandidates()
        {
            string root = null;
            var (app, uri, requests) = await StartAsync(path =>
            {
                if (path.Contains("ResidualFieldLocations(MinLocations=4)") && !path.Contains("skiptoken"))
                    return "{\"@odata.context\":\"" + root + "$metadata#Locations\",\"value\":[" + Loc(1, 10, 5, volX: 7) + "," + Loc(2, 10, 6) + "],\"@odata.nextLink\":\"" + root + "ResidualFieldLocations(MinLocations=4)?$skiptoken=2\"}";
                if (path.Contains("skiptoken"))
                    return "{\"@odata.context\":\"" + root + "$metadata#Locations\",\"value\":[" + Loc(3, 10, 7) + "]}";
                if (path.Contains("LocationLinks"))
                    return "{\"@odata.context\":\"" + root + "$metadata#LocationLinks\",\"value\":[{\"A\":1,\"B\":2,\"Created\":\"2026-01-01T00:00:00Z\"},{\"A\":2,\"B\":999,\"Created\":\"2026-01-01T00:00:00Z\"}]}";
                return null;
            });
            await using (app)
            {
                root = uri.ToString();
                var loader = new ODataMorphologyLoader(uri);

                var (candidates, links) = await loader.LoadRowsAsync(4, CancellationToken.None);

                Assert.AreEqual(3, candidates.Count);
                Assert.AreEqual(10, candidates[2].ParentID);
                Assert.AreEqual(7, candidates[2].Z);
                Assert.AreEqual(1, links.Count, "the link to a location that is not a candidate is dropped");
                Assert.AreEqual((1L, 2L), (links[0].A, links[0].B));
                Assert.IsTrue(requests.Any(r => r.Contains("$select=ID,ParentID,Z,X,Y,Terminal,OffEdge,TypeCode,LastModified")));
                Assert.IsFalse(requests.Any(r => r.Contains("VolumeX")), "stored volume positions must not be requested");
            }
        }

        [TestMethod]
        public async Task LoadRowsAsync_ServerError_Throws()
        {
            var (app, uri, _) = await StartAsync(_ => null);
            await using (app)
            {
                var loader = new ODataMorphologyLoader(uri);

                await Assert.ThrowsAsync<Exception>(() => loader.LoadRowsAsync(3, CancellationToken.None), "a 404 must surface, not look like an empty database");
            }
        }
    }
}
