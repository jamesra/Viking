using ConnectomeODataV4.Controllers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ConnectomeODataV4.Tests
{
    /// <summary>Pins the hop-count clamp applied by the OData network functions before SQL.</summary>
    [TestClass]
    public class NetworkHopsTest
    {
        /// <summary>Values above <see cref="NetworkHops.MaxHops"/> are clamped to it, never rejected.</summary>
        [DataTestMethod]
        [DataRow(1025)]
        [DataRow(4096)]
        [DataRow(int.MaxValue)]
        public void HopsAboveLimitClampToLimit(int hops)
        {
            Assert.AreEqual(NetworkHops.MaxHops, NetworkHops.Clamp(hops));
        }

        /// <summary>Values at or below the limit, including zero and negatives, pass through unchanged.</summary>
        [DataTestMethod]
        [DataRow(int.MinValue)]
        [DataRow(-1)]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(1023)]
        [DataRow(1024)]
        public void HopsAtOrBelowLimitAreUnchanged(int hops)
        {
            Assert.AreEqual(hops, NetworkHops.Clamp(hops));
        }

        /// <summary>The limit is the value the user approved for the public OData API.</summary>
        [TestMethod]
        public void LimitIs1024()
        {
            Assert.AreEqual(1024, NetworkHops.MaxHops);
        }
    }
}
