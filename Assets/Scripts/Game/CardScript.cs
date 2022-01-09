using System;
using UnityEngine;

namespace Game
{
    public enum CardEffect
    {
        None = 0,
        FromBank = 1,
        FromTarget = 2,
        FromActivePlayer = 3,
        FromPlayers = 4
    }

    public enum MoveType
    {
        None = 0,
        AnyMove = 1,
        YourMove = 2,
        EnemyMove = 3
    }

    public class CardScript : MonoBehaviour
    {
        public CardEffect effectType;
        public MoveType moveType;
        public int receivedMoney;
        public int rollResultToActivate;

        private GameManager _gameManager;

        public void OnClick()
        {
            Debug.Log("Clicked");
        }

        private void Start()
        {
            _gameManager = FindObjectOfType<GameManager>();
        }
        
        public int TryToApplyCardEffect(bool bYourTurn)
        {
            if (moveType != MoveType.AnyMove)
            {
                if (moveType == MoveType.YourMove && !bYourTurn)
                {
                    return 0;
                }
                if (moveType == MoveType.EnemyMove && bYourTurn)
                {
                    return 0;
                }
            }

            switch (effectType)
            {
                case CardEffect.FromBank:
                    Debug.LogError($"Get {receivedMoney} money from bank");
                    return receivedMoney;

                case CardEffect.FromTarget:
                    Debug.LogError($"Get {receivedMoney} money from target player (you should choose)");
                    break;
                
                case CardEffect.FromActivePlayer:
                    Debug.LogError($"Get {receivedMoney} money from active player");
                    break;
                
                case CardEffect.FromPlayers:
                    Debug.LogError($"Get {receivedMoney} money from all players");
                    break;
                
                case CardEffect.None:
                default:
                    Debug.LogError("No card effect bug");
                    break;
            }

            return 0;
        }
    }
}
