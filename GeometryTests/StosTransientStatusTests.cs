using Geometry.Transforms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

namespace GeometryTests
{
    /// <summary>Pins which HTTP statuses make <see cref="TransformFactory.ParseStos(System.Uri, System.Xml.Linq.XElement, NetworkCredential)"/> retry a .stos download instead of skipping it.</summary>
    [TestClass]
    public class StosTransientStatusTests
    {
        [DataTestMethod]
        [DataRow(HttpStatusCode.RequestTimeout)]
        [DataRow((HttpStatusCode)429)]
        [DataRow(HttpStatusCode.InternalServerError)]
        [DataRow(HttpStatusCode.BadGateway)]
        [DataRow(HttpStatusCode.ServiceUnavailable)]
        [DataRow(HttpStatusCode.GatewayTimeout)]
        public void TransientStatusesAreRetried(HttpStatusCode status)
        {
            Assert.IsTrue(TransformFactory.IsTransientStosStatus(status));
        }

        [DataTestMethod]
        [DataRow(HttpStatusCode.BadRequest)]
        [DataRow(HttpStatusCode.Unauthorized)]
        [DataRow(HttpStatusCode.Forbidden)]
        [DataRow(HttpStatusCode.NotFound)]
        [DataRow(HttpStatusCode.OK)]
        public void PermanentStatusesAreNotRetried(HttpStatusCode status)
        {
            Assert.IsFalse(TransformFactory.IsTransientStosStatus(status));
        }
    }
}
