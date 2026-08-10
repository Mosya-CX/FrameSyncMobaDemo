using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Minimal debug-only movement pilot for the camera tuning scene.
    /// Moves its own transform with WASD/arrow keys; deliberately has no
    /// Unit/Gameplay components so it can never touch deterministic state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraDebugPilot : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private bool clampToMap;
        [SerializeField] private Vector2 mapMin =
            new Vector2(-20f, -20f);
        [SerializeField] private Vector2 mapMax =
            new Vector2(20f, 20f);

        /// <summary>
        /// Configure the WASD rig exactly like the CameraDebugScene pilot
        /// (move speed 8, clamped to the supplied map rectangle).
        /// </summary>
        public void Configure(
            float moveSpeedValue = 8f,
            bool clampToMapValue = true,
            Vector2 mapMinValue = default,
            Vector2 mapMaxValue = default)
        {
            moveSpeed = moveSpeedValue;
            clampToMap = clampToMapValue;
            if (mapMaxValue.sqrMagnitude > 0f)
            {
                mapMin = mapMinValue;
                mapMax = mapMaxValue;
            }
        }

        private void Update()
        {
            float inputX =
                Input.GetAxisRaw("Horizontal");
            float inputZ =
                Input.GetAxisRaw("Vertical");
            Vector3 direction =
                new Vector3(inputX, 0f, inputZ);
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }
            transform.position +=
                direction.normalized *
                (moveSpeed * Time.deltaTime);
            if (clampToMap)
            {
                Vector3 position = transform.position;
                position.x = Mathf.Clamp(
                    position.x,
                    mapMin.x,
                    mapMax.x);
                position.z = Mathf.Clamp(
                    position.z,
                    mapMin.y,
                    mapMax.y);
                transform.position = position;
            }
        }
    }
}
