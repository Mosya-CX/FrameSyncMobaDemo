using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Centralized HUD-level coordinator for ability cooldown display.
    /// References 4 per-slot CooldownDisplayControllers and provides
    /// a single entry point for initialization and slot assignment.
    ///
    /// Each controller individually reads from LuaDataCache in its own
    /// Update() loop; this presenter handles cross-slot coordination.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 section 10
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityCooldownPresenter : MonoBehaviour
    {
        [SerializeField] private CooldownDisplayController slotQ;
        [SerializeField] private CooldownDisplayController slotW;
        [SerializeField] private CooldownDisplayController slotE;
        [SerializeField] private CooldownDisplayController slotR;

        public CooldownDisplayController GetSlotController(int slot)
        {
            switch (slot)
            {
                case 0: return slotQ;
                case 1: return slotW;
                case 2: return slotE;
                case 3: return slotR;
                default: return null;
            }
        }

        /// <summary>Assign a controller reference by slot index.</summary>
        public void SetSlotController(int slot, CooldownDisplayController controller)
        {
            switch (slot)
            {
                case 0: slotQ = controller; break;
                case 1: slotW = controller; break;
                case 2: slotE = controller; break;
                case 3: slotR = controller; break;
            }
        }

        /// <summary>Hide all cooldown overlays (e.g. when no abilities learned).</summary>
        public void HideAll()
        {
            for (int i = 0; i < 4; i++)
            {
                var ctrl = GetSlotController(i);
                if (ctrl != null) ctrl.gameObject.SetActive(false);
            }
        }

        /// <summary>Show all cooldown overlays.</summary>
        public void ShowAll()
        {
            for (int i = 0; i < 4; i++)
            {
                var ctrl = GetSlotController(i);
                if (ctrl != null) ctrl.gameObject.SetActive(true);
            }
        }

        private void Awake()
        {
            // Validate slot references if assigned in Inspector
            if (slotQ == null && slotW == null && slotE == null && slotR == null)
            {
                // Try to find children controllers
                var children = GetComponentsInChildren<CooldownDisplayController>();
                foreach (var child in children)
                {
                    if (child != null)
                    {
                        int slot = child.GetInstanceID() % 4; // fallback assignment
                        SetSlotController(slot, child);
                    }
                }
            }
        }
    }
}
