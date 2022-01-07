﻿using System.Collections.Generic;
using Game;
using Infrastructure;
using Unity.Networking.Transport;

namespace Relay
{
    /// <summary>
    /// In addition to maintaining a heartbeat with the Relay server to keep it from timing out, the host player must pass network events
    /// from clients to all other clients, since they don't connect to each other.
    /// </summary>
    public class RelayUtpHost : RelayUtpClient, IReceiveMessages
    {
        public override void Initialize(NetworkDriver networkDriver, List<NetworkConnection> connections, LobbyUser localUser, LocalLobby localLobby)
        {
            base.Initialize(networkDriver, connections, localUser, localLobby);
            m_hasSentInitialMessage = true; // The host will be alone in the lobby at first, so they need not send any messages right away.
            Locator.Get.Messenger.Subscribe(this);
        }

        protected override void Uninitialize()
        {
            base.Uninitialize();
            Locator.Get.Messenger.Unsubscribe(this);
        }

        protected override void OnUpdate()
        {
            if (!m_IsRelayConnected) // If Relay was disconnected somehow, stop taking actions that will keep the allocation alive.
                return;
            base.OnUpdate();
            UpdateConnections();
        }

        /// <summary>
        /// When a new client connects, first determine if they are allowed to do so.
        /// If so, they need to be updated with the current state of everyone else.
        /// If not, they should be informed and rejected.
        /// </summary>
        private void OnNewConnection(NetworkConnection conn, string id)
        {
            new RelayPendingApproval(conn, NewConnectionApprovalResult, id);
        }

        private void NewConnectionApprovalResult(NetworkConnection conn, Approval result)
        {
            WriteByte(m_networkDriver, conn, m_localUser.ID, MsgType.PlayerApprovalState, (byte)result);
            if (result == Approval.OK && conn.IsCreated)
            {
                foreach (var user in m_localLobby.LobbyUsers)
                    ForceFullUserUpdate(m_networkDriver, conn, user.Value);
                m_connections.Add(conn);
            }
            else
            {
                conn.Disconnect(m_networkDriver);
            }
        }

        protected override bool CanProcessDataEventFor(NetworkConnection conn, MsgType type, string id)
        {
            // Don't send through data from one client to everyone else if they haven't been approved yet. (They should also not be sending data if not approved, so this is a backup.)
            return id != m_localUser.ID && (m_localLobby.LobbyUsers.ContainsKey(id) && m_connections.Contains(conn) || type == MsgType.NewPlayer);
        }

        protected override void ProcessNetworkEventDataAdditional(NetworkConnection conn, MsgType msgType, string id)
        {
            // Forward messages from clients to other clients.
            if (msgType == MsgType.PlayerName)
            {
                string name = m_localLobby.LobbyUsers[id].DisplayName;
                foreach (NetworkConnection otherConn in m_connections)
                {
                    if (otherConn == conn)
                        continue;
                    WriteString(m_networkDriver, otherConn, id, msgType, name);
                }
            }
            else if (msgType == MsgType.Emote || msgType == MsgType.ReadyState)
            {
                byte value = msgType == MsgType.Emote ? (byte)m_localLobby.LobbyUsers[id].Emote : (byte)m_localLobby.LobbyUsers[id].UserStatus;
                foreach (NetworkConnection otherConn in m_connections)
                {
                    if (otherConn == conn)
                        continue;
                    WriteByte(m_networkDriver, otherConn, id, msgType, value);
                }
            }
            else if (msgType == MsgType.NewPlayer)
                OnNewConnection(conn, id);
            else if (msgType == MsgType.PlayerDisconnect) // Clients message the host when they intend to disconnect, or else the host ends up keeping the connection open.
            {
                conn.Disconnect(m_networkDriver);
                UnityEngine.Debug.LogWarning("Disconnecting a client due to a disconnect message.");
                return;
            }

            // If a client has changed state, check if this changes whether all players have readied.
            if (msgType == MsgType.ReadyState)
                CheckIfAllUsersReady();
        }

        protected override void ProcessDisconnectEvent(NetworkConnection conn, DataStreamReader strm)
        {
            // When a disconnect from the host occurs, no additional action is required. This override just prevents the base behavior from occurring.
            // TODO: If a client disconnects, see if remaining players are all already ready.
            UnityEngine.Debug.LogError("Client disconnected!");
        }

        public void OnReceiveMessage(MessageType type, object msg)
        {
            if (type == MessageType.LobbyUserStatus)
                CheckIfAllUsersReady();
            else if (type == MessageType.EndGame) // This assumes that only the host will have the End Game button available; otherwise, clients need to be able to send this message, too.
            {
                foreach (NetworkConnection connection in m_connections)
                    WriteByte(m_networkDriver, connection, m_localUser.ID, MsgType.EndInGame, 0);
            }
            //else if (type == MessageType.Start)
        }

        private void CheckIfAllUsersReady()
        {
            bool haveAllReadied = true;
            foreach (var user in m_localLobby.LobbyUsers)
            {
                if (user.Value.UserStatus != UserStatus.Ready)
                {   haveAllReadied = false;
                    break;
                }
            }
            if (haveAllReadied && m_localLobby.State == LobbyState.Lobby) // Need to notify both this client and all others that all players have readied.
            {
                Locator.Get.Messenger.OnReceiveMessage(MessageType.StartCountdown, null);
                foreach (NetworkConnection connection in m_connections)
                    WriteByte(m_networkDriver, connection, m_localUser.ID, MsgType.StartCountdown, 0);
            }
            else if (!haveAllReadied && m_localLobby.State == LobbyState.CountDown) // Someone cancelled during the countdown, so abort the countdown.
            {
                Locator.Get.Messenger.OnReceiveMessage(MessageType.CancelCountdown, null);
                foreach (NetworkConnection connection in m_connections)
                    WriteByte(m_networkDriver, connection, m_localUser.ID, MsgType.CancelCountdown, 0);
            }
        }

        /// <summary>
        /// In an actual game, after the countdown, there would be some step here where the host and all clients sync up on game state, load assets, etc.
        /// Here, we will instead just signal an "in-game" state that can be ended by the host.
        /// </summary>
        public void SendInGameState()
        {
            Locator.Get.Messenger.OnReceiveMessage(MessageType.ConfirmInGameState, null);
            foreach (NetworkConnection connection in m_connections)
                WriteByte(m_networkDriver, connection, m_localUser.ID, MsgType.ConfirmInGame, 0);
        }

        /// <summary>
        /// Clean out destroyed connections, and accept all new ones.
        /// </summary>
        private void UpdateConnections()
        {
            for (int c = m_connections.Count - 1; c >= 0; c--)
            {
                if (!m_connections[c].IsCreated)
                    m_connections.RemoveAt(c);
            }
            while (true)
            {
                var conn = m_networkDriver.Accept(); // Note that since we pumped the event queue earlier in Update, m_networkDriver has been updated already this frame.
                if (!conn.IsCreated) // "Nothing more to accept" is signalled by returning an invalid connection from Accept.
                    break;
                // Although the connection is created (i.e. Accepted), we still need to approve it, which will trigger when receiving the NewPlayer message from that client.
            }
        }

        public override void Leave()
        {
            foreach (NetworkConnection connection in m_connections)
                connection.Disconnect(m_networkDriver); // Note that Lobby won't receive the disconnect immediately, so its auto-disconnect takes 30-40s, if needed.
            m_connections.Clear();
            m_localLobby.RelayServer = null;
        }
    }
}
