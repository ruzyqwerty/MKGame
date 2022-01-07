using System;
using System.Collections;
using System.Collections.Generic;
using Auth;
using Infrastructure;
using LobbyAPI;
using Relay;
using UI.MainMenu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;

namespace Game
{
    /// <summary>
    /// Sets up and runs the entire sample.
    /// </summary>
    public class NetworkManager : MonoBehaviour, IReceiveMessages
    {
        private LocalGameState m_localGameState = new LocalGameState();
        private LobbyUser m_localUser;
        private LocalLobby m_localLobby;

        private LobbyServiceData m_lobbyServiceData = new LobbyServiceData();
        private LobbyContentHeartbeat m_lobbyContentHeartbeat = new LobbyContentHeartbeat();
        private RelayUtpSetup m_relaySetup;
        private RelayUtpClient m_relayClient;

        private MenuManager _menuManager;

        /// <summary>Rather than a setter, this is usable in-editor. It won't accept an enum, however.</summary>
        public void SetLobbyColorFilter(int color)
        {
            m_lobbyColorFilter = (LobbyColor)color;
        }

        private LobbyColor m_lobbyColorFilter;

        #region Setup

        private void Awake()
        {
            // Do some arbitrary operations to instantiate singletons.
#pragma warning disable IDE0059 // Unnecessary assignment of a value
            var unused = Locator.Get;
#pragma warning restore IDE0059
            
            NetworkManager[] gameManagers = FindObjectsOfType<NetworkManager>();
            if (gameManagers.Length <= 1)
            {
                Locator.Get.Provide(new Identity(OnAuthSignIn));
                Application.wantsToQuit += OnWantToQuit;
            }
        }

        private void Start()
        {
            NetworkManager[] gameManagers = FindObjectsOfType<NetworkManager>();
            if (gameManagers.Length > 1)
            {
                Destroy(gameObject);
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }

            m_localLobby = new LocalLobby { State = LobbyState.Lobby };
            m_localUser = new LobbyUser();
            m_localUser.DisplayName = "New Player";
            Locator.Get.Messenger.Subscribe(this);
        }

        private void OnAuthSignIn()
        {
            Debug.Log("Signed in.");
            m_localUser.ID = Locator.Get.Identity.GetSubIdentity(IIdentityType.Auth).GetContent("id");
            m_localUser.DisplayName = NameGenerator.GetName(m_localUser.ID);
            m_localLobby.AddPlayer(m_localUser); // The local LobbyUser object will be hooked into UI before the LocalLobby is populated during lobby join, so the LocalLobby must know about it already when that happens.
        }

        #endregion

        public (LocalGameState, LobbyUser, LocalLobby, LobbyServiceData) GetMenuManagerData()
        {
            return (m_localGameState, m_localUser, m_localLobby, m_lobbyServiceData);
        }

        /// <summary>
        /// Primarily used for UI elements to communicate state changes, this will receive messages from arbitrary providers for user interactions.
        /// </summary>
        public void OnReceiveMessage(MessageType type, object msg)
        {
            if (type == MessageType.CreateLobbyRequest)
            {
                var createLobbyData = (LocalLobby)msg;
                LobbyAsyncRequests.Instance.CreateLobbyAsync(createLobbyData.LobbyName, createLobbyData.MaxPlayerCount, createLobbyData.Private, m_localUser, (r) =>
                    {   ToLocalLobby.Convert(r, m_localLobby);
                        OnCreatedLobby();
                    },
                    OnFailedJoin);
            }
            else if (type == MessageType.JoinLobbyRequest)
            {
                LocalLobby.LobbyData lobbyInfo = (LocalLobby.LobbyData)msg;
                LobbyAsyncRequests.Instance.JoinLobbyAsync(lobbyInfo.LobbyID, lobbyInfo.LobbyCode, m_localUser, (r) =>
                    {   ToLocalLobby.Convert(r, m_localLobby);
                        OnJoinedLobby();
                    },
                    OnFailedJoin);
            }
            else if (type == MessageType.QueryLobbies)
            {
                m_lobbyServiceData.State = LobbyQueryState.Fetching;
                LobbyAsyncRequests.Instance.RetrieveLobbyListAsync(
                    qr => {
                        if (qr != null)
                            OnLobbiesQueried(ToLocalLobby.Convert(qr));
                    },
                    er => {
                        OnLobbyQueryFailed();
                    },
                    m_lobbyColorFilter);
            }
            else if (type == MessageType.QuickJoin)
            {
                LobbyAsyncRequests.Instance.QuickJoinLobbyAsync(m_localUser, m_lobbyColorFilter, (r) =>
                    {   ToLocalLobby.Convert(r, m_localLobby);
                        OnJoinedLobby();
                    },
                    OnFailedJoin);
            }
			else if (type == MessageType.RenameRequest)
            {
                string name = (string)msg;
                if (string.IsNullOrWhiteSpace(name))
                {
                    Locator.Get.Messenger.OnReceiveMessage(MessageType.DisplayErrorPopup, "Empty Name not allowed."); // Lobby error type, then HTTP error type.
                    return;
            	}
                m_localUser.DisplayName = (string)msg;
            }       
            else if (type == MessageType.ClientUserApproved)
            {   
                ConfirmApproval();
            }
            else if (type == MessageType.UserSetEmote)
            {   
                EmoteType emote = (EmoteType)msg;
                m_localUser.Emote = emote;
            }
            else if (type == MessageType.LobbyUserStatus)
            {   
                m_localUser.UserStatus = (UserStatus)msg;
            }
            else if (type == MessageType.StartCountdown)
            {   
                m_localLobby.State = LobbyState.CountDown;
            }
            else if (type == MessageType.CancelCountdown)
            {   
                m_localLobby.State = LobbyState.Lobby;
            }
            else if (type == MessageType.CompleteCountdown)
            {   
                if (m_relayClient is RelayUtpHost) 
                    (m_relayClient as RelayUtpHost)?.SendInGameState();
            }
            else if (type == MessageType.ChangeGameState)
            {   
                SetGameState((GameState)msg);
            }
            else if (type == MessageType.ConfirmInGameState)
            {   
                m_localUser.UserStatus = UserStatus.InGame;
                m_localLobby.State = LobbyState.InGame;

                SceneManager.LoadSceneAsync("Scenes/Game");
            }
            else if (type == MessageType.EndGame)
            {
                m_localLobby.State = LobbyState.Lobby;
                SetUserLobbyState();
            }
            else if (type == MessageType.SetPlayerOrder)
            {
                m_relayClient.SendMessage(((int) msg).ToString());
                m_localUser.PlayerOrder = (int) msg;
            }
        }

        private void SetGameState(GameState state)
        {
            bool isLeavingLobby = (state == GameState.Menu || state == GameState.JoinMenu) && m_localGameState.State == GameState.Lobby;
            m_localGameState.State = state;
            if (isLeavingLobby)
                OnLeftLobby();
        }

        private void OnLobbiesQueried(IEnumerable<LocalLobby> lobbies)
        {
            var newLobbyDict = new Dictionary<string, LocalLobby>();
            foreach (var lobby in lobbies)
                newLobbyDict.Add(lobby.LobbyID, lobby);

            m_lobbyServiceData.State = LobbyQueryState.Fetched;
            m_lobbyServiceData.CurrentLobbies = newLobbyDict;
        }

        private void OnLobbyQueryFailed()
        {
            m_lobbyServiceData.State = LobbyQueryState.Error;
        }

        private void OnCreatedLobby()
        {
            m_localUser.IsHost = true;
            OnJoinedLobby();
        }

        private void OnJoinedLobby()
        {
            LobbyAsyncRequests.Instance.BeginTracking(m_localLobby.LobbyID);
            m_lobbyContentHeartbeat.BeginTracking(m_localLobby, m_localUser);
            SetUserLobbyState();

            OnReceiveMessage(MessageType.LobbyUserStatus, UserStatus.Connecting);
            StartRelayConnection();
        }

        private void OnLeftLobby()
        {
            m_localUser.ResetState();
            LobbyAsyncRequests.Instance.LeaveLobbyAsync(m_localLobby.LobbyID, ResetLocalLobby);
            m_lobbyContentHeartbeat.EndTracking();
            LobbyAsyncRequests.Instance.EndTracking();

            if (m_relaySetup != null)
            {   
                Component.Destroy(m_relaySetup);
                m_relaySetup = null;
            }
            if (m_relayClient != null)
            {   
                Component.Destroy(m_relayClient);
                m_relayClient = null;
            }
        }

        /// <summary>
        /// Back to Join menu if we fail to join for whatever reason.
        /// </summary>
        private void OnFailedJoin()
        {
            SetGameState(GameState.JoinMenu);
        }

        private void StartRelayConnection()
        {
            if (m_localUser.IsHost)
                m_relaySetup = gameObject.AddComponent<RelayUtpSetupHost>();
            else
                m_relaySetup = gameObject.AddComponent<RelayUtpSetupClient>();
            m_relaySetup.BeginRelayJoin(m_localLobby, m_localUser, OnRelayConnected);

            void OnRelayConnected(bool didSucceed, RelayUtpClient client)
            {
                Component.Destroy(m_relaySetup);
                m_relaySetup = null;

                if (!didSucceed)
                {   Debug.LogError("Relay connection failed! Retrying in 5s...");
                    StartCoroutine(RetryConnection(StartRelayConnection, m_localLobby.LobbyID));
                    return;
                }

                m_relayClient = client;
                if (m_localUser.IsHost)
                {
                    CompleteRelayConnection();
                }
                else
                {
                    Debug.Log("Client is now waiting for approval...");
                }
            }
        }

        private IEnumerator RetryConnection(Action doConnection, string lobbyId)
        {
            yield return new WaitForSeconds(5);
            if (m_localLobby != null && m_localLobby.LobbyID == lobbyId && !string.IsNullOrEmpty(lobbyId)) // Ensure we didn't leave the lobby during this waiting period.
                doConnection?.Invoke();
        }

        private void ConfirmApproval()
        {
            if (!m_localUser.IsHost && m_localUser.IsApproved)
            {
                CompleteRelayConnection();
            }
        }

        private void CompleteRelayConnection()
        {
            OnReceiveMessage(MessageType.LobbyUserStatus, UserStatus.Lobby);
        }

        private void SetUserLobbyState()
        {
            SetGameState(GameState.Lobby);
            OnReceiveMessage(MessageType.LobbyUserStatus, UserStatus.Lobby);
        }

        private void ResetLocalLobby()
        {
            m_localLobby.CopyObserved(new LocalLobby.LobbyData(), new Dictionary<string, LobbyUser>());
            m_localLobby.AddPlayer(m_localUser); // As before, the local player will need to be plugged into UI before the lobby join actually happens.
            m_localLobby.RelayServer = null;
        }

        #region Teardown

        /// <summary>
        /// In builds, if we are in a lobby and try to send a Leave request on application quit, it won't go through if we're quitting on the same frame.
        /// So, we need to delay just briefly to let the request happen (though we don't need to wait for the result).
        /// </summary>
        private IEnumerator LeaveBeforeQuit()
        {
            ForceLeaveAttempt();
            yield return null;
            Application.Quit();
        }

        private bool OnWantToQuit()
        {
            bool canQuit = string.IsNullOrEmpty(m_localLobby?.LobbyID);
            StartCoroutine(LeaveBeforeQuit());
            return canQuit;
        }

        private void OnDestroy()
        {
            ForceLeaveAttempt();
        }

        private void ForceLeaveAttempt()
        {
            Locator.Get.Messenger.Unsubscribe(this);
            if (!string.IsNullOrEmpty(m_localLobby?.LobbyID))
            {
                LobbyAsyncRequests.Instance.LeaveLobbyAsync(m_localLobby?.LobbyID, null);
                m_localLobby = null;
            }
        }

        #endregion
    }
}
