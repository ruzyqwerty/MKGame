using System;
using System.Collections.Generic;
using Infrastructure;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Current state of the user in the lobby.
    /// This is a Flags enum to allow for the Inspector to select multiples for various UI features.
    /// </summary>
    [Flags]
    public enum UserStatus
    {
        None = 0,
        Connecting = 1, // User has joined a lobby but has not yet connected to Relay.
        Lobby = 2,      // User is in a lobby and connected to Relay.
        Ready = 4,      // User has selected the ready button, to ready for the "game" to start.
        InGame = 8,     // User is part of a "game" that has started.
        Menu = 16       // User is not in a lobby, in one of the main menus.
    }

    /// <summary>
    /// Data for a local player instance. This will update data and is observed to know when to push local player changes to the entire lobby.
    /// </summary>
    [Serializable]
    public class LobbyUser : Observed<LobbyUser>
    {
        public LobbyUser(bool isHost = false, string displayName = null, string id = null,
            EmoteType emote = EmoteType.None, UserStatus userStatus = UserStatus.Menu, bool isApproved = false,
            int playerOrder = -1, int money = 0)
        {
            m_data = new UserData(isHost, displayName, id, emote, userStatus, isApproved, playerOrder, money);
        }

        #region Local UserData

        public struct UserData
        {
            public bool IsHost { get; set; }
            public string DisplayName { get; set; }
            public string ID { get; set; }
            public EmoteType Emote { get; set; }
            public UserStatus UserStatus { get; set; }
            public bool IsApproved { get; set; }
            public int Order { get; set; }
            public int Money { get; set; }

            public UserData(bool isHost, string displayName, string id, EmoteType emote, UserStatus userStatus, bool isApproved, int order, int money)
            {
                IsHost = isHost;
                DisplayName = displayName;
                ID = id;
                Emote = emote;
                UserStatus = userStatus;
                IsApproved = isApproved;
                Order = order;
                Money = money;
            }
        }

        private UserData m_data;

        public void ResetState()
        {
            m_data = new UserData(false, m_data.DisplayName, m_data.ID, EmoteType.None, UserStatus.Menu, false, -1, 0); // ID and DisplayName should persist since this might be the local user.
        }

        #endregion

        /// <summary>
        /// Used for limiting costly OnChanged actions to just the members which actually changed.
        /// </summary>
        [Flags]
        public enum UserMembers
        {
            IsHost = 1,
            DisplayName = 2,
            Emote = 4,
            ID = 8,
            UserStatus = 16,
            IsApproved = 32,
            Order = 33,
            Money = 34
        }

        private UserMembers m_lastChanged;
        public UserMembers LastChanged => m_lastChanged;

        public bool IsHost
        {
            get { return m_data.IsHost; }
            set
            {
                if (m_data.IsHost != value)
                {
                    m_data.IsHost = value;
                    m_lastChanged = UserMembers.IsHost;
                    OnChanged(this);
                    if (value)
                        IsApproved = true;
                }
            }
        }

        public string DisplayName
        {
            get => m_data.DisplayName;
            set
            {
                if (m_data.DisplayName != value)
                {
                    m_data.DisplayName = value;
                    m_lastChanged = UserMembers.DisplayName;
                    OnChanged(this);
                }
            }
        }

        public EmoteType Emote
        {
            get => m_data.Emote;
            set
            {
                if (m_data.Emote != value)
                {
                    m_data.Emote = value;
                    m_lastChanged = UserMembers.Emote;
                    OnChanged(this);
                }
            }
        }

        public string ID
        {
            get => m_data.ID;
            set
            {
                if (m_data.ID != value)
                {
                    m_data.ID = value;
                    m_lastChanged = UserMembers.ID;
                    OnChanged(this);
                }
            }
        }

        UserStatus m_userStatus = UserStatus.Menu;
        public UserStatus UserStatus
        {
            get => m_userStatus;
            set
            {
                if (m_userStatus != value)
                {
                    m_userStatus = value;
                    m_lastChanged = UserMembers.UserStatus;
                    OnChanged(this);
                }
            }
        }

        public bool IsApproved // Clients joining the lobby should be approved by the host before they can interact.
        {
            get => m_data.IsApproved;
            set
            {
                if (!m_data.IsApproved && value) // Don't be un-approved except by a call to ResetState.
                {
                    m_data.IsApproved = value;
                    m_lastChanged = UserMembers.IsApproved;
                    OnChanged(this);
                    Locator.Get.Messenger.OnReceiveMessage(MessageType.ClientUserApproved, null);
                }
            }
        }
        
        public int Order
        {
            get => m_data.Order;
            set
            {
                if (m_data.Order == -1 || value == -1)
                {
                    m_data.Order = value;
                    m_lastChanged = UserMembers.Order;
                    OnChanged(this);
                }
            }
        }

        public int Money
        {
            get => m_data.Money;
            set
            {
                if (m_data.Money != value)
                {
                    m_data.Money = value;
                    m_lastChanged = UserMembers.Money;
                    OnChanged(this);
                }
            }
        }

        public override void CopyObserved(LobbyUser observed)
        {
            UserData data = observed.m_data;
            int lastChanged = // Set flags just for the members that will be changed.
                (m_data.IsHost == data.IsHost ? 0 : (int) UserMembers.IsHost) |
                (m_data.DisplayName == data.DisplayName ? 0 : (int) UserMembers.DisplayName) |
                (m_data.ID == data.ID ? 0 : (int) UserMembers.ID) |
                (m_data.Emote == data.Emote ? 0 : (int) UserMembers.Emote) |
                (m_data.UserStatus == data.UserStatus ? 0 : (int) UserMembers.UserStatus) |
                (m_data.Order == data.Order ? 0 : (int) UserMembers.Order) |
                (m_data.Money == data.Money ? 0 : (int) UserMembers.Money);

            if (lastChanged == 0) // Ensure something actually changed.
                return;

            m_data = data;
            m_lastChanged = (UserMembers)lastChanged;

            OnChanged(this);
        }
    }
}
