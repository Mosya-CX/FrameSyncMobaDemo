using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GoldDisplayController : MonoBehaviour
    {
        [SerializeField] private Text goldText;
        [SerializeField] private TMP_Text goldTextMeshPro;
        private int _displayedGold = -1;

        private void Start()
        {
            if (goldText == null)
                goldText = GetComponent<Text>();
            if (goldTextMeshPro == null)
                goldTextMeshPro = GetComponent<TMP_Text>();
        }

        private void Update()
        {
            int currentGold = LuaDataCache.CurrentGold;
            if (currentGold == _displayedGold)
                return;

            _displayedGold = currentGold;
            string value = currentGold.ToString();
            if (goldText != null)
                goldText.text = value;
            if (goldTextMeshPro != null)
            {
                goldTextMeshPro.text = value;
            }
        }
    }
}
