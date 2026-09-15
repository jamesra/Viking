using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonogameTestbed
{
    /// <summary>
    /// Screen-space File | Test | View | Help strip drawn over the game. Works with DesktopGL/SDL without a native OS menu.
    /// </summary>
    sealed class TestbedMenuBar
    {
        private const int BaseHeight = 28;
        private const float BaseMenuScale = 0.22f;

        /// <summary>
        /// Current strip height in pixels. Grows with the viewport (see <see cref="MonoTestbed.HudSizeFactorFor"/>) so
        /// the menu is not a hairline when the window is maximised on a high-resolution display.
        /// </summary>
        public int Height { get; private set; } = BaseHeight;

        private float _sizeFactor = 1f;
        private float _textScale = BaseMenuScale;

        enum OpenMenu
        {
            None,
            File,
            Test,
            View,
            ViewSliceStatus
        }

        enum FileItemId
        {
            SaveMesh
        }

        enum ViewItemId
        {
            SliceStatus
        }

        enum SliceStatusItemId
        {
            InProgress,
            SectionReady,
            Minor,
            Warning,
            Critical,
            UntiledLinked
        }

        private readonly MonoTestbed _game;
        private OpenMenu _open = OpenMenu.None;
        private MouseState _prevMouse;
        private bool _mouseSeen;
        private KeyboardState _prevKeyboard;
        private bool _keyboardSeen;

        private Rectangle _fileItemBounds;
        private Rectangle _testItemBounds;
        private Rectangle _viewItemBounds;
        private Rectangle _helpItemBounds;
        private readonly List<(Rectangle Bounds, FileItemId Id)> _fileDropdownItems = [];
        private readonly List<(Rectangle Bounds, TestMode Mode)> _testDropdownItems = [];
        private readonly List<(Rectangle Bounds, ViewItemId Id)> _viewDropdownItems = [];
        private readonly List<(Rectangle Bounds, SliceStatusItemId Id)> _sliceStatusItems = [];

        public TestbedMenuBar(MonoTestbed game)
        {
            _game = game;
        }

        /// <summary>
        /// True when a dropdown is open or the pointer is over the menu strip — callers should not pass
        /// clicks through to the active test.
        /// </summary>
        public bool CapturesInput { get; private set; }

        /// <summary>
        /// Processes clicks and Escape. Call before the active test's Update when the menu is enabled.
        /// </summary>
        public void Update(IReadOnlyDictionary<TestMode, IGraphicsTest> tests, TestMode currentMode)
        {
            MouseState mouse = Mouse.GetState();
            KeyboardState keyboard = Keyboard.GetState();
            bool leftPressed = mouse.LeftButton == ButtonState.Pressed;
            bool leftClicked = leftPressed && (!_mouseSeen || _prevMouse.LeftButton != ButtonState.Pressed);
            bool escapePressed = keyboard.IsKeyDown(Keys.Escape)
                && (!_keyboardSeen || !_prevKeyboard.IsKeyDown(Keys.Escape));
            bool helpHotkey = IsHelpHotkey(keyboard) && (!_keyboardSeen || !IsHelpHotkey(_prevKeyboard));

            int vpWidth = Math.Max(1, _game.GraphicsDevice.Viewport.Width);
            tests.TryGetValue(currentMode, out IGraphicsTest current);
            IViewMenuTarget viewTarget = current as IViewMenuTarget;
            IFileMenuTarget fileTarget = current as IFileMenuTarget;
            Layout(vpWidth, _game.GraphicsDevice.Viewport.Height, tests);

            Point p = new(mouse.X, mouse.Y);
            bool overBar = p.Y >= 0 && p.Y < Height && p.X >= 0 && p.X < vpWidth;
            bool overFileDrop = _open == OpenMenu.File && _fileDropdownItems.Any(i => i.Bounds.Contains(p));
            bool overTestDrop = _open == OpenMenu.Test && _testDropdownItems.Any(i => i.Bounds.Contains(p));
            bool overViewDrop = (_open == OpenMenu.View || _open == OpenMenu.ViewSliceStatus)
                && _viewDropdownItems.Any(i => i.Bounds.Contains(p));
            bool overSliceDrop = _open == OpenMenu.ViewSliceStatus && _sliceStatusItems.Any(i => i.Bounds.Contains(p));
            CapturesInput = overBar || overFileDrop || overTestDrop || overViewDrop || overSliceDrop || _open != OpenMenu.None;

            if (helpHotkey)
            {
                _open = OpenMenu.None;
                _game.ShowHotkeyHelp();
            }
            else if (escapePressed && _open != OpenMenu.None)
            {
                _open = OpenMenu.None;
            }
            else if (leftClicked)
            {
                if (_fileItemBounds.Contains(p))
                {
                    _open = _open == OpenMenu.File ? OpenMenu.None : OpenMenu.File;
                }
                else if (_testItemBounds.Contains(p))
                {
                    _open = _open == OpenMenu.Test ? OpenMenu.None : OpenMenu.Test;
                }
                else if (_viewItemBounds.Contains(p))
                {
                    _open = (_open == OpenMenu.View || _open == OpenMenu.ViewSliceStatus) ? OpenMenu.None : OpenMenu.View;
                }
                else if (_helpItemBounds.Contains(p))
                {
                    _open = OpenMenu.None;
                    _game.ShowHotkeyHelp();
                }
                else if (_open == OpenMenu.File)
                {
                    HandleFileClick(p, fileTarget, overBar);
                }
                else if (_open == OpenMenu.Test)
                {
                    bool hitItem = false;
                    foreach (var item in _testDropdownItems)
                    {
                        if (!item.Bounds.Contains(p))
                            continue;

                        hitItem = true;
                        _open = OpenMenu.None;
                        _game.SwitchToTest(item.Mode);
                        break;
                    }

                    if (!hitItem && !overBar)
                        _open = OpenMenu.None;
                }
                else if (_open == OpenMenu.View || _open == OpenMenu.ViewSliceStatus)
                {
                    HandleViewClick(p, viewTarget, overBar);
                }
                else if (!overBar)
                {
                    _open = OpenMenu.None;
                }
            }
            else if ((_open == OpenMenu.View || _open == OpenMenu.ViewSliceStatus) && viewTarget != null)
            {
                //Hover opens the Slice Status flyout without requiring a click.
                foreach (var item in _viewDropdownItems)
                {
                    if (item.Id == ViewItemId.SliceStatus && item.Bounds.Contains(p))
                    {
                        _open = OpenMenu.ViewSliceStatus;
                        break;
                    }
                }
            }

            _prevMouse = mouse;
            _mouseSeen = true;
            _prevKeyboard = keyboard;
            _keyboardSeen = true;
        }

        private void HandleFileClick(Point p, IFileMenuTarget fileTarget, bool overBar)
        {
            foreach (var item in _fileDropdownItems)
            {
                if (!item.Bounds.Contains(p))
                    continue;

                if (item.Id == FileItemId.SaveMesh)
                {
                    _open = OpenMenu.None;
                    if (fileTarget != null)
                        fileTarget.TrySaveMesh();
                    return;
                }
            }

            if (!overBar)
                _open = OpenMenu.None;
        }

        private void HandleViewClick(Point p, IViewMenuTarget viewTarget, bool overBar)
        {
            if (viewTarget is null)
            {
                if (!overBar)
                    _open = OpenMenu.None;
                return;
            }

            foreach (var item in _sliceStatusItems)
            {
                if (!item.Bounds.Contains(p))
                    continue;

                ToggleSliceStatus(viewTarget, item.Id);
                //Keep the flyout open so multiple categories can be toggled.
                _open = OpenMenu.ViewSliceStatus;
                return;
            }

            foreach (var item in _viewDropdownItems)
            {
                if (!item.Bounds.Contains(p))
                    continue;

                if (item.Id == ViewItemId.SliceStatus)
                {
                    _open = OpenMenu.ViewSliceStatus;
                    return;
                }
            }

            if (!overBar)
                _open = OpenMenu.None;
        }

        private static void ToggleSliceStatus(IViewMenuTarget target, SliceStatusItemId id)
        {
            switch (id)
            {
                case SliceStatusItemId.InProgress:
                    target.ShowInProgressSliceStatus = !target.ShowInProgressSliceStatus;
                    break;
                case SliceStatusItemId.SectionReady:
                    target.ShowSectionReadySliceStatus = !target.ShowSectionReadySliceStatus;
                    break;
                case SliceStatusItemId.Minor:
                    target.ShowMinorIssueSliceStatus = !target.ShowMinorIssueSliceStatus;
                    break;
                case SliceStatusItemId.Warning:
                    target.ShowWarningSliceStatus = !target.ShowWarningSliceStatus;
                    break;
                case SliceStatusItemId.Critical:
                    target.ShowCriticalSliceStatus = !target.ShowCriticalSliceStatus;
                    break;
                case SliceStatusItemId.UntiledLinked:
                    target.ShowUntiledLinkedPairStatus = !target.ShowUntiledLinkedPairStatus;
                    break;
            }
        }

        private static bool IsSliceStatusChecked(IViewMenuTarget target, SliceStatusItemId id) => id switch
        {
            SliceStatusItemId.InProgress => target.ShowInProgressSliceStatus,
            SliceStatusItemId.SectionReady => target.ShowSectionReadySliceStatus,
            SliceStatusItemId.Minor => target.ShowMinorIssueSliceStatus,
            SliceStatusItemId.Warning => target.ShowWarningSliceStatus,
            SliceStatusItemId.Critical => target.ShowCriticalSliceStatus,
            SliceStatusItemId.UntiledLinked => target.ShowUntiledLinkedPairStatus,
            _ => false
        };

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D whitePixel,
            IReadOnlyDictionary<TestMode, IGraphicsTest> tests, TestMode currentMode)
        {
            if (spriteBatch is null || font is null || whitePixel is null)
                return;

            int vpWidth = Math.Max(1, _game.GraphicsDevice.Viewport.Width);
            tests.TryGetValue(currentMode, out IGraphicsTest current);
            IViewMenuTarget viewTarget = current as IViewMenuTarget;
            IFileMenuTarget fileTarget = current as IFileMenuTarget;
            Layout(vpWidth, _game.GraphicsDevice.Viewport.Height, tests);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.Draw(whitePixel, new Rectangle(0, 0, vpWidth, Height), new Color(40, 40, 44));
            DrawMenuLabel(spriteBatch, font, whitePixel, "File", _fileItemBounds, _open == OpenMenu.File);
            DrawMenuLabel(spriteBatch, font, whitePixel, "Test", _testItemBounds, _open == OpenMenu.Test);
            DrawMenuLabel(spriteBatch, font, whitePixel, "View", _viewItemBounds,
                _open == OpenMenu.View || _open == OpenMenu.ViewSliceStatus);
            DrawMenuLabel(spriteBatch, font, whitePixel, "Help", _helpItemBounds, false);

            if (_open == OpenMenu.File)
            {
                foreach (var item in _fileDropdownItems)
                {
                    spriteBatch.Draw(whitePixel, item.Bounds, new Color(48, 48, 52));
                    string text = item.Id switch
                    {
                        FileItemId.SaveMesh when fileTarget is null => "  Save Mesh (N/A)",
                        FileItemId.SaveMesh => "  Save Mesh",
                        _ => item.Id.ToString()
                    };
                    DrawDropdownText(spriteBatch, font, text, item.Bounds);
                }
            }

            if (_open == OpenMenu.Test)
            {
                foreach (var item in _testDropdownItems)
                {
                    bool selected = item.Mode == currentMode;
                    spriteBatch.Draw(whitePixel, item.Bounds, selected ? new Color(60, 90, 140) : new Color(48, 48, 52));
                    string mark = selected ? "> " : "  ";
                    string title = tests.TryGetValue(item.Mode, out IGraphicsTest t) ? t.Title : item.Mode.ToString();
                    string text = $"{mark}{item.Mode} - {title}";
                    DrawDropdownText(spriteBatch, font, text, item.Bounds);
                }
            }

            if (_open == OpenMenu.View || _open == OpenMenu.ViewSliceStatus)
            {
                foreach (var item in _viewDropdownItems)
                {
                    bool highlight = item.Id == ViewItemId.SliceStatus && _open == OpenMenu.ViewSliceStatus;
                    spriteBatch.Draw(whitePixel, item.Bounds, highlight ? new Color(60, 90, 140) : new Color(48, 48, 52));

                    string text = item.Id switch
                    {
                        ViewItemId.SliceStatus when viewTarget is null => "  Slice Status (N/A)",
                        ViewItemId.SliceStatus => "  Slice Status  >",
                        _ => item.Id.ToString()
                    };
                    DrawDropdownText(spriteBatch, font, text, item.Bounds);
                }
            }

            if (_open == OpenMenu.ViewSliceStatus && viewTarget != null)
            {
                foreach (var item in _sliceStatusItems)
                {
                    spriteBatch.Draw(whitePixel, item.Bounds, new Color(48, 48, 52));
                    string label = item.Id switch
                    {
                        SliceStatusItemId.InProgress => "In Progress (gray contours)",
                        SliceStatusItemId.SectionReady => "Section Ready (blue)",
                        SliceStatusItemId.Minor => "Minor Issues (yellow)",
                        SliceStatusItemId.Warning => "Holes / Winding (orange)",
                        SliceStatusItemId.Critical => "Non-manifold (red)",
                        SliceStatusItemId.UntiledLinked => "Untiled linked contours (magenta)",
                        _ => item.Id.ToString()
                    };
                    string text = $"{CheckMark(IsSliceStatusChecked(viewTarget, item.Id))} {label}";
                    DrawDropdownText(spriteBatch, font, text, item.Bounds);
                }
            }

            spriteBatch.End();
        }

        private static string CheckMark(bool on) => on ? "[x]" : "[ ]";

        private int Scaled(int basePixels) => (int)Math.Round(basePixels * _sizeFactor);

        private void Layout(int vpWidth, int vpHeight, IReadOnlyDictionary<TestMode, IGraphicsTest> tests)
        {
            _sizeFactor = MonoTestbed.HudSizeFactorFor(vpHeight);
            _textScale = BaseMenuScale * _sizeFactor;
            Height = Scaled(BaseHeight);

            int itemWidth = Scaled(64);
            int gap = Scaled(4);
            _fileItemBounds = new Rectangle(gap, 0, itemWidth, Height);
            _testItemBounds = new Rectangle(gap + itemWidth + gap, 0, itemWidth, Height);
            _viewItemBounds = new Rectangle(gap + 2 * (itemWidth + gap), 0, itemWidth, Height);
            _helpItemBounds = new Rectangle(gap + 3 * (itemWidth + gap), 0, itemWidth, Height);

            _fileDropdownItems.Clear();
            _testDropdownItems.Clear();
            _viewDropdownItems.Clear();
            _sliceStatusItems.Clear();

            int rowHeight = Scaled(22);
            int dropWidth = Math.Min(Scaled(480), Math.Max(Scaled(280), vpWidth / 2));
            int y = Height;
            foreach (TestMode mode in tests.Keys.OrderBy(m => m.ToString()))
            {
                _testDropdownItems.Add((new Rectangle(_testItemBounds.X, y, dropWidth, rowHeight), mode));
                y += rowHeight;
            }

            _fileDropdownItems.Add((new Rectangle(_fileItemBounds.X, Height, Scaled(180), rowHeight), FileItemId.SaveMesh));

            int viewDropWidth = Scaled(260);
            int viewX = _viewItemBounds.X;
            _viewDropdownItems.Add((new Rectangle(viewX, Height, viewDropWidth, rowHeight), ViewItemId.SliceStatus));

            int sliceX = viewX + viewDropWidth;
            int sliceWidth = Scaled(280);
            int sliceY = Height;
            SliceStatusItemId[] sliceIds =
            [
                SliceStatusItemId.InProgress,
                SliceStatusItemId.SectionReady,
                SliceStatusItemId.Minor,
                SliceStatusItemId.Warning,
                SliceStatusItemId.Critical,
                SliceStatusItemId.UntiledLinked
            ];
            foreach (SliceStatusItemId id in sliceIds)
            {
                _sliceStatusItems.Add((new Rectangle(sliceX, sliceY, sliceWidth, rowHeight), id));
                sliceY += rowHeight;
            }
        }

        private void DrawDropdownText(SpriteBatch spriteBatch, SpriteFont font, string text, Rectangle bounds)
        {
            text = SanitizeForSpriteFont(font, text);
            Vector2 size = font.MeasureString(text) * _textScale;
            float y = bounds.Y + (bounds.Height - size.Y) * 0.5f;
            spriteBatch.DrawString(font, text, new Vector2(bounds.X + Scaled(8), y), Color.White,
                0f, Vector2.Zero, _textScale, SpriteEffects.None, 0f);
        }

        private void DrawMenuLabel(SpriteBatch spriteBatch, SpriteFont font, Texture2D whitePixel,
            string text, Rectangle bounds, bool highlight)
        {
            if (highlight)
                spriteBatch.Draw(whitePixel, bounds, new Color(60, 90, 140));

            text = SanitizeForSpriteFont(font, text);
            Vector2 size = font.MeasureString(text) * _textScale;
            float x = bounds.X + (bounds.Width - size.X) * 0.5f;
            float y = bounds.Y + (bounds.Height - size.Y) * 0.5f;
            spriteBatch.DrawString(font, text, new Vector2(x, y), Color.White,
                0f, Vector2.Zero, _textScale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Bitmap SpriteFonts only contain glyphs listed in the .spritefont file. Menu labels sometimes
        /// pick up en/em dashes or other Unicode from titles; replace missing glyphs so MeasureString
        /// and DrawString do not throw ArgumentException.
        /// </summary>
        private static string SanitizeForSpriteFont(SpriteFont font, string text)
        {
            if (string.IsNullOrEmpty(text) || font is null)
                return text ?? string.Empty;

            bool needsSanitize = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (!font.Characters.Contains(text[i]))
                {
                    needsSanitize = true;
                    break;
                }
            }

            if (!needsSanitize)
                return text;

            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!font.Characters.Contains(chars[i]))
                    chars[i] = '?';
            }

            return new string(chars);
        }

        private static bool IsHelpHotkey(KeyboardState keyboard)
        {
            bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            return keyboard.IsKeyDown(Keys.OemQuestion)
                || keyboard.IsKeyDown(Keys.Divide)
                || (shift && keyboard.IsKeyDown(Keys.F1));
        }
    }
}
