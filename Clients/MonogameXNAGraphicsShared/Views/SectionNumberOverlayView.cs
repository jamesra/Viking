using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VikingXNA;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Specifies which edge of the screen the section number overlay should be displayed on
    /// </summary>
    public enum OverlayEdge
    {
        Left,
        Right
    }

    /// <summary>
    /// A visual overlay that displays a vertical scrolling list of section numbers.
    /// The overlay animates smoothly when the current section changes, using a PID-like controller.
    /// </summary>
    public class SectionNumberOverlayView
    {
        #region Configuration Constants

        /// <summary>
        /// Bar width as a percentage of client area width (5%)
        /// </summary>
        private const double BarWidthPercent = 0.05;

        /// <summary>
        /// Begin fade-out when label width exceeds this percentage of client width
        /// </summary>
        private const double FadeBeginPercent = 0.10;

        /// <summary>
        /// Fully invisible when label width exceeds this percentage of client width
        /// </summary>
        private const double FadeEndPercent = 0.15;

        /// <summary>
        /// Number of sections to cache beyond visible range (above and below)
        /// </summary>
        private const int CacheBuffer = 10;

        #endregion

        #region PID Configuration (configurable via preferences)
        
        /// <summary>
        /// Proportional gain for PID controller
        /// </summary>
        private double _pidProportionalGain = 4.0;
        public double PidProportionalGain
        {
            get => _pidProportionalGain;
            set
            {
                _pidProportionalGain = value;
                _pidNeedsUpdate = true;
            }
        }
        
        /// <summary>
        /// Derivative gain for PID controller
        /// </summary>
        private double _pidDerivativeGain = 1.0;
        public double PidDerivativeGain
        {
            get => _pidDerivativeGain;
            set
            {
                _pidDerivativeGain = value;
                _pidNeedsUpdate = true;
            }
        }
        
        /// <summary>
        /// Integral gain for PID controller
        /// </summary>
        private double _pidIntegralGain = 0.01;
        public double PidIntegralGain
        {
            get => _pidIntegralGain;
            set
            {
                _pidIntegralGain = value;
                _pidNeedsUpdate = true;
            }
        }
        
        /// <summary>
        /// Velocity threshold in screen-height units (for completion check)
        /// </summary>
        private double _pidVelocityThresholdScreenHeights = 0.001;
        public double PidVelocityThresholdScreenHeights
        {
            get => _pidVelocityThresholdScreenHeights;
            set
            {
                _pidVelocityThresholdScreenHeights = value;
                _pidNeedsUpdate = true;
            }
        }
        
        /// <summary>
        /// Position threshold in screen-height units (for completion check)
        /// </summary>
        private double _pidPositionThresholdScreenHeights = 0.01;
        public double PidPositionThresholdScreenHeights
        {
            get => _pidPositionThresholdScreenHeights;
            set
            {
                _pidPositionThresholdScreenHeights = value;
                _pidNeedsUpdate = true;
            }
        }
        
        /// <summary>
        /// Flag to track if PID parameters need to be applied
        /// </summary>
        private bool _pidNeedsUpdate = true;

        #endregion

        #region Properties

        /// <summary>
        /// Number of sections to display above and below the current section (default: 4)
        /// </summary>
        private int _sectionsAboveBelow = 4;
        public int SectionsAboveBelow
        {
            get => _sectionsAboveBelow;
            set
            {
                _sectionsAboveBelow = value;
                UpdateAccelerationLimit();
                // PID target is in pixels; recompute when totalSlots changes
                if (_lastScreenHeight > 0)
                {
                    int totalSlots = 2 * _sectionsAboveBelow + 1;
                    double positionInScreenHeights = (double)CurrentSectionNumber / totalSlots;
                    double positionInPixels = positionInScreenHeights * _lastScreenHeight;
                    AnimationController.SetTarget(positionInPixels);
                }
            }
        }

        /// <summary>
        /// The current section number (Z coordinate)
        /// </summary>
        public int CurrentSectionNumber { get; private set; }

        /// <summary>
        /// Minimum section number in the volume
        /// </summary>
        public int MinSectionNumber { get; set; } = 0;

        /// <summary>
        /// Maximum section number in the volume
        /// </summary>
        public int MaxSectionNumber { get; set; } = 100;
            
        /// <summary>
        /// Which edge to display the overlay on
        /// </summary>
        public OverlayEdge Edge { get; set; } = OverlayEdge.Left;

        /// <summary>
        /// Whether the overlay is enabled and should be rendered
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Function to check if a section number exists in the volume
        /// </summary>
        public Func<int, bool> SectionExistsFunc { get; set; }

        /// <summary>
        /// Function to check if the volume has slice-to-slice transforms
        /// (only show red for missing sections if transforms exist)
        /// </summary>
        public Func<bool> HasTransformsFunc { get; set; }

        /// <summary>
        /// The PID controller for smooth animation
        /// </summary>
        public PIDController AnimationController { get; private set; }

        /// <summary>
        /// Acceleration limit in screen-height units (resolution-independent).
        /// 1.0 = one screen height per second squared.
        /// This is converted internally to section units based on SectionsAboveBelow.
        /// </summary>
        private double _accelerationInScreenHeights = 0.1;
        public double AccelerationInScreenHeights
        {
            get => _accelerationInScreenHeights;
            set
            {
                if(_accelerationInScreenHeights != value)
                { 
                    _accelerationInScreenHeights = value;
                    _accelerationNeedsUpdate = true;
                    UpdateAccelerationLimit();
                }
            }
        }

        /// <summary>
        /// Color for normal (existing) sections
        /// </summary>
        public Color NormalColor { get; set; } = Color.White;

        /// <summary>
        /// Color for missing sections (within range, only if transforms exist)
        /// </summary>
        public Color MissingColor { get; set; } = Color.Red;

        /// <summary>
        /// Magnification factor for the section number at the center of the view (default: 3).
        /// Numbers at the edge use 1x; the center number is drawn at this scale.
        /// </summary>
        private double _centerMagnification = 3.0;
        public double CenterMagnification
        {
            get => _centerMagnification;
            set => _centerMagnification = value < 1.0 ? 1.0 : (value > 4.0 ? 4.0 : value);
        }

        /// <summary>
        /// Opacity for all section numbers (0.0 = invisible, 1.0 = fully opaque)
        /// </summary>
        private double _opacity = 0.7;
        public double Opacity
        {
            get => _opacity;
            set => _opacity = Clamp(value, 0.0, 1.0);
        }

        /// <summary>
        /// Minimum opacity for non-center section numbers (0.0 to 0.8). Default 0.5.
        /// Edge numbers never go below this so they remain readable.
        /// </summary>
        private double _minOpacityForNonCenterSections = 0.5;
        public double MinOpacityForNonCenterSections
        {
            get => _minOpacityForNonCenterSections;
            set => _minOpacityForNonCenterSections = Clamp(value, 0.0, 0.8);
        }

        #endregion

        #region Private Fields

        private readonly Dictionary<int, LabelView> _labelCache = new();
        private double _lastScreenHeight = 0;
        private double _lastScreenWidth = 0;
        private double _currentFontSize = 16.0;
        private int _lastCacheCenterSection = int.MinValue;
        
        /// <summary>
        /// Current slot height in pixels (screen height / total slots)
        /// Used to convert between section numbers and pixel positions
        /// </summary>
        private double _slotHeight = 100.0;
        
        /// <summary>
        /// Flag to track if acceleration limit needs to be applied once screen height is known
        /// </summary>
        private bool _accelerationNeedsUpdate = true;

        /// <summary>
        /// Cancellation for the current animation task. Cancelled when a new target is set or on Initialize.
        /// </summary>
        private CancellationTokenSource _animationCts;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new SectionNumberOverlayView
        /// </summary>
        public SectionNumberOverlayView()
        {
            // PID controller works in screen-height units for resolution independence
            // Position is section / totalSlots, velocity is screen-heights/s, acceleration is screen-heights/s²
            AnimationController = new PIDController(0.0);
            // Set initial PID configuration
            ConfigurePid();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the current section number, triggering animation if changed
        /// </summary>
        /// <param name="sectionNumber">The new current section number</param>
        public void SetCurrentSection(int sectionNumber)
        {
            if (CurrentSectionNumber == sectionNumber)
                return;

            CurrentSectionNumber = sectionNumber;
            // Convert from screen-height units to pixels
            // Position in screen-heights = sectionNumber / totalSlots
            // Position in pixels = (sectionNumber / totalSlots) * screenHeight
            int totalSlots = 2 * _sectionsAboveBelow + 1;
            double positionInScreenHeights = (double)sectionNumber / totalSlots;
            // If screen height not known yet, use screen-height units (will be converted later)
            double positionInPixels = _lastScreenHeight > 0 
                ? positionInScreenHeights * _lastScreenHeight 
                : positionInScreenHeights;

            // Cancel previous animation task so only one runs at a time
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _animationCts = new CancellationTokenSource();
            CancellationToken token = _animationCts.Token;

            AnimationController.SetTarget(positionInPixels);
            UpdateLabelCache();

            // Start background task that updates PID at 20 Hz until at target
            Task.Run(() => RunAnimationLoopAsync(token));
        }

        /// <summary>
        /// Initialize or reset the overlay to a specific section without animation
        /// </summary>
        /// <param name="sectionNumber">The section number to display</param>
        public void Initialize(int sectionNumber)
        {
            CurrentSectionNumber = sectionNumber;
            // Convert from screen-height units to pixels
            // Position in screen-heights = sectionNumber / totalSlots
            // Position in pixels = (sectionNumber / totalSlots) * screenHeight
            int totalSlots = 2 * _sectionsAboveBelow + 1;
            double positionInScreenHeights = (double)sectionNumber / totalSlots;
            // If screen height not known yet, use screen-height units (will be converted later)
            double positionInPixels = _lastScreenHeight > 0 
                ? positionInScreenHeights * _lastScreenHeight 
                : positionInScreenHeights;

            // Cancel any running animation task; no new task for snap
            _animationCts?.Cancel();
            _animationCts?.Dispose();
            _animationCts = null;

            AnimationController.SnapTo(positionInPixels);
            _labelCache.Clear();
            _lastCacheCenterSection = int.MinValue;
            UpdateLabelCache();
        }

        /// <summary>
        /// No-op. Animation is driven by an internal background task at 20 Hz until the PID reaches target.
        /// Kept for API compatibility; callers (e.g. draw) should not call this.
        /// </summary>
        /// <param name="elapsedSeconds">Ignored.</param>
        public void Update(double elapsedSeconds)
        {
            // Animation runs on background task; do not update PID on draw thread.
        }

        /// <summary>
        /// Background loop: update PID at ~20 Hz (50 ms target period) until at target or cancelled.
        /// Uses actual elapsed time per iteration for accurate animation. Sleep time is adjusted
        /// so that if the last loop took longer than 50 ms we sleep less (catch up), and if it
        /// took shorter we sleep longer, keeping the average period at 50 ms.
        /// </summary>
        private async Task RunAnimationLoopAsync(CancellationToken token)
        {
            const int targetPeriodMs = 50;
            long lastTicks = Stopwatch.GetTimestamp();

            while (true)
            {
                long nowTicks = Stopwatch.GetTimestamp();
                double elapsedSeconds = (double)(nowTicks - lastTicks) / Stopwatch.Frequency;
                lastTicks = nowTicks;

                // First iteration or negligible elapsed: use nominal step so Update runs
                if (elapsedSeconds <= 0)
                    elapsedSeconds = 0.05;

                AnimationController.Update(elapsedSeconds);
                if (AnimationController.IsComplete())
                    break;

                // Adjust delay: if last loop took longer than target, sleep less; if shorter, sleep more
                double lastLoopMs = elapsedSeconds * 1000.0;
                int delayMs = (int)(targetPeriodMs * 2 - lastLoopMs);
                if (delayMs < 0)
                    delayMs = 0;

                try
                {
                    await Task.Delay(delayMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Draw the section number overlay
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch for rendering</param>
        /// <param name="font">Font to use for labels</param>
        /// <param name="screenWidth">Current screen width in pixels</param>
        /// <param name="screenHeight">Current screen height in pixels</param>
        public void Draw(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight)
        {
            if (!Enabled || spriteBatch == null || font == null)
                return;

            // Check if screen size changed and update labels if needed
            bool screenSizeChanged = Math.Abs(_lastScreenWidth - screenWidth) > 1 || Math.Abs(_lastScreenHeight - screenHeight) > 1;
            if (screenSizeChanged)
            {
                double oldScreenHeight = _lastScreenHeight;
                _lastScreenWidth = screenWidth;
                _lastScreenHeight = screenHeight;
                
                // Update slot height (pixels per section slot) - used for rendering
                int totalSlots = 2 * _sectionsAboveBelow + 1;
                _slotHeight = screenHeight / (double)totalSlots;
                
                // Scale PID positions from old pixel scale to new pixel scale
                if (oldScreenHeight > 0 && Math.Abs(oldScreenHeight - screenHeight) > 1)
                {
                    double scaleFactor = screenHeight / oldScreenHeight;
                    AnimationController.ScalePositions(scaleFactor);
                }
                else if (oldScreenHeight <= 0 && screenHeight > 0)
                {
                    // Screen height just became known - update target position to pixels
                    // Recalculate target from current section number
                    double positionInScreenHeights = (double)CurrentSectionNumber / totalSlots;
                    double positionInPixels = positionInScreenHeights * screenHeight;
                    AnimationController.SetTarget(positionInPixels);
                }
                
                RecalculateFontSize(screenHeight);
                UpdateAllLabelFontSizes();
            }
            
            // Apply PID configuration if parameters changed or screen size changed
            if (_accelerationNeedsUpdate || _pidNeedsUpdate || screenSizeChanged)
            {
                ConfigurePid();
            }

            // Update label cache if needed
            UpdateLabelCache();

            // Calculate fade alpha based on label width vs screen width
            float alpha = CalculateFadeAlpha(font, screenWidth);
            if (alpha <= 0)
                return;

            // Calculate positions and draw labels
            DrawLabels(spriteBatch, font, screenWidth, screenHeight, alpha);
        }

        /// <summary>
        /// Notify the overlay that the viewport size has changed
        /// </summary>
        /// <param name="screenWidth">New screen width</param>
        /// <param name="screenHeight">New screen height</param>
        public void OnViewportChanged(int screenWidth, int screenHeight)
        {
            if (Math.Abs(_lastScreenWidth - screenWidth) > 1 || Math.Abs(_lastScreenHeight - screenHeight) > 1)
            {
                double oldScreenHeight = _lastScreenHeight;
                _lastScreenWidth = screenWidth;
                _lastScreenHeight = screenHeight;
                
                // Update slot height (pixels per section slot) - used for rendering
                int totalSlots = 2 * _sectionsAboveBelow + 1;
                _slotHeight = screenHeight / (double)totalSlots;
                
                // Scale PID positions from old pixel scale to new pixel scale
                if (oldScreenHeight > 0 && Math.Abs(oldScreenHeight - screenHeight) > 1)
                {
                    double scaleFactor = screenHeight / oldScreenHeight;
                    AnimationController.ScalePositions(scaleFactor);
                }
                else if (oldScreenHeight <= 0 && screenHeight > 0)
                {
                    // Screen height just became known - update target position to pixels
                    // Recalculate target from current section number
                    double positionInScreenHeights = (double)CurrentSectionNumber / totalSlots;
                    double positionInPixels = positionInScreenHeights * screenHeight;
                    AnimationController.SetTarget(positionInPixels);
                }
                
                // Reconfigure PID with new screen height (converts thresholds/acceleration)
                ConfigurePid();
                
                RecalculateFontSize(screenHeight);
                UpdateAllLabelFontSizes();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Configures all PID controller parameters.
        /// Converts from screen-height units to pixels for the PID controller.
        /// User-facing settings are in screen-height units, but PID operates in pixels.
        /// </summary>
        private void ConfigurePid()
        {
            if (AnimationController == null || _lastScreenHeight <= 0)
                return;
            
            // Gains remain constant (they're ratios)
            AnimationController.ProportionalGain = _pidProportionalGain;
            AnimationController.DerivativeGain = _pidDerivativeGain;
            AnimationController.IntegralGain = _pidIntegralGain;
            
            // Convert acceleration from screen-heights/s² to pixels/s²
            AnimationController.AccelerationLimit = _accelerationInScreenHeights * _lastScreenHeight;
            
            // Convert thresholds from screen-height units to pixels
            AnimationController.VelocityThreshold = _pidVelocityThresholdScreenHeights * _lastScreenHeight;
            AnimationController.PositionThreshold = _pidPositionThresholdScreenHeights * _lastScreenHeight;
            
            _accelerationNeedsUpdate = false;
            _pidNeedsUpdate = false;
        }
        
        /// <summary>
        /// Updates the PID configuration
        /// </summary>
        private void UpdateAccelerationLimit()
        {
            ConfigurePid();
        }

        /// <summary>
        /// Calculate the font size based on screen height
        /// Height of each number = 1/4 of evenly divided vertical space
        /// </summary>
        private void RecalculateFontSize(double screenHeight)
        {
            int totalSlots = 2 * SectionsAboveBelow + 1;
            double slotHeight = screenHeight / totalSlots;
            double newFontSize = slotHeight / 4.0;

            // Only update if the new size is larger than current (prevents regeneration on shrink)
            // Or if current labels don't exist yet
            if (newFontSize > _currentFontSize || _labelCache.Count == 0)
            {
                _currentFontSize = newFontSize;
            }
        }

        /// <summary>
        /// Update font sizes for all cached labels
        /// </summary>
        private void UpdateAllLabelFontSizes()
        {
            foreach (var label in _labelCache.Values)
            {
                if (label != null)
                {
                    label.FontSize = _currentFontSize;
                }
            }
        }

        /// <summary>
        /// Update the label cache to include visible sections plus buffer
        /// </summary>
        private void UpdateLabelCache()
        {
            int centerSection = CurrentSectionNumber;
            
            // Only update cache if center has moved significantly
            if (Math.Abs(centerSection - _lastCacheCenterSection) < 1)
                return;

            _lastCacheCenterSection = centerSection;

            int cacheMin = centerSection - SectionsAboveBelow - CacheBuffer;
            int cacheMax = centerSection + SectionsAboveBelow + CacheBuffer;

            // Remove labels outside cache range
            List<int> keysToRemove = new();
            foreach (var key in _labelCache.Keys)
            {
                if (key < cacheMin || key > cacheMax)
                {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _labelCache.Remove(key);
            }

            // Add labels for sections in cache range that don't exist yet
            for (int z = cacheMin; z <= cacheMax; z++)
            {
                if (!_labelCache.ContainsKey(z))
                {
                    _labelCache[z] = CreateLabelForSection(z);
                }
            }
        }

        /// <summary>
        /// Create a LabelView for a specific section number
        /// </summary>
        private LabelView CreateLabelForSection(int sectionNumber)
        {
            // Determine if this is a blank space (beyond min/max)
            bool isBeyondRange = sectionNumber < MinSectionNumber || sectionNumber > MaxSectionNumber;

            string text = isBeyondRange ? "" : sectionNumber.ToString();
            Color color = GetColorForSection(sectionNumber);
            color = color.SetAlpha((float)this.Opacity);

            var label = new LabelView(
                text,
                GridVector2.Zero,
                color,
                Alignment.CenterCenter,
                Anchor.CenterCenter,
                scaleFontWithScene: false,
                fontSize: _currentFontSize
            );

            return label;
        }

        /// <summary>
        /// Get the color for a section number based on whether it exists
        /// </summary>
        private Color GetColorForSection(int sectionNumber)
        {
            // Beyond range - transparent (won't be drawn anyway due to empty text)
            if (sectionNumber < MinSectionNumber || sectionNumber > MaxSectionNumber)
            {
                return Color.Transparent;
            }

            // Check if section exists
            bool exists = SectionExistsFunc?.Invoke(sectionNumber) ?? true;

            if (exists)
            {
                return NormalColor;
            }

            // Section doesn't exist - only show red if transforms exist
            bool hasTransforms = HasTransformsFunc?.Invoke() ?? false;
            return hasTransforms ? MissingColor : NormalColor;
        }

        /// <summary>
        /// Get the width in pixels of the largest section number label (at current font size, with 2x center scaling).
        /// Used to size the bar so the widest number fits and for fade calculation.
        /// </summary>
        private double GetMaxLabelWidthPixels(SpriteFont font)
        {
            string sampleText = MaxSectionNumber.ToString();
            Vector2 measurement = font.MeasureString(sampleText);
            double fontScale = _currentFontSize / font.LineSpacing;
            return measurement.X * fontScale * CenterMagnification; // center section scaling
        }

        /// <summary>
        /// Calculate the fade alpha based on label width vs screen width
        /// </summary>
        private float CalculateFadeAlpha(SpriteFont font, int screenWidth)
        {
            double labelWidth = GetMaxLabelWidthPixels(font);
            double widthRatio = labelWidth / screenWidth;

            if (widthRatio <= FadeBeginPercent)
            {
                return 1.0f;
            }
            else if (widthRatio >= FadeEndPercent)
            {
                return 0.0f;
            }
            else
            {
                // Linear interpolation between fade begin and end
                double t = (widthRatio - FadeBeginPercent) / (FadeEndPercent - FadeBeginPercent);
                return (float)(1.0 - t);
            }
        }

        /// <summary>
        /// Draw all visible labels
        /// </summary>
        private void DrawLabels(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight, float alpha)
        {
            // PID position is in pixels
            // Convert to fractional section number: sectionNumber = positionInPixels / slotHeight
            int totalSlots = 2 * SectionsAboveBelow + 1;
            double slotHeight = screenHeight / (double)totalSlots;
            double animatedSectionPosition = AnimationController.CurrentPosition / slotHeight;
            double centerY = screenHeight / 2.0;

            // Bar width: at least minimum percent, or wide enough for the largest section number
            double maxLabelWidth = GetMaxLabelWidthPixels(font);
            double barWidth = Math.Max(screenWidth * BarWidthPercent, maxLabelWidth);
            double xPos = Edge == OverlayEdge.Left ? barWidth / 2.0 : screenWidth - barWidth / 2.0;

            // Combine fade alpha with user-set opacity
            float combinedAlpha = (float)(alpha * _opacity);
            if (combinedAlpha <= 0)
                return;

            // Collect (label, drawScale) to draw; FontSize is not changed per-frame (texture stays valid)
            List<(LabelView label, float drawScale)> labelsToDraw = new();

            // Draw sections from bottom to top (higher Z at top)
            for (int offset = -SectionsAboveBelow - 1; offset <= SectionsAboveBelow + 1; offset++)
            {
                int sectionNumber = CurrentSectionNumber + offset;
                double fractionalOffset = sectionNumber - animatedSectionPosition;

                // Calculate Y position (inverted because screen Y increases downward)
                // Higher section numbers appear higher (lower Y)
                // Subtract half slot height to center the number within its display area
                double yPos = centerY - fractionalOffset * slotHeight - slotHeight / 2.0;

                // Skip if outside screen bounds
                if (yPos < -slotHeight || yPos > screenHeight + slotHeight)
                    continue;

                // Get or create label (base FontSize set at creation/UpdateAllLabelFontSizes only)
                if (!_labelCache.TryGetValue(sectionNumber, out LabelView label))
                {
                    label = CreateLabelForSection(sectionNumber);
                    _labelCache[sectionNumber] = label;
                }

                if (label == null || string.IsNullOrEmpty(label.Text))
                    continue;

                // Scale at draw time (1x to 2x) - no FontSize change, so texture stays valid
                double distanceFromCenter = Math.Abs(fractionalOffset);
                float drawScale = (float)CalculateScale(distanceFromCenter);

                // Opacity: fully opaque at center; at edges use at least MinOpacityForNonCenterSections
                double t = Clamp(distanceFromCenter, 0.0, 1.0);
                double smoothT = t * t * (3.0 - 2.0 * t);
                double effectiveEdgeAlpha = Math.Max(combinedAlpha, _minOpacityForNonCenterSections);
                float labelAlpha = (float)(1.0 - smoothT * (1.0 - effectiveEdgeAlpha));

                // Update position and color only (not FontSize)
                label.Position = new GridVector2(xPos, yPos);
                Color baseColor = GetColorForSection(sectionNumber);
                label.Color = new Color(baseColor.R, baseColor.G, baseColor.B, (byte)(baseColor.A * labelAlpha));

                labelsToDraw.Add((label, drawScale));
            }

            // Draw all labels using texture-based drawing with draw-time scale
            if (labelsToDraw.Count > 0)
            {
                // Save graphics state
                BlendState originalBlendState = spriteBatch.GraphicsDevice.BlendState;
                DepthStencilState originalDepthState = spriteBatch.GraphicsDevice.DepthStencilState;
                RasterizerState originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
                SamplerState originalSamplerState = spriteBatch.GraphicsDevice.SamplerStates[0];

                try
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                    foreach (var (label, drawScale) in labelsToDraw)
                    {
                        DrawLabelDirect(spriteBatch, font, label, drawScale);
                    }

                    spriteBatch.End();
                }
                finally
                {
                    // Restore graphics state
                    if (originalBlendState != null)
                        spriteBatch.GraphicsDevice.BlendState = originalBlendState;
                    if (originalDepthState != null)
                        spriteBatch.GraphicsDevice.DepthStencilState = originalDepthState;
                    if (originalRasterizerState != null)
                        spriteBatch.GraphicsDevice.RasterizerState = originalRasterizerState;
                    if (originalSamplerState != null)
                        spriteBatch.GraphicsDevice.SamplerStates[0] = originalSamplerState;
                }
            }
        }

        /// <summary>
        /// Draw a label using texture-based rendering (screen-space coordinates)
        /// Uses cached texture if available with draw-time scale; falls back to direct rendering if not ready
        /// </summary>
        private void DrawLabelDirect(SpriteBatch spriteBatch, SpriteFont font, LabelView label, float drawScale = 1.0f)
        {
            if (string.IsNullOrEmpty(label.Text))
                return;

            Vector2 screenPosition = new Vector2((float)label.Position.X, (float)label.Position.Y);
            label.DrawWithTexture(spriteBatch, font, spriteBatch.GraphicsDevice, screenPosition, drawScale);
        }

        /// <summary>
        /// Calculate the scale factor based on distance from center
        /// Uses smooth ease-in-out interpolation from CenterMagnification at center to 1x at edge
        /// </summary>
        private double CalculateScale(double distanceFromCenter)
        {
            // At center (distance 0): scale = CenterMagnification
            // At edge (distance >= 1): scale = 1x
            // Use smooth step for nice easing

            double t = Clamp(distanceFromCenter, 0.0, 1.0);
            
            // Smooth step interpolation: 3t^2 - 2t^3
            double smoothT = t * t * (3.0 - 2.0 * t);
            
            // Interpolate from CenterMagnification to 1.0
            return _centerMagnification - smoothT * (_centerMagnification - 1.0);
        }

        /// <summary>
        /// Clamp a value between min and max (Math.Clamp not available in .NET Framework 4.8)
        /// </summary>
        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        #endregion
    }
}
