using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking.Input;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotation;
using WebAnnotation.Tools;
using WebAnnotation.ViewModel;
using Key = System.Windows.Input.Key;
using MouseButton = System.Windows.Input.MouseButton;

namespace WebAnnotationTests
{
    /// <summary>
    /// Jotunn's tool host without a view: a viewport whose screen and world coordinates are the same and a scene over an
    /// empty volume, so nothing is hit and nothing is loaded.
    /// </summary>
    [TestClass]
    public class AnnotationToolHostTests
    {
        sealed class IdentityViewport : IViewportHost
        {
            public int SectionNumber => 1;
            public Geometry.Rectangle VisibleWorldBounds => new(new Geometry.Vector2(0, 0), 100, 100);
            public double Downsample => 1;
            public int ViewportWidth => 100;
            public int ViewportHeight => 100;
            public ModifierKeys CurrentModifiers => ModifierKeys.None;
            public Geometry.Vector2 ScreenToWorld(Geometry.Vector2 screen) => screen;
            public Geometry.Vector2 WorldToScreen(Geometry.Vector2 world) => world;
            public void Invalidate() { }
            public void CapturePointer() { }
            public void ReleasePointer() { }
            public event EventHandler ViewportChanged { add { } remove { } }
        }

        /// <summary>A tool that runs an action on a click and then finishes.</summary>
        sealed class ScriptedTool(Action onClick) : IAnnotationTool, IAnnotationToolPreview
        {
            public bool Cancelled { get; private set; }
            public string StatusText => "scripted";
            public AnnotationToolResult OnMouseDown(Geometry.Vector2 screen, MouseButton button, int clickCount)
            {
                onClick?.Invoke();
                return AnnotationToolResult.Done;
            }
            public void OnMouseMove(Geometry.Vector2 screen) { }
            public void OnMouseUp() { }
            public AnnotationToolResult OnKeyDown(Key key) => AnnotationToolResult.Continue;
            public void Cancel() => Cancelled = true;
            public void DrawPreview(GraphicsDevice device, VikingXNA.Scene scene, RoundLineCode.RoundLineManager lineManager, OverlayShaderEffect overlayEffect) { }
        }

        Func<int, SectionAnnotationsView> _savedLookup;
        AnnotationScene _scene;
        AnnotationToolHost _host;
        string _status;

        [TestInitialize]
        public void Setup()
        {
            _savedLookup = AnnotationOverlay.SectionViewLookup;
            Volume volume = (Volume)FormatterServices.GetUninitializedObject(typeof(Volume));
            _scene = new AnnotationScene(volume);
            _host = new AnnotationToolHost(new IdentityViewport(), _scene);
            _host.StatusChanged += (_, text) => _status = text;
        }

        [TestCleanup]
        public void Cleanup() => AnnotationOverlay.SectionViewLookup = _savedLookup;

        [TestMethod]
        public void AToolStartedDuringAnotherToolsClickStartsAfterThatToolFinishes()
        {
            ScriptedTool next = new(null);
            ScriptedTool first = new(() => _host.StartTool(next));
            _host.StartTool(first);

            _host.OnMouseDown(new Geometry.Vector2(1, 1), MouseButton.Left, 1);

            Assert.IsFalse(_host.IsIdle, "the tool started from the click must survive the first tool finishing");
            Assert.AreSame(next, _scene.ToolPreview);
            Assert.IsFalse(next.Cancelled);
        }

        [TestMethod]
        public void AFinishedToolReturnsToTheDefaultTool()
        {
            _host.StartTool(new ScriptedTool(null));

            _host.OnMouseDown(new Geometry.Vector2(1, 1), MouseButton.Left, 1);

            Assert.IsTrue(_host.IsIdle);
        }

        [TestMethod]
        public void AStructureShortcutArmsUntilEscape()
        {
            string settings = RepoUserSettings();
            if (!File.Exists(settings))
                Assert.Inconclusive($"{settings} is not beside this checkout's sources");
            _host.Hotkeys = UserSettingsHotkeys.Load(settings);

            Assert.IsTrue(_host.TryRunHotkey(Key.C, shift: false, ctrl: false, alt: false));
            StringAssert.StartsWith(_status, "Annotate Cell with Circle");

            Assert.IsTrue(_host.OnKeyDown(Key.Escape), "Escape disarms the shortcut");
            Assert.IsFalse(_host.OnKeyDown(Key.Escape), "nothing is armed any more");
        }

        [TestMethod]
        public void UnboundKeysAreNotHotkeys()
        {
            string settings = RepoUserSettings();
            if (!File.Exists(settings))
                Assert.Inconclusive($"{settings} is not beside this checkout's sources");
            _host.Hotkeys = UserSettingsHotkeys.Load(settings);

            Assert.IsFalse(_host.TryRunHotkey(Key.F5, shift: false, ctrl: false, alt: false));
        }

        [TestMethod]
        public void MeasuringReportsTheDistanceBetweenTwoClicks()
        {
            _host.StartMeasure();
            _host.OnMouseDown(new Geometry.Vector2(0, 0), MouseButton.Left, 1);
            _host.OnMouseDown(new Geometry.Vector2(3, 4), MouseButton.Left, 1);

            StringAssert.StartsWith(_status, "Distance: ");
            Assert.IsTrue(_host.IsIdle);
        }

        [TestMethod]
        public void UndoWithNothingRecordedSaysSo()
        {
            while (UndoManager.CanUndo)
                UndoManager.UndoLast();

            Assert.IsFalse(_host.Undo());
            Assert.AreEqual("Nothing to undo", _status);
        }

        [TestMethod]
        public void ConvertingACircleToAPolygonTracesItsOutline()
        {
            var circle = WebAnnotationTests.Actions.ChangeTypeActionTests.NewLocation(
                Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE, "CURVEPOLYGON(CIRCULARSTRING(40 50, 50 40, 60 50, 50 60, 40 50))");

            _host.ConvertCircleToPolygon(circle);

            Assert.AreEqual(Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON, circle.TypeCode);
            Assert.IsInstanceOfType(circle.MosaicShape, typeof(Geometry.IPolygon2D));
        }

        [TestMethod]
        public void ConvertingAPolygonToACircleKeepsItsCenter()
        {
            var square = WebAnnotationTests.Actions.ChangeTypeActionTests.NewLocation(
                Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON, "POLYGON((10 10, 90 10, 90 90, 10 90, 10 10))");
            Geometry.Vector2 center = square.Position;

            _host.ConvertToCircle(square);

            Assert.AreEqual(Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE, square.TypeCode);
            Assert.IsInstanceOfType(square.MosaicShape, typeof(Geometry.ICircle2D));
            Assert.AreEqual(center.X, square.Position.X, 1e-6);
            Assert.AreEqual(center.Y, square.Position.Y, 1e-6);
        }

        [TestMethod]
        public void SimplifyingDropsVerticesThatAddNothing()
        {
            // A densely traced ring, as a pen stroke or segmentation leaves: 64 vertices on a circle of radius 40.
            var ring = System.Linq.Enumerable.Range(0, 65).Select(i =>
            {
                double angle = 2 * System.Math.PI * (i % 64) / 64;
                return $"{100 + 40 * System.Math.Cos(angle):F3} {100 + 40 * System.Math.Sin(angle):F3}";
            });
            var polygon = WebAnnotationTests.Actions.ChangeTypeActionTests.NewLocation(
                Viking.AnnotationServiceTypes.Interfaces.LocationType.POLYGON, $"POLYGON(({string.Join(", ", ring)}))");

            _host.SimplifyPolygon(polygon);

            Assert.IsTrue(SqlGeometryUtils.Extensions.ToPolygon(polygon.MosaicShape).ExteriorRing.Length < 65, _status);
        }

        [TestMethod]
        public void ChangingTypeReplacesTheShapeWithTheClickedPoints()
        {
            var circle = WebAnnotationTests.Actions.ChangeTypeActionTests.NewLocation(
                Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE, "CURVEPOLYGON(CIRCULARSTRING(40 50, 50 40, 60 50, 50 60, 40 50))");
            AnnotationToolContext context = new(new IdentityViewport(), _scene, () => null, _ => { }, _ => { }, _ => { });

            _ = ShapeActions.ReplaceWithPointsAsync(context, circle, Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE,
                [new Geometry.Vector2(10, 10), new Geometry.Vector2(50, 50), new Geometry.Vector2(90, 10)]);

            Assert.AreEqual(Viking.AnnotationServiceTypes.Interfaces.LocationType.OPENCURVE, circle.TypeCode);
            Assert.AreEqual(8.0, circle.Width);
            Assert.AreEqual(3, SqlGeometryUtils.Extensions.ToPoints(circle.MosaicShape).Length);
        }

        [TestMethod]
        public void PenModeTogglesAndSaysSo()
        {
            bool saved = WebAnnotation.Global.PenMode;
            try
            {
                WebAnnotation.Global.PenMode = false;
                _host.TogglePenMode();
                Assert.IsTrue(WebAnnotation.Global.PenMode);
                StringAssert.StartsWith(_status, "Pen mode on");
                _host.TogglePenMode();
                Assert.IsFalse(WebAnnotation.Global.PenMode);
            }
            finally
            {
                WebAnnotation.Global.PenMode = saved;
            }
        }

        [TestMethod]
        public void PenChoicesNameWhatTheyDo()
        {
            var transform = WebAnnotationTests.Actions.ChangeTypeActionTests.CreateBoundedTransform();
            var closed = new WebAnnotation.UI.Actions.Create2DStructureAction(5,
                new Geometry.Polygon([new(10, 10), new(90, 10), new(50, 90), new(10, 10)]), 1, transform);

            StringAssert.StartsWith(PenStrokeTool.Describe(closed), "Create ");
            StringAssert.EndsWith(PenStrokeTool.Describe(closed), "(closed shape)");
        }

        static string RepoUserSettings([CallerFilePath] string thisSourceFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisSourceFile) ?? "", @"..\WebAnnotation\UserSettings.xml"));
    }
}
