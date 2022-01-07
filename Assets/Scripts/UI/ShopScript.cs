using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class ShopScript : MonoBehaviour
    {
        public GameObject ShopPanel;
        
        private bool _bIsShopClosed = true;

        public void ToggleShop()
        {
            if (_bIsShopClosed)
            {
                OpenShop();
                _bIsShopClosed = !_bIsShopClosed;
                return;
            }
        
            _bIsShopClosed = !_bIsShopClosed;
            CloseShop();
        }
    
        private void OpenShop()
        {
            ShopPanel.SetActive(true);
        }
    
        private void CloseShop()
        {
            ShopPanel.SetActive(false);
        }
    }
}
