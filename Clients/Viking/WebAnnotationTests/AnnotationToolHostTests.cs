using System;
using System.IO;
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

        static string RepoUserSettings([CallerFilePath] string thisSourceFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisSourceFile) ?? "", @"..\WebAnnotation\UserSettings.xml"));
    }
}
