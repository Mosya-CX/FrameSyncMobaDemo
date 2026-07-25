using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class HealthBarController : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider;
        [SerializeField] private float smoothSpeed = 8f;
        private float _targetFill;

        private void Start()
        {
            if (healthSlider == null) healthSlider = GetComponent<Slider>();
        }

        private void Update()
        {
            if (healthSlider == null || !LuaDataCache.HasValidData) return;
            float current = (float)LuaDataCache.CurrentHealth;
            float max = (float)LuaDataCache.MaxHealth;
            _targetFill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            healthSlider.value = Mathf.Lerp(healthSlider.value, _targetFill, Time.deltaTime * smoothSpeed);
        }
    }
}
