using System;
using System.Collections.Generic;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff
{
    /// <summary>
    /// Base class for network transport
    /// </summary>
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/transports")]
    public abstract class NetworkTransport : MonoBehaviour
    {
        public enum ConnectionResponseStatus
        {
            Ok,
            Error
        }
        
        public enum EndType
        {
            None,
            Server, 
            Host,
            Client
        }
        
        /// <summary>
        /// Returns the type of the end. It can be Server, Host or Client
        /// </summary>
        public EndType Type { get; protected set; } = EndType.None;
        
        /// <summary>
        /// Returns current local client connection info
        /// </summary>
        public IConnectionInfo ClientConnectionInfo { get; protected set; }
        
        /// <summary>
        /// Used to set / get the name of local server
        /// </summary>
        public string Name { get => name; set => name = value; }

        [SerializeField]
        private new string name = "server";

        #region Callbacks
        /// <summary>
        /// Called when a packet is received on server side
        /// </summary>
        public Action<int, IPacket> OnServerPacketReceived { get; set; }
        /// <summary>
        /// Called when a packet is received on client side
        /// </summary>
        public Action<IPacket> OnClientPacketReceived { get; set; }
        /// <summary>
        /// Called when a client is connected to server
        /// </summary>
        public Action<int> OnClientConnected { get; set; }
        /// <summary>
        /// Called when a client is disconnected from server
        /// </summary>
        public Action<int, string> OnClientDisconnected { get; set; }
        /// <summary>
        /// Called on client side on connect
        /// </summary>
        public Action OnConnect { get; set; }
        /// <summary>
        /// Called on client side on disconnect
        /// </summary>
        public Action<string> OnDisconnect { get; set; }
        
        /// <summary>
        /// Called when server is started
        /// </summary>
        public Action OnServerStart { get; set; }
        
        /// <summary>
        /// Called when server is stopped
        /// </summary>
        public Action OnServerStop { get; set; }
        #endregion

        #region ManagementMethods
        /// <summary>
        /// Starts the server and client
        /// </summary>
        /// <param name="magicNumber"></param>
        public abstract void StartHost(int magicNumber);
        
        /// <summary>
        /// Starts the server
        /// </summary>
        public abstract void StartServer();
        
        /// <summary>
        /// Starts the client
        /// </summary>
        /// <param name="magicNumber"></param>
        public abstract void StartClient(int magicNumber);
        
        /// <summary>
        /// Close the server and/or client
        /// </summary>
        public abstract void Close();
        #endregion

        #region Client Methods
        /// <summary>
        /// Returns the client connection info by id (Server side)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ServerOnly]
        public abstract IClientConnectionInfo GetClientInfo(int id);
        
        /// <summary>
        /// Returns the number of clients connected to server
        /// </summary>
        /// <returns></returns>
        [ServerOnly]
        public abstract int GetClientCount();
        
        /// <summary>
        /// Returns the list of clients connected to server
        /// </summary>
        /// <returns></returns>
        [ServerOnly]
        public abstract IEnumerable<IClientConnectionInfo> GetClients();
        #endregion

        #region Lifecycle
        /// <summary>
        /// Disconnects the client from server
        /// </summary>
        /// <param name="reason"></param>
        [ClientOnly]
        public abstract void ClientDisconnect(string reason);
        
        /// <summary>
        /// Disconnects the client from server
        /// </summary>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        [ServerOnly]
        public abstract void ServerDisconnect(int id, string reason);
        
        /// <summary>
        /// Sends a packet to the server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        public abstract void SendClientPacket(IPacket packet, bool reliable = false);
        
        /// <summary>
        /// Sends a packet to a client / broadcast to all clients
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="target"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public abstract void SendServerPacket(IPacket packet, int target = -1, bool reliable = false);
        
        /// <summary>
        /// Broadcasts a packet to all clients
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            SendServerPacket(packet, -1, reliable);
        }
        #endregion
    }
}