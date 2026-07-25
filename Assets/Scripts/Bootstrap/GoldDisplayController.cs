using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GoldDisplayController : MonoBehaviour
    {
        [SerializeField] private Text goldText;
        private int _displayedGold = -1;

        private void Start()
        {
            if (goldText == null) goldText = GetComponent<Text>();
        }

        private void Update()
        {
            int currentGold = LuaDataCache.CurrentGold;
            if (currentGold != _displayedGold && goldText != null)
            {
                _displayedGold = currentGold;
                goldText.text = currentGold.ToString();
            }
        }
    }
}
