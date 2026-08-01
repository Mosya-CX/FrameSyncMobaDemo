using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class ResourceBarController : MonoBehaviour
    {
        [SerializeField] private Slider resourceSlider;
        [SerializeField] private TMP_Text valueText;

        private void Awake()
        {
            if (resourceSlider == null)
                resourceSlider = GetComponent<Slider>();
            if (valueText == null)
                valueText = GetComponentInChildren<TMP_Text>(true);
        }

        private void Update()
        {
            if (resourceSlider == null ||
                !LuaDataCache.HasValidData)
                return;

            float current =
                (float)LuaDataCache.Latest.CurrentResource;
            float maximum =
                (float)LuaDataCache.Latest.MaxResource;
            resourceSlider.value = maximum > 0f
                ? Mathf.Clamp01(current / maximum)
                : 0f;
            if (valueText != null)
            {
                valueText.text =
                    $"{Mathf.CeilToInt(current)} / " +
                    $"{Mathf.CeilToInt(maximum)}";
            }
        }
    }
}
