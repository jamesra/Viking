using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitsAndScale;

namespace UnitsAndScaleTests
{
    /// <summary>
    /// Characterizes <see cref="AxisUnits"/> explicit <see cref="IAxisUnits"/> forwarding and
    /// <see cref="Scale"/> retention of per-axis scale metadata used by volume XML and gRPC conversions.
    /// </summary>
    [TestClass]
    public class ScaleTests
    {
        private sealed class StubAxisUnits : IAxisUnits
        {
            public StubAxisUnits(double value, string units)
            {
                Value = value;
                Units = units;
            }

            public double Value { get; }
            public string Units { get; }
        }

        [TestMethod]
        public void AxisUnits_PublicProperties_MatchConstructor()
        {
            var axis = new AxisUnits(2.5, "nm");
            Assert.AreEqual(2.5, axis.Value);
            Assert.AreEqual("nm", axis.Units);
        }

        [TestMethod]
        public void AxisUnits_ThroughIAxisUnits_ForwardsValueAndUnits()
        {
            IAxisUnits axis = new AxisUnits(4.0, "µm");
            Assert.AreEqual(4.0, axis.Value);
            Assert.AreEqual("µm", axis.Units);
        }

        [TestMethod]
        public void AxisUnits_ExplicitInterfaceMatchesPublic_ForVolumeTypicalNmScale()
        {
            var axis = new AxisUnits(3.84, "nm");
            IAxisUnits viaInterface = axis;
            Assert.AreEqual(axis.Value, viaInterface.Value);
            Assert.AreEqual(axis.Units, viaInterface.Units);
        }

        [TestMethod]
        public void Scale_RetainsSameAxisInstances()
        {
            var x = new StubAxisUnits(1.0, "nm");
            var y = new StubAxisUnits(1.0, "nm");
            var z = new StubAxisUnits(90.0, "nm");
            var scale = new Scale(x, y, z);
            Assert.AreSame(x, scale.X);
            Assert.AreSame(y, scale.Y);
            Assert.AreSame(z, scale.Z);
        }

        [TestMethod]
        public void Scale_AxisUnitsValues_AreIndependentPerAxis()
        {
            var scale = new Scale(
                new AxisUnits(2.0, "nm"),
                new AxisUnits(2.0, "nm"),
                new AxisUnits(50.0, "nm"));
            Assert.AreEqual(2.0, scale.X.Value);
            Assert.AreEqual(2.0, scale.Y.Value);
            Assert.AreEqual(50.0, scale.Z.Value);
            Assert.AreEqual("nm", scale.X.Units);
            Assert.AreEqual("nm", scale.Y.Units);
            Assert.AreEqual("nm", scale.Z.Units);
        }

        [TestMethod]
        public void Scale_ThroughIScale_ExposesSameAxes()
        {
            var x = new AxisUnits(1.0, "px");
            var y = new AxisUnits(1.0, "px");
            var z = new AxisUnits(1.0, "px");
            IScale scale = new Scale(x, y, z);
            Assert.AreSame(x, scale.X);
            Assert.AreSame(y, scale.Y);
            Assert.AreSame(z, scale.Z);
        }

        [TestMethod]
        public void AxisUnits_IAxisUnitsForwarding_MatchesPublicForAllGeneratedInputs()
        {
            var values = Arb.From(Gen.Choose(-1000, 1000).Select(i => i / 10.0));
            var unitLabels = Arb.From(Gen.Elements("nm", "µm", "mm", "px", string.Empty));
            Prop.ForAll(values, unitLabels, (value, units) =>
            {
                IAxisUnits axis = new AxisUnits(value, units);
                var concrete = (AxisUnits)axis;
                return axis.Value == concrete.Value && axis.Units == concrete.Units;
            }).QuickCheckThrowOnFailure();
        }
    }
}
