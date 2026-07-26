using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Serves the volume metadata clients need before they can interpret annotation
    /// coordinates, which today is the scale of a pixel in real units.
    /// </summary>
    public class MetaDataService : Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotateMetaData.AnnotateMetaDataBase
    {
        private readonly AnnotationContext _context;
        private readonly ILogger<MetaDataService> _logger;

        public MetaDataService(AnnotationContext context, ILogger<MetaDataService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override async Task<ScaleResponse> Scale(ScaleRequest request, ServerCallContext context)
        {
            try
            {
                // The scale lives in scalar SQL functions rather than a table, so it is read
                // directly instead of through a DbSet.
                var xyValue = await ScalarAsync<double?>("SELECT dbo.XYScale()");
                var zValue = await ScalarAsync<double?>("SELECT dbo.ZScale()");
                var xyUnits = await ScalarAsync<string>("SELECT dbo.XYScaleUnits()");
                var zUnits = await ScalarAsync<string>("SELECT dbo.ZScaleUnits()");

                var scale = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Scale
                {
                    X = new AxisUnits { Units = xyUnits ?? string.Empty, Value = xyValue ?? 0 },
                    Y = new AxisUnits { Units = xyUnits ?? string.Empty, Value = xyValue ?? 0 }
                };

                if (zValue.HasValue)
                    scale.Z = new AxisUnits { Units = zUnits ?? string.Empty, Value = zValue.Value };

                return new ScaleResponse { Scale = scale };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "{Operation} failed", nameof(Scale));
                throw new RpcException(new Status(StatusCode.Unknown, nameof(Scale), e));
            }
        }

        private async Task<T> ScalarAsync<T>(string sql)
        {
            var results = await _context.Database.SqlQueryRaw<T>(sql).ToListAsync();
            return results.FirstOrDefault();
        }
    }
}
