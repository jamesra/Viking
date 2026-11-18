using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VikingXNAGraphics;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="FrameRequests"/>: a frame asks for another only when something drawn in it did, the request does not
    /// leak into the next frame, and every line or curve technique that reads the time counts as animated.
    /// </summary>
    [TestClass]
    public class FrameRequestsTests
    {
        [TestMethod]
        public void FrameWithoutRequests_AsksForNone()
        {
            FrameRequests.BeginFrame();
            Assert.IsFalse(FrameRequests.EndFrame());
            Assert.IsNull(FrameRequests.LastFrameReason);
        }

        [TestMethod]
        public void Request_AsksForOneMoreFrame_AndKeepsTheFirstReason()
        {
            FrameRequests.BeginFrame();
            FrameRequests.Request("shimmer");
            FrameRequests.Request("label easing");
            Assert.IsTrue(FrameRequests.EndFrame());
            Assert.AreEqual("shimmer", FrameRequests.LastFrameReason);

            FrameRequests.BeginFrame();
            Assert.IsFalse(FrameRequests.EndFrame(), "the next frame starts clear: an animation must ask again");
        }

        [TestMethod]
        public void RequestOutsideAFrame_IsDroppedAtTheNextFrame()
        {
            FrameRequests.Request("stray");
            FrameRequests.BeginFrame();
            Assert.IsFalse(FrameRequests.EndFrame());
        }

        [TestMethod]
        public void AnimatedLineStyles_AreAnimatedTechniques()
        {
            Assert.IsTrue(FrameRequests.IsAnimatedTechnique(nameof(LineStyle.AnimatedLinear)));
            Assert.IsTrue(FrameRequests.IsAnimatedTechnique(nameof(LineStyle.AnimatedBidirectional)));
            Assert.IsTrue(FrameRequests.IsAnimatedTechnique(nameof(LineStyle.AnimatedRadial)));

            foreach (LineStyle still in new[] { LineStyle.Standard, LineStyle.AlphaGradient, LineStyle.NoBlur, LineStyle.Ladder,
                                                LineStyle.Dashed, LineStyle.Tubular, LineStyle.HalfTube, LineStyle.Glow, LineStyle.Textured })
            {
                Assert.IsFalse(FrameRequests.IsAnimatedTechnique(still.Name()), still.Name());
            }

            Assert.IsFalse(FrameRequests.IsAnimatedTechnique(null));
        }

        /// <summary>
        /// Reads the line and curve shaders: a technique whose pixel shader is one of the MyPSAnimated* functions moves with
        /// time, so <see cref="FrameRequests.IsAnimatedTechnique"/> must name it, or the line freezes while idle.
        /// </summary>
        [TestMethod]
        public void EveryShaderTechniqueWithAnAnimatedPixelShader_IsListed()
        {
            string content = FindContentFolder();
            string[] files = ["RoundLine.fx", "RoundLineHSV.fx", "RoundCurve.fx", "RoundCurveHSV.fx"];
            int found = 0;
            foreach (string file in files)
            {
                string fx = File.ReadAllText(Path.Combine(content, file));
                foreach (Match technique in Regex.Matches(fx, @"technique\s+(\w+)\s*\{(.*?)\n\}", RegexOptions.Singleline))
                {
                    if (!technique.Groups[2].Value.Contains("MyPSAnimated"))
                        continue;
                    found++;
                    Assert.IsTrue(FrameRequests.IsAnimatedTechnique(technique.Groups[1].Value), $"{file}: {technique.Groups[1].Value}");
                }
            }

            Assert.IsTrue(found >= 12, $"the four shaders each have three animated techniques (found {found})");
        }

        private static string FindContentFolder()
        {
            for (DirectoryInfo? dir = new(AppDomain.CurrentDomain.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Clients", "Monographics", "Content");
                if (File.Exists(Path.Combine(candidate, "RoundLine.fx")))
                    return candidate;
            }

            Assert.Inconclusive("Monographics/Content not found above the test folder");
            return "";
        }
    }
}
