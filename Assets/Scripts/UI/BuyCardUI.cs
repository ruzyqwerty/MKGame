using System;
using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class BuyCardUI : MonoBehaviour
{
    public int Cost;
    public int CardID;

    private GameManager _gameManager;
    
    public void BuyCard()
    {
        _gameManager.BuyCard(CardID, Cost);
    }

    private void Start()
    {
        _gameManager = FindObjectOfType<GameManager>();
    }
}
