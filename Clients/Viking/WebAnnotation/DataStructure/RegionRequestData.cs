
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Linq;
using Viking.Common;
using Geometry;
using Viking.ViewModels;
using WebAnnotationModel;
using System.ComponentModel;
using System.Threading.Tasks;
using SqlGeometryUtils;
using WebAnnotation.View;

namespace WebAnnotation
{
    /// <summary>
    /// Stores information about location queries for this region in the volume
    /// </summary>
    public class RegionRequestData
    {
        public DateTime LastQuery = DateTime.MinValue;

        /// <summary>
        /// True if a query has been sent to the server but has not returned
        /// </summary>
        public bool OutstandingQuery
        {
            get
            {
                if (this.AsyncResult == null)
                    return false;

                return AsyncResult.IsCompleted;
            }
        }

        public IAsyncResult AsyncResult;

        public RegionRequestData(DateTime query, IAsyncResult result)
        {
            AsyncResult = result;
            query = LastQuery;
        }
    }

    public class AnnotationRegions : RegionPyramid<RegionRequestData>
    {
        /// <summary>
        /// If set to true any threads using this objects should cancel loading operations
        /// </summary>
        public bool CancelRunningOperations = false;

        public AnnotationRegions(GridRectangle Boundaries, GridCellDimensions cellDimensions)
            : base(Boundaries, cellDimensions)
        { }
    }
}
