using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIGameController : MonoBehaviour
    {
        public TextMeshProUGUI MoneyText;
        public Text DebugText;
    
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }

        public void SetDebugText(string value)
        {
            DebugText.text = value;
        }

        public void SetMoney(int value)
        {
            MoneyText.text = $"Money: {value}";
        }
    }
}
