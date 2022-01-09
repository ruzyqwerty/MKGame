using System;
using TMPro;
using UI;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Game
{
    public class GameManager : MonoBehaviour
    {
        /* Server Sync Variables */
        private int _activePlayerOrder;
        
        // private List<~Card~> Cards; // массив с картами предприятия
        // private List<~Sight~> Sights; // массив с картами достопримечательностями
        
        /* Local Variables */
        private UIGameController _uiGameController;

        private void Start()
        {
            _uiGameController = GetComponent<UIGameController>();
            
            _uiGameController.SetMoney(3);

            _activePlayerOrder = 0;
        }

        public void RollTheDice()
        {
            /* if (your_order != _activePlayerOrder) return;*/ 
            
            int dice = Random.Range(1, 7);
            Debug.Log($"Rolled dice = {dice}");
        }
    }
}
