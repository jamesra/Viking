using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// Trajectory precompute step in seconds (25 ms)
        /// </summary>
        private const double TrajectoryStepSeconds = 0.025;

        /// <summary>
        /// Maximum trajectory duration in seconds; if not complete by then, final entry at target is added
        /// </summary>
        private const double TrajectoryMaxDurationSeconds = 15.0;

        #endregion

        #region PID Configuration (configurable via preferences)
        
        /// <summary>
        /// Proportional gain for PID controller
        /// </summary>
        private double _pidProportionalGain = 2500.0;
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
        private double _pidDerivativeGain = 4500;
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
        private double _pidIntegralGain = 40;
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
        private double _pidVelocityThresholdScreenHeights = 0.01;
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
        private double _pidPositionThresholdScreenHeights = 0.02;
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
        private int _sectionsAboveBelow = 25;
        public int SectionsAboveBelow
        {
            get => _sectionsAboveBelow;
            set
            {
                _sectionsAboveBelow = value;
                ConfigurePid();
                // PID target is in pixels; recompute when totalSlots changes
                if (_lastScreenHeight > 0)
                    PrecomputeTrajectory(GetPositionPixelsForSection(CurrentSectionNumber));
            }
        }

        /// <summary>
        /// Total vertical slots (2 * SectionsAboveBelow + 1). Used for section-to-position conversion and layout.
        /// </summary>
        private int TotalSlots => 2 * _sectionsAboveBelow + 1;

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
        private double _accelerationInScreenHeights = 50.0;
        public double AccelerationInScreenHeights
        {
            get => _accelerationInScreenHeights;
            set
            {
                if(_accelerationInScreenHeights != value)
                { 
                    _accelerationInScreenHeights = value;
                    _accelerationNeedsUpdate = true;
                    ConfigurePid();
                }
            }
        }

        /// <summary>
        /// Color for normal (existing) sections
        /// </summary>
        public Color NormalColor { get; set; } = Color.Cornsilk;

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
        private double _opacity = 1;
        public double Opacity
        {
            get => _opacity;
            set => _opacity = Clamp(value, 0.0, 1.0);
        }

        /// <summary>
        /// Minimum opacity for non-center section numbers (0.0 to 0.8). Default 0.5.
        /// Edge numbers never go below this so they remain readable.
        /// </summary>
        private double _minOpacityForNonCenterSections = 0.3;
        public double MinOpacityForNonCenterSections
        {
            get => _minOpacityForNonCenterSections;
            set => _minOpacityForNonCenterSections = Clamp(value, 0.0, 0.8);
        }

        #endregion

        #region Private Fields

        private readonly SharedDigitTextureCache _digitCache = new SharedDigitTextureCache();
        private NumberDisplayDrawContext _drawContext;
        private double _lastScreenHeight = 0;
        private double _lastScreenWidth = 0;
        /// <summary>
        /// Display scale: (slotHeight/4) / DigitTextureResolution. Converts fixed ~128 px digit texture space to screen pixels.
        /// </summary>
        private double _displayScale = 0.25;
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
        /// Precomputed trajectory: (timeSeconds, position, velocity) keyframes. Null when not animating.
        /// </summary>
        private List<(double timeSeconds, double position, double velocity)> _trajectory;

        /// <summary>
        /// High-resolution timestamp when current trajectory started (from Stopwatch.GetTimestamp()).
        /// Used in Draw to compute elapsed time since target was set for correct frame sampling.
        /// </summary>
        private long _trajectoryStartTicks;

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
            PrecomputeTrajectory(GetPositionPixelsForSection(sectionNumber));
        }

        /// <summary>
        /// Initialize or reset the overlay to a specific section without animation
        /// </summary>
        /// <param name="sectionNumber">The section number to display</param>
        public void Initialize(int sectionNumber)
        {
            CurrentSectionNumber = sectionNumber;
            _trajectory = null;
            AnimationController.SnapTo(GetPositionPixelsForSection(sectionNumber));
        }

        /// <summary>
        /// No-op. Animation is driven by a precomputed trajectory sampled in Draw. Kept for API compatibility.
        /// </summary>
        /// <param name="elapsedSeconds">Ignored.</param>
        public void Update(double elapsedSeconds)
        {
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
                ApplyViewportChange(screenWidth, screenHeight);

            // Apply PID configuration if parameters changed or screen size changed.
            // New PID/accel values apply only to the next trajectory (next target change); current animation finishes with the old curve.
            if (_accelerationNeedsUpdate || _pidNeedsUpdate || screenSizeChanged)
            {
                ConfigurePid();
            }

            // Playback: sample precomputed trajectory by elapsed real time since target was set
            if (_trajectory != null && _trajectory.Count > 0)
            {
                long nowTicks = Stopwatch.GetTimestamp();
                double elapsedSeconds = (nowTicks - _trajectoryStartTicks) / (double)Stopwatch.Frequency;
                double endTime = _trajectory[_trajectory.Count - 1].timeSeconds;
                if (elapsedSeconds >= endTime)
                {
                    AnimationController.SnapTo(AnimationController.TargetPosition);
                    _trajectory = null;
                }
                else
                {
                    double position = SampleTrajectory(elapsedSeconds);
                    AnimationController.SetPosition(position);
                }
            }

            _digitCache.EnsureInitialized(font);

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
                bool wasFirstKnownHeight = ApplyViewportChange(screenWidth, screenHeight);
                ConfigurePid();
                if (!wasFirstKnownHeight)
                    PrecomputeTrajectory(AnimationController.TargetPosition);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Get position in pixels for a section number. When screen height is unknown (0), returns position in 0..1 (screen-height units).
        /// </summary>
        /// <param name="sectionNumber">Section number (Z)</param>
        /// <param name="screenHeight">Optional height in pixels; when null uses _lastScreenHeight</param>
        private double GetPositionPixelsForSection(int sectionNumber, double? screenHeight = null)
        {
            double effectiveHeight = screenHeight ?? _lastScreenHeight;
            double positionInScreenHeights = (double)sectionNumber / TotalSlots;
            return effectiveHeight > 0 ? positionInScreenHeights * effectiveHeight : positionInScreenHeights;
        }

        /// <summary>
        /// Snap the overlay to the current section position (no animation). Used when screen height first becomes known.
        /// </summary>
        private void SnapToCurrentSectionPosition(double screenHeight)
        {
            _trajectory = null;
            AnimationController.SnapTo(GetPositionPixelsForSection(CurrentSectionNumber, screenHeight));
        }

        /// <summary>
        /// Apply a viewport/screen size change: update stored size, slot height, scale or snap position, and font size.
        /// Caller should call ConfigurePid() after. Does not call PrecomputeTrajectory.
        /// </summary>
        /// <returns>True if this was the first time screen height became known (caller may skip PrecomputeTrajectory).</returns>
        private bool ApplyViewportChange(int screenWidth, int screenHeight)
        {
            double oldScreenHeight = _lastScreenHeight;
            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _slotHeight = screenHeight / (double)TotalSlots;

            if (oldScreenHeight > 0 && Math.Abs(oldScreenHeight - screenHeight) > 1)
            {
                double scaleFactor = screenHeight / oldScreenHeight;
                AnimationController.ScalePositions(scaleFactor);
                ScaleTrajectory(scaleFactor);
            }
            else if (oldScreenHeight <= 0 && screenHeight > 0)
            {
                SnapToCurrentSectionPosition(screenHeight);
            }

            RecalculateFontSize(screenHeight);
            return oldScreenHeight <= 0 && screenHeight > 0;
        }

        /// <summary>
        /// Scale trajectory keyframes by the given factor (e.g. when viewport is resized).
        /// </summary>
        private void ScaleTrajectory(double scaleFactor)
        {
            if (_trajectory == null)
                return;
            var scaled = new List<(double timeSeconds, double position, double velocity)>(_trajectory.Count);
            for (int i = 0; i < _trajectory.Count; i++)
            {
                var k = _trajectory[i];
                scaled.Add((k.timeSeconds, k.position * scaleFactor, k.velocity * scaleFactor));
            }
            _trajectory = scaled;
        }

        /// <summary>
        /// Precompute trajectory from current position/velocity to target using a temporary PID controller.
        /// Steps at 25 ms; stops at IsComplete or 5 s, then adds final entry at target if capped.
        /// Call when target changes (SetCurrentSection, SectionsAboveBelow, or viewport/target update).
        /// </summary>
        private void PrecomputeTrajectory(double targetPositionInPixels)
        {
            ConfigurePid();
            if (AnimationController == null || _lastScreenHeight <= 0)
            {
                AnimationController?.SetTarget(targetPositionInPixels);
                _trajectory = null;
                return;
            }

            var temp = new PIDController(AnimationController.CurrentPosition);
            temp.ProportionalGain = AnimationController.ProportionalGain;
            temp.DerivativeGain = AnimationController.DerivativeGain;
            temp.IntegralGain = AnimationController.IntegralGain;
            temp.AccelerationLimit = AnimationController.AccelerationLimit;
            temp.VelocityThreshold = AnimationController.VelocityThreshold;
            temp.PositionThreshold = AnimationController.PositionThreshold;
            temp.SetTarget(targetPositionInPixels);
            temp.SetState(AnimationController.CurrentPosition, AnimationController.Velocity);

            var list = new List<(double timeSeconds, double position, double velocity)>();
            list.Add((0, temp.CurrentPosition, temp.Velocity));

            double time = 0;
            bool completed = false;
            while (time < TrajectoryMaxDurationSeconds)
            {
                time += TrajectoryStepSeconds;
                if (time > TrajectoryMaxDurationSeconds)
                    break;
                temp.Update(TrajectoryStepSeconds);
                list.Add((time, temp.CurrentPosition, temp.Velocity));
                if (temp.IsComplete())
                {
                    completed = true;
                    break;
                }
            }

            if (!completed && time >= TrajectoryMaxDurationSeconds)
                list.Add((TrajectoryMaxDurationSeconds, targetPositionInPixels, 0));

            _trajectory = list;
            AnimationController.SetTarget(targetPositionInPixels);
            _trajectoryStartTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Sample position from trajectory at given elapsed time (linear interpolation between keyframes).
        /// </summary>
        private double SampleTrajectory(double elapsedSeconds)
        {
            if (_trajectory == null || _trajectory.Count == 0)
                return AnimationController.CurrentPosition;
            if (elapsedSeconds <= _trajectory[0].timeSeconds)
                return _trajectory[0].position;
            int last = _trajectory.Count - 1;
            if (elapsedSeconds >= _trajectory[last].timeSeconds)
                return _trajectory[last].position;
            for (int i = 0; i < last; i++)
            {
                if (elapsedSeconds >= _trajectory[i].timeSeconds && elapsedSeconds < _trajectory[i + 1].timeSeconds)
                {
                    double t0 = _trajectory[i].timeSeconds;
                    double t1 = _trajectory[i + 1].timeSeconds;
                    double p0 = _trajectory[i].position;
                    double p1 = _trajectory[i + 1].position;
                    double alpha = (elapsedSeconds - t0) / (t1 - t0);
                    return p0 + alpha * (p1 - p0);
                }
            }
            return _trajectory[last].position;
        }

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
        /// Recompute display scale from slot height. Digit textures are fixed ~128 px; display scale converts to screen pixels.
        /// </summary>
        private void RecalculateFontSize(double screenHeight)
        {
            double slotHeight = screenHeight / TotalSlots;
            double displaySizePerNumber = slotHeight / 4.0;
            _displayScale = displaySizePerNumber / SharedDigitTextureCache.DigitTextureResolution;
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
        /// Get the width in pixels of the largest section number label at 1x display size.
        /// Used for fade calculation (so CenterMagnification does not affect opacity) and as base for bar width.
        /// </summary>
        private double GetMaxLabelWidthPixels()
        {
            float widthInTextureSpace = _digitCache.GetWidthForNumber(MaxSectionNumber.ToString());
            return widthInTextureSpace * _displayScale;
        }

        /// <summary>
        /// Calculate the fade alpha based on label width vs screen width
        /// </summary>
        private float CalculateFadeAlpha(SpriteFont font, int screenWidth)
        {
            double labelWidth = GetMaxLabelWidthPixels();
            double widthRatio = labelWidth / screenWidth;
            if (widthRatio <= FadeBeginPercent)
                return 1.0f;
            if (widthRatio >= FadeEndPercent)
                return 0.0f;
            double t = (widthRatio - FadeBeginPercent) / (FadeEndPercent - FadeBeginPercent);
            return (float)(1.0 - t);
        }

        /// <summary>
        /// Draw all visible section numbers using NumberDisplayView and shared digit textures.
        /// </summary>
        private void DrawLabels(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight, float alpha)
        {
            // PID position is in pixels; convert to section space to determine which numbers are visible
            double slotHeight = screenHeight / (double)TotalSlots;
            double animatedSectionPosition = AnimationController.CurrentPosition / slotHeight;
            double centerY = screenHeight / 2.0;

            // Visible section range from current PID position (not CurrentSectionNumber)
            int sectionMin = (int)Math.Floor(animatedSectionPosition - SectionsAboveBelow - 1);
            int sectionMax = (int)Math.Ceiling(animatedSectionPosition + SectionsAboveBelow + 1);

            // Bar width: at least minimum percent, or wide enough for the magnified center label
            double maxLabelWidth = GetMaxLabelWidthPixels();
            double barWidth = Math.Max(screenWidth * BarWidthPercent, maxLabelWidth * _centerMagnification);
            double xPos = Edge == OverlayEdge.Left ? barWidth / 2.0 : screenWidth - barWidth / 2.0;

            // Combine fade alpha with user-set opacity
            float combinedAlpha = (float)(alpha * _opacity);
            if (combinedAlpha <= 0)
                return;

            // Build or reuse draw context
            if (_drawContext == null || _drawContext.SpriteBatch != spriteBatch || _drawContext.Font != font)
                _drawContext = new NumberDisplayDrawContext(_digitCache, spriteBatch, font, spriteBatch.GraphicsDevice);

            BlendState originalBlendState = spriteBatch.GraphicsDevice.BlendState;
            DepthStencilState originalDepthState = spriteBatch.GraphicsDevice.DepthStencilState;
            RasterizerState originalRasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
            SamplerState originalSamplerState = spriteBatch.GraphicsDevice.SamplerStates[0];

            try
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                // Draw all section numbers visible given current PID position (bottom to top, higher Z at top)
                for (int sectionNumber = sectionMin; sectionNumber <= sectionMax; sectionNumber++)
                {
                    if (sectionNumber < MinSectionNumber || sectionNumber > MaxSectionNumber)
                        continue;

                    double fractionalOffset = sectionNumber - animatedSectionPosition;
                    double yPos = centerY - fractionalOffset * slotHeight - slotHeight / 2.0;

                    if (yPos < -slotHeight || yPos > screenHeight + slotHeight)
                        continue;

                    double distanceFromCenter = Math.Abs(fractionalOffset);
                    float sectionDrawScale = (float)CalculateScale(distanceFromCenter, SectionsAboveBelow);

                    double tOpacity = Clamp(distanceFromCenter / SectionsAboveBelow, 0.0, 1.0);
                    float labelAlpha = (float)(tOpacity <= 0.5
                        ? _opacity - (tOpacity * 2.0) * (_opacity - _minOpacityForNonCenterSections)
                        : _minOpacityForNonCenterSections);

                    Color baseColor = GetColorForSection(sectionNumber);
                    Color color = new Color(baseColor.R, baseColor.G, baseColor.B, (byte)(baseColor.A * labelAlpha * alpha));
                    float drawScale = (float)(sectionDrawScale * _displayScale);

                    NumberDisplayView.Draw(_drawContext, sectionNumber, new Vector2((float)xPos, (float)yPos), color, drawScale);
                }

                spriteBatch.End();
            }
            finally
            {
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

        /// <summary>
        /// Calculate the scale factor based on distance from center.
        /// Scale is CenterMagnification at center, 1.0 at the midpoint to edge, linear ramp in between.
        /// </summary>
        /// <param name="distanceFromCenter">Distance in section units (0 = center).</param>
        /// <param name="maxDistance">Distance at edge (e.g. SectionsAboveBelow).</param>
        private double CalculateScale(double distanceFromCenter, double maxDistance)
        {
            if (maxDistance <= 0.0)
                return _centerMagnification;

            // t = 0 at center, t = 1 at edge; midpoint is t = 0.5
            double t = Clamp(distanceFromCenter / maxDistance, 0.0, 1.0);

            if (t <= 0.5)
            {
                // Ramp from CenterMagnification at center to 1.0 at midpoint (u = 0..1 over first half)
                double u = t * 2.0;
                return _centerMagnification - u * (_centerMagnification - 1.0);
            }
            return 1.0;
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

        /// <summary>
        /// Implements INumberDisplayDrawContext using SharedDigitTextureCache + SpriteBatch + font + device.
        /// </summary>
        private sealed class NumberDisplayDrawContext : INumberDisplayDrawContext
        {
            private readonly SharedDigitTextureCache _cache;
            public readonly SpriteBatch SpriteBatch;
            public readonly SpriteFont Font;
            private readonly GraphicsDevice _device;

            public NumberDisplayDrawContext(SharedDigitTextureCache cache, SpriteBatch spriteBatch, SpriteFont font, GraphicsDevice device)
            {
                _cache = cache;
                SpriteBatch = spriteBatch;
                Font = font;
                _device = device;
            }

            public void DrawDigit(int digitIndex, Vector2 centerPosition, float drawScale, Color color)
            {
                _cache.DrawDigit(SpriteBatch, _device, Font, digitIndex, centerPosition, drawScale, color);
            }

            public float GetDigitWidth(int digitIndex) => _cache.GetDigitWidth(digitIndex);

            public float GetDigitHeight() => _cache.GetDigitHeight();
        }
    }
}
