using System;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Client-only animation sampling configuration. This never participates
    /// in Gameplay, snapshots, checksums, rollback authority or networking.
    /// </summary>
    public static class UnitAnimationSynchronizationSettings
    {
        public const float DefaultSynchronizationRateHz = 20f;
        public const float MinimumSynchronizationRateHz = 1f;
        public const float MaximumSynchronizationRateHz = 240f;

        public static float SynchronizationRateHz { get; private set; } =
            DefaultSynchronizationRateHz;

        public static bool InterpolateProgress { get; private set; } = true;

        public static float SynchronizationIntervalSeconds =>
            1f / SynchronizationRateHz;

        public static void Configure(
            float synchronizationRateHz,
            bool interpolateProgress)
        {
            if (float.IsNaN(synchronizationRateHz) ||
                float.IsInfinity(synchronizationRateHz))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(synchronizationRateHz));
            }

            SynchronizationRateHz = Mathf.Clamp(
                synchronizationRateHz,
                MinimumSynchronizationRateHz,
                MaximumSynchronizationRateHz);
            InterpolateProgress = interpolateProgress;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SynchronizationRateHz = DefaultSynchronizationRateHz;
            InterpolateProgress = true;
        }
    }

    /// <summary>
    /// Read-only continuous projection of the current simulation position for
    /// client presentation. Bootstrap publishes it; it cannot advance Gameplay.
    /// </summary>
    public readonly struct AnimationPresentationTime
    {
        public AnimationPresentationTime(
            int completedLogicTick,
            int tickRate,
            double subTickAlpha)
        {
            if (tickRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(tickRate));
            if (double.IsNaN(subTickAlpha) ||
                double.IsInfinity(subTickAlpha))
            {
                throw new ArgumentOutOfRangeException(nameof(subTickAlpha));
            }

            CompletedLogicTick = completedLogicTick;
            TickRate = tickRate;
            SubTickAlpha = Math.Max(0d, Math.Min(1d, subTickAlpha));
        }

        public int CompletedLogicTick { get; }
        public int TickRate { get; }
        public double SubTickAlpha { get; }
        public double LogicTimeTicks => CompletedLogicTick + SubTickAlpha;
        public double LogicTimeSeconds => LogicTimeTicks / TickRate;
    }

    /// <summary>
    /// Single presentation clock view. It stores only the most recently
    /// published projection and has no scheduling authority of its own.
    /// </summary>
    public static class AnimationPresentationClock
    {
        private static AnimationPresentationTime current;
        private static UnitWorld currentWorld;
        private static bool hasCurrent;

        public static bool TryGetCurrent(
            UnitWorld world,
            out AnimationPresentationTime presentationTime)
        {
            presentationTime = current;
            return hasCurrent &&
                world != null &&
                ReferenceEquals(currentWorld, world);
        }

        public static void Publish(
            UnitWorld world,
            int completedLogicTick,
            int tickRate,
            double subTickAlpha)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            current = new AnimationPresentationTime(
                completedLogicTick,
                tickRate,
                subTickAlpha);
            currentWorld = world;
            hasCurrent = true;
        }

        /// <summary>
        /// Releases a match-owned projection without allowing an outgoing
        /// match to clear a newer match's clock.
        /// </summary>
        public static void Clear(UnitWorld world)
        {
            if (!hasCurrent ||
                world == null ||
                !ReferenceEquals(currentWorld, world))
            {
                return;
            }

            current = default;
            currentWorld = null;
            hasCurrent = false;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            current = default;
            currentWorld = null;
            hasCurrent = false;
        }
    }

    /// <summary>
    /// Samples normalized animation progress at a configurable cadence. When
    /// interpolation is enabled, each sample predicts the next sample endpoint
    /// and render frames interpolate between the two, avoiding sample-step
    /// playback without coupling the cadence to Gameplay TickRate.
    /// </summary>
    public sealed class ConfigurableAnimationProgressSampler
    {
        private bool initialized;
        private int stateKey;
        private bool looping;
        private bool interpolate;
        private float rateHz;
        private double segmentStartTime;
        private double segmentEndTime;
        private double lastObservedTime;
        private float segmentStartValue;
        private float segmentEndValue;
        private float heldValue;

        public float Sample(
            double nowSeconds,
            float valueNow,
            float valueAtNextSample,
            int newStateKey,
            bool loop,
            float synchronizationRateHz,
            bool interpolateProgress,
            double segmentDurationSeconds = -1d)
        {
            ValidateFinite(nowSeconds, nameof(nowSeconds));
            ValidateFinite(valueNow, nameof(valueNow));
            ValidateFinite(valueAtNextSample, nameof(valueAtNextSample));
            ValidateFinite(
                synchronizationRateHz,
                nameof(synchronizationRateHz));
            ValidateFinite(
                segmentDurationSeconds,
                nameof(segmentDurationSeconds));
            float clampedRate = Mathf.Clamp(
                synchronizationRateHz,
                UnitAnimationSynchronizationSettings
                    .MinimumSynchronizationRateHz,
                UnitAnimationSynchronizationSettings
                    .MaximumSynchronizationRateHz);
            bool reset =
                !initialized ||
                stateKey != newStateKey ||
                looping != loop ||
                interpolate != interpolateProgress ||
                !Mathf.Approximately(rateHz, clampedRate) ||
                nowSeconds < lastObservedTime;
            double interval = 1d / clampedRate;
            if (segmentDurationSeconds >= 0d)
            {
                interval = Math.Min(
                    interval,
                    segmentDurationSeconds);
            }
            if (reset)
            {
                initialized = true;
                stateKey = newStateKey;
                looping = loop;
                interpolate = interpolateProgress;
                rateHz = clampedRate;
                segmentStartTime = nowSeconds;
                segmentEndTime = nowSeconds + interval;
                segmentStartValue = valueNow;
                segmentEndValue = interpolateProgress && interval > 0d
                    ? valueAtNextSample
                    : valueNow;
                heldValue = valueNow;
                lastObservedTime = nowSeconds;
                return Normalize(valueNow, loop);
            }

            if (nowSeconds >= segmentEndTime)
            {
                segmentStartValue = valueNow;
                if (interpolateProgress)
                {
                    segmentEndValue = interval > 0d
                        ? valueAtNextSample
                        : valueNow;
                }
                else
                {
                    heldValue = valueNow;
                    segmentEndValue = heldValue;
                }

                segmentStartTime = nowSeconds;
                segmentEndTime = nowSeconds + interval;
            }

            lastObservedTime = nowSeconds;
            if (!interpolateProgress)
                return Normalize(heldValue, loop);

            float t = segmentEndTime <= segmentStartTime
                ? 1f
                : Mathf.Clamp01((float)(
                    (nowSeconds - segmentStartTime) /
                    (segmentEndTime - segmentStartTime)));
            return Normalize(
                Mathf.LerpUnclamped(
                    segmentStartValue,
                    segmentEndValue,
                    t),
                loop);
        }

        public void Clear()
        {
            initialized = false;
        }

        private static float Normalize(float value, bool loop) =>
            loop ? Mathf.Repeat(value, 1f) : Mathf.Clamp01(value);

        private static void ValidateFinite(double value, string parameter)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameter);
        }

        private static void ValidateFinite(float value, string parameter)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
    }

    /// <summary>
    /// Reconstructs an unwrapped loop phase from the match logic-time epoch.
    /// The public sampled result is reduced modulo one by the progress sampler.
    /// </summary>
    public sealed class LoopAnimationPhaseTracker
    {
        private bool initialized;
        private int stateKey;
        private double anchorTimeSeconds;
        private double anchorPhase;
        private double cyclesPerSecond;
        private double lastObservedTime;

        public bool Observe(
            int newStateKey,
            double nowSeconds,
            double newCyclesPerSecond)
        {
            ValidateFinite(nowSeconds, nameof(nowSeconds));
            ValidateFinite(newCyclesPerSecond, nameof(newCyclesPerSecond));
            bool reset =
                !initialized ||
                stateKey != newStateKey ||
                nowSeconds < lastObservedTime;
            if (reset)
            {
                initialized = true;
                stateKey = newStateKey;
                anchorTimeSeconds = nowSeconds;
                cyclesPerSecond = Math.Max(0d, newCyclesPerSecond);
                // A match-logic epoch makes first observation, asynchronous
                // view creation and rollback re-observation reconstruct the
                // same phase instead of anchoring to a local render frame.
                anchorPhase = nowSeconds * cyclesPerSecond;
                lastObservedTime = nowSeconds;
                return true;
            }

            double clampedRate = Math.Max(0d, newCyclesPerSecond);
            bool rateChanged = false;
            if (Math.Abs(clampedRate - cyclesPerSecond) > 0.000001d)
            {
                anchorTimeSeconds = nowSeconds;
                cyclesPerSecond = clampedRate;
                // Rebuild from the canonical logic epoch so asynchronous
                // observers and a single endpoint rollback do not retain a
                // permanent phase split after historical speed changes.
                anchorPhase = nowSeconds * cyclesPerSecond;
                rateChanged = true;
            }

            lastObservedTime = nowSeconds;
            return rateChanged;
        }

        public double EvaluateUnwrapped(double timeSeconds)
        {
            ValidateFinite(timeSeconds, nameof(timeSeconds));
            if (!initialized)
                return 0d;
            return anchorPhase +
                Math.Max(0d, timeSeconds - anchorTimeSeconds) *
                cyclesPerSecond;
        }

        public void Clear()
        {
            initialized = false;
        }

        private static void ValidateFinite(double value, string parameter)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
