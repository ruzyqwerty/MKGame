using System;
using System.Collections.Generic;
using Infrastructure;
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

        private int _money;

        public List<CardScript> Cards;
        // private List<~Sight~> Sights; // массив с картами достопримечательностями
        
        /* Local Variables */
        private UIGameController _uiGameController;

        private NetworkManager _networkManager;

        public void RollTheDice()
        {
            if (_activePlayerOrder != _networkManager.GetPlayerOrder())
            {
                return;
            }
            
            int dice = Random.Range(1, 7);
            _networkManager.SendRollResult(dice);
        }

        public void SetDiceResult(int dice)
        {
            _uiGameController.SetDebugText($"Игроку {_networkManager.GetPlayerNameByOrder(_activePlayerOrder)} выпало {dice}");
            foreach (var Card in Cards)
            {
                if (Card.rollResultToActivate == dice)
                {
                    int money = Card.TryToApplyCardEffect(_activePlayerOrder == _networkManager.GetPlayerOrder());
                    _money += money;
                    _uiGameController.SetMoney(_money);
                }
            }
            _networkManager.SendPlayerMoney(_money);
        }

        private void Start()
        {
            _uiGameController = GetComponent<UIGameController>();
            
            _money = 3;
            
            _uiGameController.SetMoney(_money);

            _activePlayerOrder = 0;

            GetNetworkManager();
        }

        private NetworkManager GetNetworkManager()
        {
            if (_networkManager is null)
            {
                _networkManager = FindObjectOfType<NetworkManager>();
                _networkManager.SetGameManager(this);
            }

            return _networkManager;
        }
    }
}
