using Microsoft.VisualStudio.TestTools.UnitTesting;
using VikingXNAGraphics;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the numeric behavior of <see cref="PIDController"/>, the animation controller behind the
    /// section-number overlay. Expected values are worked out by hand from the PID formula
    /// (desired velocity = P*error + I*integral + D*dError/dt, then limited to AccelerationLimit*dt).
    /// Gains and thresholds are the class defaults unless a test sets them.
    /// </summary>
    [TestClass]
    public class PidControllerTests
    {
        const double Tol = 1e-9;

        /// <summary>An acceleration limit large enough that the velocity always jumps straight to the PID output.</summary>
        const double Unlimited = 1e9;

        [TestMethod]
        public void Constructor_StartsAtRestOnTheInitialPosition()
        {
            var pid = new PIDController(7.5);

            Assert.AreEqual(7.5, pid.CurrentPosition);
            Assert.AreEqual(7.5, pid.TargetPosition);
            Assert.AreEqual(0.0, pid.Velocity);
            Assert.IsTrue(pid.IsComplete());
            Assert.AreEqual(0.0, new PIDController().CurrentPosition);
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(-0.1)]
        [DataRow(-5.0)]
        public void Update_NonPositiveElapsedTime_ChangesNothing(double dt)
        {
            var pid = new PIDController(0) { AccelerationLimit = 100 };
            pid.SetTarget(10);
            pid.Update(0.1);
            double position = pid.CurrentPosition, velocity = pid.Velocity;

            pid.Update(dt);

            Assert.AreEqual(position, pid.CurrentPosition);
            Assert.AreEqual(velocity, pid.Velocity);
        }

        [TestMethod]
        public void Update_NonPositiveElapsedTime_DoesNotConsumeTheFirstUpdate()
        {
            // A skipped call must not count as the first update, or the derivative term would spike on the real first one.
            var skipped = new PIDController(0) { AccelerationLimit = Unlimited };
            var direct = new PIDController(0) { AccelerationLimit = Unlimited };
            skipped.SetTarget(10);
            direct.SetTarget(10);
            skipped.SetState(2, 0);
            direct.SetState(2, 0);
            direct.Update(0.1);

            skipped.Update(0);
            skipped.Update(-1);
            skipped.Update(0.1);

            Assert.AreEqual(direct.Velocity, skipped.Velocity, Tol);
            Assert.AreEqual(direct.CurrentPosition, skipped.CurrentPosition, Tol);
        }

        [TestMethod]
        public void Update_AccelerationLimit_ClampsVelocityChangeTowardPositiveTarget()
        {
            // error 10, integral 10*0.1 = 1: desired = 5*10 + 0.01*1 = 50.01, but only 100*0.1 = 10 may be added.
            var pid = new PIDController(0) { AccelerationLimit = 100 };
            pid.SetTarget(10);

            pid.Update(0.1);

            Assert.AreEqual(10.0, pid.Velocity, Tol);
            Assert.AreEqual(1.0, pid.CurrentPosition, Tol);
        }

        [TestMethod]
        public void Update_AccelerationLimit_ClampsVelocityChangeTowardNegativeTarget()
        {
            var pid = new PIDController(0) { AccelerationLimit = 100 };
            pid.SetTarget(-10);

            pid.Update(0.1);

            Assert.AreEqual(-10.0, pid.Velocity, Tol);
            Assert.AreEqual(-1.0, pid.CurrentPosition, Tol);
        }

        [TestMethod]
        public void Update_AccelerationLimit_AppliesToDecelerationToo()
        {
            // Moving at +40 with the target behind us wants a much lower velocity; the change is still capped at 10.
            var pid = new PIDController(0) { AccelerationLimit = 100 };
            pid.SetTarget(-10);
            pid.SetState(0, 40);

            pid.Update(0.1);

            Assert.AreEqual(30.0, pid.Velocity, Tol);
        }

        [TestMethod]
        public void Update_UnderTheLimit_VelocityEqualsPidOutput()
        {
            // 5*10 + 0.01*(10*0.1) = 50.01 with the derivative term skipped on the first update.
            var pid = new PIDController(0) { AccelerationLimit = Unlimited };
            pid.SetTarget(10);

            pid.Update(0.1);

            Assert.AreEqual(50.01, pid.Velocity, Tol);
            Assert.AreEqual(5.001, pid.CurrentPosition, Tol);
        }

        [TestMethod]
        public void Update_SecondStep_AddsDerivativeAndIntegralTerms()
        {
            // Step 1: v=50.01, position 5.001, integral 1, previous error 10.
            // Step 2: error 4.999, derivative (4.999-10)/0.1 = -50.01, integral 1.4999,
            //         desired = 5*4.999 + 0.01*1.4999 + 2*(-50.01) = -75.010001.
            var pid = new PIDController(0) { AccelerationLimit = Unlimited };
            pid.SetTarget(10);
            pid.Update(0.1);

            pid.Update(0.1);

            Assert.AreEqual(-75.010001, pid.Velocity, Tol);
            Assert.AreEqual(5.001 - 7.5010001, pid.CurrentPosition, Tol);
        }

        [TestMethod]
        [DataRow(1000.0, 5001.0)]
        [DataRow(-1000.0, -5001.0)]
        public void Update_IntegralTermIsLimitedToPlusMinus100(double target, double expectedVelocity)
        {
            // integral = 1000*0.5 = 500 is limited to 100, so desired = 5*1000 + 0.01*100 = 5001.
            var pid = new PIDController(0) { AccelerationLimit = Unlimited };
            pid.SetTarget(target);

            pid.Update(0.5);

            Assert.AreEqual(expectedVelocity, pid.Velocity, Tol);
        }

        [TestMethod]
        public void SetTarget_HalvesVelocityAndKeepsPosition()
        {
            var pid = new PIDController(3);
            pid.SetState(3, 8);

            pid.SetTarget(20);

            Assert.AreEqual(20.0, pid.TargetPosition);
            Assert.AreEqual(4.0, pid.Velocity);
            Assert.AreEqual(3.0, pid.CurrentPosition);
        }

        [TestMethod]
        public void SetTarget_ResetsIntegralAndDerivativeHistory()
        {
            // After step 1 (position 5.001, integral 1, previous error 10) retarget to 10 again.
            // The next update must see integral 0 + 4.999*0.1 and a derivative of 0 (previous error re-seeded
            // to 4.999), so desired = 5*4.999 + 0.01*0.4999.
            var pid = new PIDController(0) { AccelerationLimit = Unlimited };
            pid.SetTarget(10);
            pid.Update(0.1);

            pid.SetTarget(10);
            pid.Update(0.1);

            Assert.AreEqual(5 * 4.999 + 0.01 * 0.4999, pid.Velocity, Tol);
        }

        [TestMethod]
        public void Update_WhenWithinBothThresholds_SnapsToTargetAndStops()
        {
            // P=1: desired = 0.2 + 0.01*0.02 = 0.2002, position 0.02002; error 0.18 <= 0.5 and |v| <= 1.
            var pid = new PIDController(0) { AccelerationLimit = Unlimited, ProportionalGain = 1 };
            pid.SetTarget(0.2);

            pid.Update(0.1);

            Assert.AreEqual(0.2, pid.CurrentPosition);
            Assert.AreEqual(0.0, pid.Velocity);
            Assert.IsTrue(pid.IsComplete());
        }

        [TestMethod]
        public void Update_FastButNearTarget_DoesNotSnap()
        {
            // Default P=5: velocity 1.0002 exceeds the threshold of 1, so the controller keeps its motion.
            var pid = new PIDController(0) { AccelerationLimit = Unlimited };
            pid.SetTarget(0.2);

            pid.Update(0.1);

            Assert.AreEqual(5 * 0.2 + 0.01 * 0.02, pid.Velocity, Tol);
            Assert.AreEqual(0.1 * pid.Velocity, pid.CurrentPosition, Tol);
            Assert.IsFalse(pid.IsComplete());
        }

        [TestMethod]
        public void Update_SnapClearsIntegralHistory()
        {
            // After a snap the integral restarts at zero: a new far target sees only its own first-step integral.
            var pid = new PIDController(0) { AccelerationLimit = Unlimited, ProportionalGain = 1 };
            pid.SetTarget(0.2);
            pid.Update(0.1);
            pid.ProportionalGain = 5;

            pid.SetTarget(10.2);
            pid.Update(0.1);

            // error 10, integral 1 gives 50.01; a leftover integral of 0.02 would give 50.0102.
            Assert.AreEqual(50.01, pid.Velocity, Tol);
        }

        [TestMethod]
        public void IsComplete_ThresholdsAreInclusiveAndBothRequired()
        {
            var pid = new PIDController(0);
            pid.SetTarget(0.5);
            pid.SetState(0, 1.0);
            Assert.IsTrue(pid.IsComplete(), "error == PositionThreshold and |v| == VelocityThreshold are complete");

            pid.SetState(0, 1.5);
            Assert.IsFalse(pid.IsComplete(), "too fast");

            pid.SetState(0, -1.5);
            Assert.IsFalse(pid.IsComplete(), "too fast backwards");

            pid.SetState(0, -1.0);
            Assert.IsTrue(pid.IsComplete(), "velocity threshold applies to the magnitude");

            pid.SetTarget(0.75);
            pid.SetState(0, 0);
            Assert.IsFalse(pid.IsComplete(), "too far");

            pid.SetTarget(-0.75);
            Assert.IsFalse(pid.IsComplete(), "too far on the negative side");

            pid.SetTarget(-0.5);
            Assert.IsTrue(pid.IsComplete(), "error magnitude on the negative side");
        }

        [TestMethod]
        [DataRow(0.0, 100.0)]
        [DataRow(100.0, 0.0)]
        [DataRow(-40.0, 60.0)]
        public void Update_AtFrameRate_ReachesTargetAndStopsExactly(double start, double target)
        {
            // Explicit gains so the test does not depend on the shipped defaults: critically damped enough to settle at 60 Hz.
            var pid = new PIDController(start) { AccelerationLimit = 10000, ProportionalGain = 5, IntegralGain = 0, DerivativeGain = 0.5 };
            pid.SetTarget(target);

            int steps = 0;
            while (!pid.IsComplete() && steps < 2000)
            {
                pid.Update(1.0 / 60.0);
                steps++;
            }

            Assert.IsTrue(pid.IsComplete(), "controller never settled");
            Assert.AreEqual(target, pid.CurrentPosition);
            Assert.AreEqual(0.0, pid.Velocity);
        }

        [TestMethod]
        public void ScalePositions_ScalesPositionTargetAndVelocity()
        {
            var pid = new PIDController(1);
            pid.SetTarget(10);
            pid.SetState(1, 4);

            pid.ScalePositions(2);

            Assert.AreEqual(2.0, pid.CurrentPosition);
            Assert.AreEqual(20.0, pid.TargetPosition);
            Assert.AreEqual(8.0, pid.Velocity);
        }

        [TestMethod]
        [DataRow(2.0)]
        [DataRow(0.5)]
        public void ScalePositions_ThenUpdate_MatchesScalingTheUnscaledResult(double factor)
        {
            // The PID terms are linear in position, so scaling mid-animation (history included)
            // must scale the next step's velocity and position by the same factor.
            var plain = new PIDController(0) { AccelerationLimit = Unlimited };
            var scaled = new PIDController(0) { AccelerationLimit = Unlimited };
            foreach (var pid in new[] { plain, scaled })
            {
                pid.SetTarget(10);
                pid.Update(0.1);
            }

            scaled.ScalePositions(factor);
            plain.Update(0.1);
            scaled.Update(0.1);

            Assert.AreEqual(plain.Velocity * factor, scaled.Velocity, Tol);
            Assert.AreEqual(plain.CurrentPosition * factor, scaled.CurrentPosition, Tol);
            Assert.AreEqual(plain.TargetPosition * factor, scaled.TargetPosition, Tol);
        }

        [TestMethod]
        public void SnapTo_StopsMotionAndClearsHistory()
        {
            var moved = new PIDController(0) { AccelerationLimit = Unlimited };
            moved.SetTarget(10);
            moved.Update(0.1);
            moved.Update(0.1);

            moved.SnapTo(7);

            Assert.AreEqual(7.0, moved.CurrentPosition);
            Assert.AreEqual(7.0, moved.TargetPosition);
            Assert.AreEqual(0.0, moved.Velocity);
            Assert.IsTrue(moved.IsComplete());

            // A following move behaves exactly like one from a fresh controller (no leftover integral).
            var fresh = new PIDController(7) { AccelerationLimit = Unlimited };
            moved.SetTarget(8);
            fresh.SetTarget(8);
            moved.Update(0.1);
            fresh.Update(0.1);
            Assert.AreEqual(fresh.Velocity, moved.Velocity, Tol);
            Assert.AreEqual(fresh.CurrentPosition, moved.CurrentPosition, Tol);
        }

        [TestMethod]
        public void Reset_ReturnsToRestAtThePosition()
        {
            var moved = new PIDController(0) { AccelerationLimit = Unlimited };
            moved.SetTarget(10);
            moved.Update(0.1);
            moved.Update(0.1);

            moved.Reset(3);

            Assert.AreEqual(3.0, moved.CurrentPosition);
            Assert.AreEqual(3.0, moved.TargetPosition);
            Assert.AreEqual(0.0, moved.Velocity);

            var fresh = new PIDController(3) { AccelerationLimit = Unlimited };
            moved.SetTarget(9);
            fresh.SetTarget(9);
            moved.Update(0.1);
            fresh.Update(0.1);
            Assert.AreEqual(fresh.Velocity, moved.Velocity, Tol);
        }

        [TestMethod]
        public void SetPosition_KeepsVelocityAndTarget()
        {
            var pid = new PIDController(0);
            pid.SetTarget(10);
            pid.SetState(0, 6);

            pid.SetPosition(4);

            Assert.AreEqual(4.0, pid.CurrentPosition);
            Assert.AreEqual(10.0, pid.TargetPosition);
            Assert.AreEqual(6.0, pid.Velocity);
        }

        [TestMethod]
        public void SetState_SetsPositionAndVelocityButNotTarget()
        {
            var pid = new PIDController(0);
            pid.SetTarget(10);

            pid.SetState(2, -3);

            Assert.AreEqual(2.0, pid.CurrentPosition);
            Assert.AreEqual(-3.0, pid.Velocity);
            Assert.AreEqual(10.0, pid.TargetPosition);
        }
    }
}
