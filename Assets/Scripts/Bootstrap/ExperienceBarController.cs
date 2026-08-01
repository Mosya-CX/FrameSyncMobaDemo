using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class ExperienceBarController : MonoBehaviour
    {
        [SerializeField] private Slider experienceSlider;

        private void Awake()
        {
            if (experienceSlider == null)
                experienceSlider = GetComponent<Slider>();
        }

        private void Update()
        {
            if (experienceSlider == null ||
                !LuaDataCache.HasValidData)
                return;

            int current =
                LuaDataCache.Latest.CurrentExperience;
            int required =
                LuaDataCache.Latest.ExperienceForNextLevel;
            experienceSlider.value =
                required > 0 &&
                required < int.MaxValue
                    ? Mathf.Clamp01(
                        (float)current / required)
                    : 1f;
        }
    }
}
