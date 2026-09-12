using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace MonogameTestbed
{
    /// <summary>
    /// Screen-space popup for DesktopGL/SDL (no native ContextMenuStrip). Same drawing style as
    /// <see cref="TestbedMenuBar"/>.
    /// </summary>
    sealed class SliceContextMenu
    {
        const int BaseRowHeight = 22;
        const int BasePadX = 10;
        const float BaseTextScale = 0.22f;

        readonly List<(Rectangle Bounds, string Label, Action Action, bool Enabled)> _items = [];
        Point _anchor;
        bool _mouseSeen;
        MouseState _prevMouse;
        float _sizeFactor = 1f;

        public bool IsOpen => _items.Count > 0;

        public void Open(Point screenAnchor, IReadOnlyList<(string Label, Action Action, bool Enabled)> items)
        {
            Close();
            if (items is null || items.Count == 0)
                return;

            _anchor = screenAnchor;
            foreach (var item in items)
                _items.Add((default, item.Label, item.Action, item.Enabled));
        }

        public void Close()
        {
            _items.Clear();
        }

        /// <summary>
        /// Handles left-click activation and outside-click dismissal. Returns true when the click was consumed.
        /// </summary>
        public bool Update(MouseState mouse, int viewportWidth, int viewportHeight)
        {
            if (!IsOpen)
            {
                _prevMouse = mouse;
                _mouseSeen = true;
                return false;
            }

            Layout(viewportWidth, viewportHeight);

            bool leftPressed = mouse.LeftButton == ButtonState.Pressed;
            bool leftClicked = leftPressed && (!_mouseSeen || _prevMouse.LeftButton != ButtonState.Pressed);
            bool rightPressed = mouse.RightButton == ButtonState.Pressed;
            bool rightClicked = rightPressed && (!_mouseSeen || _prevMouse.RightButton != ButtonState.Pressed);

            Point p = new(mouse.X, mouse.Y);
            bool overMenu = false;
            foreach (var item in _items)
            {
                if (item.Bounds.Contains(p))
                {
                    overMenu = true;
                    break;
                }
            }

            bool consumed = false;
            if (leftClicked)
            {
                if (overMenu)
                {
                    for (int i = 0; i < _items.Count; i++)
                    {
                        var item = _items[i];
                        if (!item.Bounds.Contains(p))
                            continue;
                        if (item.Enabled)
                            item.Action?.Invoke();
                        Close();
                        consumed = true;
                        break;
                    }
                }
                else
                {
                    Close();
                    consumed = true;
                }
            }
            else if (rightClicked && !overMenu)
            {
                Close();
                consumed = true;
            }

            _prevMouse = mouse;
            _mouseSeen = true;
            return consumed;
        }

        public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D whitePixel, int viewportHeight)
        {
            if (!IsOpen || spriteBatch is null || font is null || whitePixel is null)
                return;

            _sizeFactor = MonoTestbed.HudSizeFactorFor(viewportHeight);
            Layout(spriteBatch.GraphicsDevice.Viewport.Width, viewportHeight, font);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Point mouse = Mouse.GetState().Position;
            foreach (var item in _items)
            {
                bool hover = item.Bounds.Contains(mouse);
                Color bg = !item.Enabled
                    ? new Color(36, 36, 40)
                    : hover
                        ? new Color(60, 90, 140)
                        : new Color(48, 48, 52);
                spriteBatch.Draw(whitePixel, item.Bounds, bg);

                string text = SanitizeForSpriteFont(font, item.Label);
                Color fg = item.Enabled ? Color.White : new Color(140, 140, 140);
                float textScale = BaseTextScale * _sizeFactor;
                Vector2 size = font.MeasureString(text) * textScale;
                Vector2 pos = new(
                    item.Bounds.X + Scaled(BasePadX),
                    item.Bounds.Y + (item.Bounds.Height - size.Y) * 0.5f);
                spriteBatch.DrawString(font, text, pos, fg, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        void Layout(int viewportWidth, int viewportHeight, SpriteFont font = null)
        {
            if (_items.Count == 0)
                return;

            _sizeFactor = MonoTestbed.HudSizeFactorFor(viewportHeight);
            int rowHeight = Scaled(BaseRowHeight);
            float textScale = BaseTextScale * _sizeFactor;

            int maxTextWidth = Scaled(160);
            foreach (var item in _items)
            {
                int widthEstimate;
                if (font is not null)
                {
                    string text = SanitizeForSpriteFont(font, item.Label);
                    widthEstimate = (int)Math.Ceiling(font.MeasureString(text).X * textScale) + Scaled(BasePadX) * 2;
                }
                else
                {
                    widthEstimate = (int)Math.Ceiling(item.Label.Length * 7 * _sizeFactor) + Scaled(BasePadX) * 2;
                }

                if (widthEstimate > maxTextWidth)
                    maxTextWidth = widthEstimate;
            }

            int width = Math.Clamp(maxTextWidth, Scaled(160), Math.Max(Scaled(160), viewportWidth - 8));
            int totalHeight = rowHeight * _items.Count;
            int x = Math.Clamp(_anchor.X, 4, Math.Max(4, viewportWidth - width - 4));
            int y = Math.Clamp(_anchor.Y, 4, Math.Max(4, viewportHeight - totalHeight - 4));

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                _items[i] = (new Rectangle(x, y + i * rowHeight, width, rowHeight), item.Label, item.Action, item.Enabled);
            }
        }

        int Scaled(int basePixels) => (int)Math.Round(basePixels * _sizeFactor);

        static string SanitizeForSpriteFont(SpriteFont font, string text)
        {
            if (font is null || string.IsNullOrEmpty(text))
                return text ?? "";

            char[] chars = text.ToCharArray();
            bool changed = false;
            for (int i = 0; i < chars.Length; i++)
            {
                if (font.Characters.Contains(chars[i]))
                    continue;
                chars[i] = '?';
                changed = true;
            }

            return changed ? new string(chars) : text;
        }
    }
}
