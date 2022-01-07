using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

namespace UI.MainMenu
{
    public class MenuManager : MonoBehaviour
    {
        #region UI elements that observe the local state. These should be assigned the observers in the scene during Start.

        [SerializeField]
        private List<LocalGameStateObserver> m_GameStateObservers = new List<LocalGameStateObserver>();
        [SerializeField]
        private List<LocalLobbyObserver> m_LocalLobbyObservers = new List<LocalLobbyObserver>();
        [SerializeField]
        private List<LobbyUserObserver> m_LocalUserObservers = new List<LobbyUserObserver>();
        [SerializeField]
        private List<LobbyServiceDataObserver> m_LobbyServiceObservers = new List<LobbyServiceDataObserver>();

        #endregion

        private LocalGameState m_localGameState;
        private LobbyUser m_localUser;
        private LocalLobby m_localLobby;
        private LobbyServiceData m_lobbyServiceData;

        private bool _bIsObservingStarted = false;

        private void Start()
        {
            if (m_localGameState == null)
                return;
            
            if (m_localUser == null)
                return;
            
            if (m_localLobby == null)
                return;
            
            if (m_lobbyServiceData == null)
                return;
            
            BeginObservers();
        }

        private void Update()
        {
            Game.GameManager gameManager = FindObjectOfType<Game.GameManager>();
            if (gameManager != null)
            {
                var (localGameState, localUser, localLobby, lobbyServiceData) = gameManager.GetMenuManagerData();
                
                m_localGameState = localGameState; 
                m_localUser = localUser;
                m_localLobby = localLobby;
                m_lobbyServiceData = lobbyServiceData;

                if (!_bIsObservingStarted)
                    BeginObservers();
            }
        }

        private void BeginObservers()
        {
            foreach (var gameStateObs in m_GameStateObservers)
                gameStateObs.BeginObserving(m_localGameState);
            foreach (var serviceObs in m_LobbyServiceObservers)
                serviceObs.BeginObserving(m_lobbyServiceData);
            foreach (var lobbyObs in m_LocalLobbyObservers)
                lobbyObs.BeginObserving(m_localLobby);
            foreach (var userObs in m_LocalUserObservers)
                userObs.BeginObserving(m_localUser);

            _bIsObservingStarted = true;
        }
    }
}
