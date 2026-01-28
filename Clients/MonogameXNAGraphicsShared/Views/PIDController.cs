using System;

namespace VikingXNAGraphics
{
    /// <summary>
    /// A PID-like animation controller that smoothly animates a value toward a target position.
    /// Supports acceleration limits and velocity preservation when the target changes.
    /// 
    /// This controller operates in pixel units. For resolution-independent behavior,
    /// convert screen-height units to pixels before passing values to this controller.
    /// </summary>
    public class PIDController
    {
        #region Properties

        /// <summary>
        /// The target position to reach
        /// </summary>
        public double TargetPosition { get; private set; }

        /// <summary>
        /// The current position
        /// </summary>
        public double CurrentPosition { get; private set; }

        /// <summary>
        /// The current velocity (units per second)
        /// </summary>
        public double Velocity { get; private set; }

        /// <summary>
        /// Maximum rate of velocity change (acceleration limit in pixels per second squared).
        /// </summary>
        public double AccelerationLimit { get; set; } = 0.1;

        /// <summary>
        /// Proportional gain coefficient (P term)
        /// </summary>
        public double ProportionalGain { get; set; } = 5.0;

        /// <summary>
        /// Integral gain coefficient (I term) - optional, helps eliminate steady-state error
        /// </summary>
        public double IntegralGain { get; set; } = 0.01;

        /// <summary>
        /// Derivative gain coefficient (D term) - provides damping
        /// </summary>
        public double DerivativeGain { get; set; } = 2.0;

        /// <summary>
        /// Threshold for considering velocity "near zero" (pixels per second)
        /// </summary>
        public double VelocityThreshold { get; set; } = 1;

        /// <summary>
        /// Threshold for considering position "at target" (pixels)
        /// </summary>
        public double PositionThreshold { get; set; } = 0.5;

        #endregion

        #region Private Fields

        private double _integralError = 0.0;
        private double _previousError = 0.0;
        private bool _firstUpdate = true;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new PIDController with the specified initial position
        /// </summary>
        /// <param name="initialPosition">The initial position</param>
        public PIDController(double initialPosition = 0.0)
        {
            CurrentPosition = initialPosition;
            TargetPosition = initialPosition;
            Velocity = 0.0;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update the controller state based on elapsed time
        /// </summary>
        /// <param name="elapsedSeconds">Time elapsed since last update in seconds</param>
        public void Update(double elapsedSeconds)
        {
            if (elapsedSeconds <= 0)
                return;

            // Calculate error (difference between target and current position)
            double error = TargetPosition - CurrentPosition;

            // Calculate derivative of error
            double derivativeError = 0.0;
            if (!_firstUpdate)
            {
                derivativeError = (error - _previousError) / elapsedSeconds;
            }
            _firstUpdate = false;

            // Accumulate integral error (with anti-windup)
            // Limit is in pixel-seconds
            _integralError += error * elapsedSeconds;
            _integralError = Clamp(_integralError, -100.0, 100.0); // Prevent integral windup

            // Calculate desired velocity using PID formula
            double desiredVelocity = ProportionalGain * error +
                                     IntegralGain * _integralError +
                                     DerivativeGain * derivativeError;

            // Apply acceleration limit
            double velocityChange = desiredVelocity - Velocity;
            double maxVelocityChange = AccelerationLimit * elapsedSeconds;

            if (Math.Abs(velocityChange) > maxVelocityChange)
            {
                velocityChange = Math.Sign(velocityChange) * maxVelocityChange;
            }

            Velocity += velocityChange;

            // Update position based on velocity
            CurrentPosition += Velocity * elapsedSeconds;

            // Store error for next iteration
            _previousError = error;

            // If we're very close to target and moving slowly, snap to target
            if (IsComplete())
            {
                CurrentPosition = TargetPosition;
                Velocity = 0.0;
                _integralError = 0.0;
            }
        }

        /// <summary>
        /// Check if the animation is complete (velocity near zero AND at target position)
        /// </summary>
        /// <returns>True if animation is complete</returns>
        public bool IsComplete()
        {
            double error = Math.Abs(TargetPosition - CurrentPosition);
            return error <= PositionThreshold && Math.Abs(Velocity) <= VelocityThreshold;
        }

        /// <summary>
        /// Set a new target position (preserves current velocity for smooth transitions)
        /// </summary>
        /// <param name="target">The new target position</param>
        public void SetTarget(double target)
        {
            TargetPosition = target;
            // Velocity is preserved for smooth transitions
            // Reset integral error to prevent overshoot with new target
            _integralError = 0.0;
        }

        /// <summary>
        /// Set the current position without affecting velocity or target
        /// </summary>
        /// <param name="position">The new current position</param>
        public void SetPosition(double position)
        {
            CurrentPosition = position;
            _previousError = TargetPosition - CurrentPosition;
        }

        /// <summary>
        /// Reset the controller to initial state with the specified position
        /// </summary>
        /// <param name="position">The position to reset to (defaults to 0)</param>
        public void Reset(double position = 0.0)
        {
            CurrentPosition = position;
            TargetPosition = position;
            Velocity = 0.0;
            _integralError = 0.0;
            _previousError = 0.0;
            _firstUpdate = true;
        }

        /// <summary>
        /// Immediately set both position and target to the same value, stopping all motion
        /// </summary>
        /// <param name="position">The position to snap to</param>
        public void SnapTo(double position)
        {
            CurrentPosition = position;
            TargetPosition = position;
            Velocity = 0.0;
            _integralError = 0.0;
            _previousError = 0.0;
        }

        /// <summary>
        /// Scale all position-related values by a factor (used when screen size changes).
        /// Preserves the animation state relative to the new scale.
        /// </summary>
        /// <param name="scaleFactor">The factor to multiply positions and velocity by</param>
        public void ScalePositions(double scaleFactor)
        {
            CurrentPosition *= scaleFactor;
            TargetPosition *= scaleFactor;
            Velocity *= scaleFactor;
            _previousError *= scaleFactor;
            // Integral error is in position-seconds, so it also needs scaling
            _integralError *= scaleFactor;
        }

        #endregion

        #region Private Helpers

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
