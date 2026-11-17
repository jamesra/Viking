using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotation.UI.Commands;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="ProposalButtonAtlas"/>: one texture holds every look of the proposal buttons, each in its own cell, with
    /// the shape, outline and symbol where the view expects them and a full mip chain.
    /// </summary>
    [TestClass]
    public class ProposalButtonAtlasTests
    {
        private static List<Color[]> levels;

        [ClassInitialize]
        public static void BuildOnce(TestContext _) => levels = ProposalButtonAtlas.BuildMipLevels();

        /// <summary>The level-0 texel at (<paramref name="x"/>, <paramref name="y"/>) button radii from a look's centre, y up.</summary>
        private static Color At(ProposalButtonKind kind, ProposalButtonPhase phase, bool hovered, double x, double y)
        {
            (int column, int row) = ProposalButtonAtlas.CellOf(kind, phase, hovered);
            double half = ProposalButtonAtlas.CellPixels / 2.0;
            int px = (int)(column * ProposalButtonAtlas.CellPixels + half + x * ProposalButtonAtlas.ButtonRadiusPixels);
            int py = (int)(row * ProposalButtonAtlas.CellPixels + half - y * ProposalButtonAtlas.ButtonRadiusPixels);
            return levels[0][py * ProposalButtonAtlas.Width + px];
        }

        private static void AssertColor(Color expected, Color actual, string message)
        {
            Assert.AreEqual(255, actual.A, $"{message}: opaque");
            Assert.IsTrue(System.Math.Abs(expected.R - actual.R) <= 2 && System.Math.Abs(expected.G - actual.G) <= 2 && System.Math.Abs(expected.B - actual.B) <= 2,
                $"{message}: expected {expected}, got {actual}");
        }

        [TestMethod]
        public void EveryLookHasItsOwnCell()
        {
            HashSet<(int, int)> cells = [];
            foreach (ProposalButtonKind kind in new[] { ProposalButtonKind.Accept, ProposalButtonKind.Hide })
                foreach (ProposalButtonPhase phase in new[] { ProposalButtonPhase.Idle, ProposalButtonPhase.Pressed, ProposalButtonPhase.Waiting, ProposalButtonPhase.Armed })
                    foreach (bool hovered in new[] { false, true })
                    {
                        (int column, int row) = ProposalButtonAtlas.CellOf(kind, phase, hovered);
                        Assert.IsTrue(column < ProposalButtonAtlas.Columns && row < ProposalButtonAtlas.Rows);
                        Assert.IsTrue(cells.Add((column, row)), $"{kind} {phase} hovered={hovered} shares a cell");
                    }
        }

        [TestMethod]
        public void MipChainRunsDownToOneTexel()
        {
            Assert.AreEqual(ProposalButtonAtlas.Width * ProposalButtonAtlas.Height, levels[0].Length);
            Assert.AreEqual(10, levels.Count, "512, 256, ... 1");
            Assert.AreEqual(1, levels[levels.Count - 1].Length);
            Assert.IsTrue(levels[levels.Count - 1][0].A > 0, "the buttons still show in the smallest level");
        }

        [TestMethod]
        public void FillOutlineAndMargin()
        {
            AssertColor(ProposalButtonAtlas.AcceptColor, At(ProposalButtonKind.Accept, ProposalButtonPhase.Idle, false, 0, 0.6), "accept fill");
            AssertColor(ProposalButtonAtlas.HideColor, At(ProposalButtonKind.Hide, ProposalButtonPhase.Idle, false, 0, 0.6), "hide fill");
            AssertColor(Color.Black, At(ProposalButtonKind.Accept, ProposalButtonPhase.Idle, false, 0, 0.95), "outline");
            AssertColor(Color.Black, At(ProposalButtonKind.Hide, ProposalButtonPhase.Idle, false, 0, 0.84), "square outline");
            AssertColor(Color.White, At(ProposalButtonKind.Accept, ProposalButtonPhase.Idle, true, 0, 0.95), "hovered outline");
            Assert.AreEqual(0, At(ProposalButtonKind.Accept, ProposalButtonPhase.Idle, false, 1.1, 1.1).A, "the margin is clear");
            Assert.AreEqual(0, At(ProposalButtonKind.Accept, ProposalButtonPhase.Idle, false, 0.8, 0.8).A, "outside the circle");
            Assert.AreEqual(255, At(ProposalButtonKind.Hide, ProposalButtonPhase.Idle, false, 0.7, 0.7).A, "the square reaches its corners");
        }

        [TestMethod]
        public void SymbolsShowOnlyWhenIdleOrArmed()
        {
            AssertColor(Color.White, At(ProposalButtonKind.Accept, ProposalButtonPhase.Idle, false, -0.13, -0.33), "check");
            AssertColor(Color.Black, At(ProposalButtonKind.Hide, ProposalButtonPhase.Idle, false, 0.3, 0.3), "X");
            AssertColor(ProposalButtonAtlas.AcceptColor, At(ProposalButtonKind.Accept, ProposalButtonPhase.Waiting, false, -0.13, -0.33), "no check while waiting");

            Color armedFill = ProposalButtonAtlas.FillColor(ProposalButtonKind.Hide, ProposalButtonPhase.Armed, false);
            AssertColor(PenButtonStyle.LabelColorFor(armedFill), At(ProposalButtonKind.Hide, ProposalButtonPhase.Armed, false, 0, -0.42), "the dot of the ?");
        }

        [TestMethod]
        public void PressedIsDarkerAndSmaller()
        {
            Color pressed = ProposalButtonAtlas.FillColor(ProposalButtonKind.Accept, ProposalButtonPhase.Pressed, false);
            AssertColor(pressed, At(ProposalButtonKind.Accept, ProposalButtonPhase.Pressed, false, -0.13, -0.33), "darker, no check");
            Assert.IsTrue(pressed.G < ProposalButtonAtlas.AcceptColor.G);
            Assert.AreEqual(0, At(ProposalButtonKind.Accept, ProposalButtonPhase.Pressed, false, 0, 0.97).A, "shrunk inside the idle edge");
        }

        [TestMethod]
        public void QuadCoversTheMargin()
        {
            Assert.AreEqual(10 * 64.0 / 56.0, ProposalButtonAtlas.QuadHalfSize(10), 1e-9);
        }
    }
}
