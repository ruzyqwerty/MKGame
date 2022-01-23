using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIGameController : MonoBehaviour
    {
        public TextMeshProUGUI MoneyText;
        public Text DebugText;
        public GameObject CardPanel;
        public GameObject MinCardPanel;

        public void SetDebugText(string value)
        {
            DebugText.text = value;
        }

        public void SetMoney(int value)
        {
            MoneyText.text = $"Money: {value}";
        }

        public void OnCardPanelEnter()
        {
            CardPanel.SetActive(true);
        }

        public void OnCardPanelExit()
        {
            CardPanel.SetActive(false);
        }
    }
}
