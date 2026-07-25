using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class CooldownDisplayController : MonoBehaviour
    {
        [SerializeField] private int abilitySlot;
        [SerializeField] private Image cooldownFill;
        [SerializeField] private GameObject cooldownOverlay;

        private void Start()
        {
            if (cooldownFill == null) cooldownFill = GetComponent<Image>();
        }

        private void Update()
        {
            int remaining = LuaDataCache.CooldownRemaining(abilitySlot);
            int total = LuaDataCache.CooldownTotal(abilitySlot);
            bool onCooldown = remaining > 0 && total > 0;
            if (cooldownOverlay != null) cooldownOverlay.SetActive(onCooldown);
            if (cooldownFill != null)
            {
                if (onCooldown) cooldownFill.fillAmount = 1f - ((float)remaining / total);
                else cooldownFill.fillAmount = 1f;
            }
        }
    }
}
