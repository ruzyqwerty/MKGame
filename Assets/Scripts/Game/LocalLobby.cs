using System;
using System.Collections.Generic;
using Infrastructure;
using UnityEngine;

namespace Game
{
    [Flags] // Some UI elements will want to specify multiple states in which to be active, so this is Flags.
    public enum LobbyState
    {
        Lobby = 1,
        CountDown = 2,
        InGame = 4
    }

    public enum LobbyColor { None = 0, Orange = 1, Green = 2, Blue = 3 }

    /// <summary>
    /// A local wrapper around a lobby's remote data, with additional functionality for providing that data to UI elements and tracking local player objects.
    /// (The way that the Lobby service handles its data doesn't necessarily match our needs, so we need to map from that to this LocalLobby for use in the sample code.)
    /// </summary>
    [System.Serializable]
    public class LocalLobby : Observed<LocalLobby>
    {
        Dictionary<string, LobbyUser> m_LobbyUsers = new Dictionary<string, LobbyUser>();
        public Dictionary<string, LobbyUser> LobbyUsers => m_LobbyUsers;

        #region LocalLobbyData
        public struct LobbyData
        {
            public string LobbyID { get; set; }
            public string LobbyCode { get; set; }
            public string RelayCode { get; set; }
            public string LobbyName { get; set; }
            public bool Private { get; set; }
            public int MaxPlayerCount { get; set; }
            public LobbyState State { get; set; }
            public LobbyColor Color { get; set; }
            public long State_LastEdit { get; set; }
            public long Color_LastEdit { get; set; }

            public LobbyData(LobbyData existing)
            {
                LobbyID = existing.LobbyID;
                LobbyCode = existing.LobbyCode;
                RelayCode = existing.RelayCode;
                LobbyName = existing.LobbyName;
                Private = existing.Private;
                MaxPlayerCount = existing.MaxPlayerCount;
                State = existing.State;
                Color = existing.Color;
                State_LastEdit = existing.State_LastEdit;
                Color_LastEdit = existing.Color_LastEdit;
            }

            public LobbyData(string lobbyCode)
            {
                LobbyID = null;
                LobbyCode = lobbyCode;
                RelayCode = null;
                LobbyName = null;
                Private = false;
                MaxPlayerCount = -1;
                State = LobbyState.Lobby;
                Color = LobbyColor.None;
                State_LastEdit = 0;
                Color_LastEdit = 0;
            }
        }

        private LobbyData m_data;
        public LobbyData Data
        {
            get { return new LobbyData(m_data); }
        }

        ServerAddress m_relayServer;

        /// <summary>Used only for visual output of the Relay connection info. The obfuscated Relay server IP is obtained during allocation in the RelayUtpSetup.</summary>
        public ServerAddress RelayServer
        {
            get => m_relayServer;
            set
            {
                m_relayServer = value;
                OnChanged(this);
            }
        }

        #endregion

        public void AddPlayer(LobbyUser user)
        {
            if (m_LobbyUsers.ContainsKey(user.ID))
            {
                Debug.LogError($"Cant add player {user.DisplayName}({user.ID}) to lobby: {LobbyID} twice");
                return;
            }

            DoAddPlayer(user);
            OnChanged(this);
        }

        private void DoAddPlayer(LobbyUser user)
        {
            m_LobbyUsers.Add(user.ID, user);
            user.onChanged += OnChangedUser;
        }

        public void RemovePlayer(LobbyUser user)
        {
            DoRemoveUser(user);
            OnChanged(this);
        }

        private void DoRemoveUser(LobbyUser user)
        {
            if (!m_LobbyUsers.ContainsKey(user.ID))
            {
                Debug.LogWarning($"Player {user.DisplayName}({user.ID}) does not exist in lobby: {LobbyID}");
                return;
            }

            m_LobbyUsers.Remove(user.ID);
            user.onChanged -= OnChangedUser;
        }

        private void OnChangedUser(LobbyUser user)
        {
            OnChanged(this);
        }

        public string LobbyID
        {
            get => m_data.LobbyID;
            set
            {
                m_data.LobbyID = value;
                OnChanged(this);
            }
        }

        public string LobbyCode
        {
            get => m_data.LobbyCode;
            set
            {
                m_data.LobbyCode = value;
                OnChanged(this);
            }
        }

        public string RelayCode
        {
            get => m_data.RelayCode;
            set
            {
                m_data.RelayCode = value;
                OnChanged(this);
            }
        }

        public string LobbyName
        {
            get => m_data.LobbyName;
            set
            {
                m_data.LobbyName = value;
                OnChanged(this);
            }
        }

        public LobbyState State
        {
            get => m_data.State;
            set
            {
                m_data.State = value;
                m_data.State_LastEdit = DateTime.Now.Ticks;
                OnChanged(this);
            }
        }
        
        public bool Private
        {
            get => m_data.Private;
            set
            {
                m_data.Private = value;
                OnChanged(this);
            }
        }

        public int PlayerCount => m_LobbyUsers.Count;

        public int MaxPlayerCount
        {
            get => m_data.MaxPlayerCount;
            set
            {
                m_data.MaxPlayerCount = value;
                OnChanged(this);
            }
        }

        public LobbyColor Color
        {
            get => m_data.Color;
            set
            {
                if (m_data.Color != value)
                {   m_data.Color = value;
                    m_data.Color_LastEdit = DateTime.Now.Ticks;
                    OnChanged(this);
                }
            }
        }

        public void CopyObserved(LobbyData data, Dictionary<string, LobbyUser> currUsers)
        {
            // It's possible for the host to edit the lobby in between the time they last pushed lobby data and the time their pull for new lobby data completes.
            // If that happens, the edit will be lost, so instead we maintain the time of last edit to detect that case.
            var pendingState = data.State;
            var pendingColor = data.Color;
            if (m_data.State_LastEdit > data.State_LastEdit)
                pendingState = m_data.State;
            if (m_data.Color_LastEdit > data.Color_LastEdit)
                pendingColor = m_data.Color;
            m_data = data;
            m_data.State = pendingState;
            m_data.Color = pendingColor;

            if (currUsers == null)
                m_LobbyUsers = new Dictionary<string, LobbyUser>();
            else
            {
                List<LobbyUser> toRemove = new List<LobbyUser>();
                foreach (var oldUser in m_LobbyUsers)
                {
                    if (currUsers.ContainsKey(oldUser.Key))
                        oldUser.Value.CopyObserved(currUsers[oldUser.Key]);
                    else
                        toRemove.Add(oldUser.Value);
                }

                foreach (var remove in toRemove)
                {
                    DoRemoveUser(remove);
                }

                foreach (var currUser in currUsers)
                {
                    if (!m_LobbyUsers.ContainsKey(currUser.Key))
                        DoAddPlayer(currUser.Value);
                }
            }

            OnChanged(this);
        }

        // This ends up being called from the lobby list when we get data about a lobby without having joined it yet.
        public override void CopyObserved(LocalLobby oldObserved)
        {
            CopyObserved(oldObserved.Data, oldObserved.m_LobbyUsers);
        }
    }
}
