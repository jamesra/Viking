using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MonogameTestbed
{
    /// <summary>
    /// Screen-space Test | Help strip drawn over the game. Works with DesktopGL/SDL without a native OS menu.
    /// </summary>
    sealed class TestbedMenuBar
    {
        public const int Height = 28;

        enum OpenMenu
        {
            None,
            Test
        }

        private readonly MonoTestbed _game;
        private OpenMenu _open = OpenMenu.None;
        private MouseState _prevMouse;
        private bool _mouseSeen;
        private KeyboardState _prevKeyboard;
        private bool _keyboardSeen;

        private Rectangle _testItemBounds;
        private Rectangle _helpItemBounds;
        private readonly List<(Rectangle Bounds, TestMode Mode)> _testDropdownItems = [];

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
            Layout(vpWidth, tests);

            Point p = new(mouse.X, mouse.Y);
            bool overBar = p.Y >= 0 && p.Y < Height && p.X >= 0 && p.X < vpWidth;
            bool overDropdown = _open == OpenMenu.Test && _testDropdownItems.Any(i => i.Bounds.Contains(p));
            CapturesInput = overBar || overDropdown || _open != OpenMenu.None;

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
                if (_testItemBounds.Contains(p))
                {
                    _open = _open == OpenMenu.Test ? OpenMenu.None : OpenMenu.Test;
                }
                else if (_helpItemBounds.Contains(p))
                {
                    _open = OpenMenu.None;
                    _game.ShowHotkeyHelp();
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
                else if (!overBar)
                {
                    _open = OpenMenu.None;
                }
            }

            _prevMouse = mouse;
            _mouseSeen = true;
            _prevKeyboard = keyboard;
            _keyboardSeen = true;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D whitePixel,
            IReadOnlyDictionary<TestMode, IGraphicsTest> tests, TestMode currentMode)
        {
            if (spriteBatch is null || font is null || whitePixel is null)
                return;

            int vpWidth = Math.Max(1, _game.GraphicsDevice.Viewport.Width);
            Layout(vpWidth, tests);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.Draw(whitePixel, new Rectangle(0, 0, vpWidth, Height), new Color(40, 40, 44));
            DrawMenuLabel(spriteBatch, font, whitePixel, "Test", _testItemBounds, _open == OpenMenu.Test);
            DrawMenuLabel(spriteBatch, font, whitePixel, "Help", _helpItemBounds, false);

            if (_open == OpenMenu.Test)
            {
                foreach (var item in _testDropdownItems)
                {
                    bool selected = item.Mode == currentMode;
                    spriteBatch.Draw(whitePixel, item.Bounds, selected ? new Color(60, 90, 140) : new Color(48, 48, 52));
                    string mark = selected ? "> " : "  ";
                    string title = tests.TryGetValue(item.Mode, out IGraphicsTest t) ? t.Title : item.Mode.ToString();
                    string text = $"{mark}{item.Mode} — {title}";
                    Vector2 size = font.MeasureString(text) * MenuScale;
                    float y = item.Bounds.Y + (item.Bounds.Height - size.Y) * 0.5f;
                    spriteBatch.DrawString(font, text, new Vector2(item.Bounds.X + 8, y), Color.White,
                        0f, Vector2.Zero, MenuScale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.End();
        }

        private const float MenuScale = 0.22f;

        private void Layout(int vpWidth, IReadOnlyDictionary<TestMode, IGraphicsTest> tests)
        {
            const int itemWidth = 64;
            _testItemBounds = new Rectangle(4, 0, itemWidth, Height);
            _helpItemBounds = new Rectangle(4 + itemWidth + 4, 0, itemWidth, Height);

            _testDropdownItems.Clear();
            int rowHeight = 22;
            int dropWidth = Math.Min(480, Math.Max(280, vpWidth / 2));
            int y = Height;
            foreach (TestMode mode in tests.Keys.OrderBy(m => m.ToString()))
            {
                _testDropdownItems.Add((new Rectangle(4, y, dropWidth, rowHeight), mode));
                y += rowHeight;
            }
        }

        private static void DrawMenuLabel(SpriteBatch spriteBatch, SpriteFont font, Texture2D whitePixel,
            string text, Rectangle bounds, bool highlight)
        {
            if (highlight)
                spriteBatch.Draw(whitePixel, bounds, new Color(60, 90, 140));

            Vector2 size = font.MeasureString(text) * MenuScale;
            float x = bounds.X + (bounds.Width - size.X) * 0.5f;
            float y = bounds.Y + (bounds.Height - size.Y) * 0.5f;
            spriteBatch.DrawString(font, text, new Vector2(x, y), Color.White,
                0f, Vector2.Zero, MenuScale, SpriteEffects.None, 0f);
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
