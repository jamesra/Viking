using System;

namespace VikingXNAGraphics
{
    /// <summary>
    /// A PID-like animation controller that smoothly animates a value toward a target position.
    /// Supports acceleration limits and velocity preservation when the target changes.
    /// Not thread-safe; use from a single thread (e.g. UI/game thread).
    /// This controller operates in pixel units. For resolution-independent behavior,
    /// convert screen-height units to pixels before passing values to this controller.
    /// </summary>
    public class PIDController
    {
        #region Properties

        private double _targetPosition;
        /// <summary>
        /// The target position to reach
        /// </summary>
        public double TargetPosition
        {
            get { return _targetPosition; }
            private set { _targetPosition = value; }
        }

        private double _currentPosition;
        /// <summary>
        /// The current position
        /// </summary>
        public double CurrentPosition
        {
            get { return _currentPosition; }
            private set { _currentPosition = value; }
        }

        private double _velocity;
        /// <summary>
        /// The current velocity (units per second)
        /// </summary>
        public double Velocity
        {
            get { return _velocity; }
            private set { _velocity = value; }
        }

        private double _accelerationLimit = 0.1;
        /// <summary>
        /// Maximum rate of velocity change (acceleration limit in pixels per second squared).
        /// </summary>
        public double AccelerationLimit
        {
            get { return _accelerationLimit; }
            set { _accelerationLimit = value; }
        }

        private double _proportionalGain = 5.0;
        /// <summary>
        /// Proportional gain coefficient (P term)
        /// </summary>
        public double ProportionalGain
        {
            get { return _proportionalGain; }
            set { _proportionalGain = value; }
        }

        private double _integralGain = 0.01;
        /// <summary>
        /// Integral gain coefficient (I term) - optional, helps eliminate steady-state error
        /// </summary>
        public double IntegralGain
        {
            get { return _integralGain; }
            set { _integralGain = value; }
        }

        private double _derivativeGain = 2.0;
        /// <summary>
        /// Derivative gain coefficient (D term) - provides damping
        /// </summary>
        public double DerivativeGain
        {
            get { return _derivativeGain; }
            set { _derivativeGain = value; }
        }

        private double _velocityThreshold = 1;
        /// <summary>
        /// Threshold for considering velocity "near zero" (pixels per second)
        /// </summary>
        public double VelocityThreshold
        {
            get { return _velocityThreshold; }
            set { _velocityThreshold = value; }
        }

        private double _positionThreshold = 0.5;
        /// <summary>
        /// Threshold for considering position "at target" (pixels)
        /// </summary>
        public double PositionThreshold
        {
            get { return _positionThreshold; }
            set { _positionThreshold = value; }
        }

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
            _currentPosition = initialPosition;
            _targetPosition = initialPosition;
            _velocity = 0.0;
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
            double error = _targetPosition - _currentPosition;

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
            double desiredVelocity = _proportionalGain * error +
                                     _integralGain * _integralError +
                                     _derivativeGain * derivativeError;

            // Apply acceleration limit
            double velocityChange = desiredVelocity - _velocity;
            double maxVelocityChange = _accelerationLimit * elapsedSeconds;

            if (Math.Abs(velocityChange) > maxVelocityChange)
            {
                velocityChange = Math.Sign(velocityChange) * maxVelocityChange;
            }

            _velocity += velocityChange;

            // Update position based on velocity
            _currentPosition += _velocity * elapsedSeconds;

            // Store error for next iteration
            _previousError = error;

            // If we're very close to target and moving slowly, snap to target
            if (IsComplete())
            {
                _currentPosition = _targetPosition;
                _velocity = 0.0;
                _integralError = 0.0;
                _previousError = 0.0;
                _firstUpdate = true;
            }
        }

        /// <summary>
        /// Check if the animation is complete (velocity near zero AND at target position)
        /// </summary>
        /// <returns>True if animation is complete</returns>
        public bool IsComplete()
        {
            double error = Math.Abs(_targetPosition - _currentPosition);
            return error <= _positionThreshold && Math.Abs(_velocity) <= _velocityThreshold;
        }

        /// <summary>
        /// Set a new target position. Damps current velocity so rapidly changing targets
        /// in one direction don't cause runaway speed and large overshoot.
        /// </summary>
        /// <param name="target">The new target position</param>
        public void SetTarget(double target)
        {
            _targetPosition = target;
            // Reset integral error to prevent overshoot with new target
            _integralError = 0.0;
            // Update _previousError so derivative term doesn't spike on next Update()
            _previousError = _targetPosition - _currentPosition;
            // Damp velocity on target change so repeated section changes in one direction
            // don't accumulate speed and overshoot by a lot (e.g. 5→6→7→8...)
            _velocity *= 0.5;
        }

        /// <summary>
        /// Set the current position without affecting velocity or target
        /// </summary>
        /// <param name="position">The new current position</param>
        public void SetPosition(double position)
        {
            _currentPosition = position;
            _previousError = _targetPosition - _currentPosition;
        }

        /// <summary>
        /// Set both position and velocity without changing target. Used when cloning state for simulation (e.g. trajectory precompute).
        /// </summary>
        /// <param name="position">The current position</param>
        /// <param name="velocity">The current velocity (units per second)</param>
        public void SetState(double position, double velocity)
        {
            _currentPosition = position;
            _velocity = velocity;
            _previousError = _targetPosition - _currentPosition;
        }

        /// <summary>
        /// Reset the controller to initial state with the specified position
        /// </summary>
        /// <param name="position">The position to reset to (defaults to 0)</param>
        public void Reset(double position = 0.0)
        {
            _currentPosition = position;
            _targetPosition = position;
            _velocity = 0.0;
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
            _currentPosition = position;
            _targetPosition = position;
            _velocity = 0.0;
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
            _currentPosition *= scaleFactor;
            _targetPosition *= scaleFactor;
            _velocity *= scaleFactor;
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
