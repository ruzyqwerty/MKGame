using TMPro;
using UnityEngine;

namespace UI
{
    public class UIGameController : MonoBehaviour
    {
        public TextMeshProUGUI MoneyText;
    
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }

        public void SetMoney(int value)
        {
            MoneyText.text = $"Money: {value}";
        }
    }
}
