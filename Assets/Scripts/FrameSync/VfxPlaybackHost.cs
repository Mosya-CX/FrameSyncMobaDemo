using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public enum VfxPlaybackMode : byte
    {
        ParticleSystem = 0,
        TimedGameObject = 1,
        SourceToTargetArc = 2,
    }

    /// <summary>
    /// Presentation-only playback adapter for pooled VFX GameObjects.
    /// It allows one VFX prefab to be particle, mesh, renderer, trail,
    /// line, animator, or a composite of those without changing Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxPlaybackHost : MonoBehaviour
    {
        [SerializeField] private VfxPlaybackMode playbackMode =
            VfxPlaybackMode.TimedGameObject;
        [SerializeField, Min(0.01f)] private float durationSeconds = 1f;
        [SerializeField] private Transform animatedModel;
        [SerializeField, Min(0f)] private float arcHeight = 3f;
        [SerializeField] private Vector3 modelRotationDegrees =
            new Vector3(90f, 0f, 0f);

        private Vector3 startPosition;
        private Vector3 targetPosition;
        private Vector3 initialModelLocalPosition;
        private Quaternion initialModelLocalRotation;
        private float startedAt;
        private float activeDuration;
        private bool playing;

        public float BeginPlayback(
            Vector3 sourcePosition,
            Vector3 eventPosition,
            float durationScale)
        {
            if (animatedModel == null)
                animatedModel = transform.childCount > 0
                    ? transform.GetChild(0)
                    : transform;

            initialModelLocalPosition =
                animatedModel.localPosition;
            initialModelLocalRotation =
                animatedModel.localRotation;
            activeDuration = Mathf.Max(
                0.01f,
                durationSeconds *
                Mathf.Max(0.01f, durationScale));
            startPosition = playbackMode ==
                VfxPlaybackMode.SourceToTargetArc
                    ? sourcePosition
                    : eventPosition;
            targetPosition = eventPosition;
            transform.position = startPosition;
            startedAt = Time.unscaledTime;
            playing = true;

            ParticleSystem[] particles =
                GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);

            Animator[] animators =
                GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                animators[i].Rebind();
                animators[i].Update(0f);
            }

            return activeDuration;
        }

        public void ResetForPool()
        {
            playing = false;
            ParticleSystem[] particles =
                GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i].Stop(
                    true,
                    ParticleSystemStopBehavior
                        .StopEmittingAndClear);

            TrailRenderer[] trails =
                GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
                trails[i].Clear();

            if (animatedModel != null)
            {
                animatedModel.localPosition =
                    initialModelLocalPosition;
                animatedModel.localRotation =
                    initialModelLocalRotation;
            }
        }

        private void Update()
        {
            if (!playing ||
                playbackMode !=
                    VfxPlaybackMode.SourceToTargetArc)
                return;

            float progress = Mathf.Clamp01(
                (Time.unscaledTime - startedAt) /
                activeDuration);
            transform.position = Vector3.LerpUnclamped(
                startPosition,
                targetPosition,
                progress);

            if (animatedModel != null)
            {
                Vector3 localPosition =
                    initialModelLocalPosition;
                localPosition.y +=
                    4f * arcHeight *
                    progress * (1f - progress);
                animatedModel.localPosition =
                    localPosition;
                animatedModel.localRotation =
                    initialModelLocalRotation *
                    Quaternion.Euler(
                        modelRotationDegrees *
                        progress);
            }
        }
    }
}
