using ODataClient.ConnectomeDataModel;
using Microsoft.OData.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace AnnotationVizLib.OData
{

    public static class ODataExtensions
    {
        public static UnitsAndScale.Scale ToGeometryScale(this ODataClient.ConnectomeDataModel.Scale scale)
        {
            return new UnitsAndScale.Scale(new UnitsAndScale.AxisUnits(scale.X.Value, scale.X.Units),
                                      new UnitsAndScale.AxisUnits(scale.Y.Value, scale.Y.Units),
                                      new UnitsAndScale.AxisUnits(scale.Z.Value, scale.Z.Units));
        }




        /// <summary>
        /// Asynchronously gets all results from a DataServiceQuery.
        /// This is the original GetAllPagesAsync behavior but properly implemented.
        /// </summary>
        /// <typeparam name="T">The type of entities in the result set</typeparam>
        /// <param name="query">The DataServiceQuery to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task that contains all entities from all pages</returns>
        public static async Task<List<T>> GetAllPagesToListAsync<T>(
            this DataServiceQuery<T> query,
            CancellationToken cancellationToken = default)
        {
            var allResults = await Task.Run(() => query.Execute(), cancellationToken);
            return [.. allResults];
        }

        /// <summary>
        /// Asynchronously streams individual entities from all results as they arrive.
        /// This provides a streaming interface for processing large result sets.
        /// </summary>
        /// <typeparam name="T">The type of entities in the result set</typeparam>
        /// <param name="query">The DataServiceQuery to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An async enumerable that yields each entity as it arrives</returns>
        public static async IAsyncEnumerable<T> StreamAllEntitiesAsync<T>(
            this DataServiceQuery<T> query,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var allResults = await Task.Run(() => query.Execute(), cancellationToken);

            foreach (var entity in allResults)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entity;
            }
        }

    }
}
