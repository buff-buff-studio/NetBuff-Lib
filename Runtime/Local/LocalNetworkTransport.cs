using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetBuff.Interface;
using UnityEngine;

namespace NetBuff.Local
{
    /// <summary>
    /// The local network transport is a transport that is mainly used for split-screen games or local multiplayer games
    /// </summary>
    [Icon("Assets/Editor/Icons/LocalNetworkTransport.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/transports/#local")]
    public class LocalNetworkTransport : NetworkTransport
    {
        private Queue<Action> _dispatcher = new Queue<Action>();

        /// <summary>
        /// Represents the connection information of a client on the client side
        /// </summary>
        public class LocalClientConnectionInfo : IClientConnectionInfo
        {
            /// <summary>
            /// Current connection RTT (Round Trip Time) in milliseconds
            /// </summary>
            public int Latency => 0;
            
            /// <summary>
            /// Total packets sent to the client
            /// </summary>
            public long PacketSent => 0;
            
            /// <summary>
            /// total packets received from the client
            /// </summary>
            public long PacketReceived => 0;
            
            /// <summary>
            /// Total packets lost between the client and the server
            /// </summary>
            public long PacketLoss => 0;
            
            /// <summary>
            /// Local client remote id on server
            /// </summary>
            public int Id { get; }
            
            public LocalClientConnectionInfo(int id)
            {
                Id = id;
            }
        }
        
        private int _nextClientId;
        private readonly Dictionary<int, LocalClientConnectionInfo> _clients = new Dictionary<int, LocalClientConnectionInfo>();
        
        private void CreatePlayer()
        {
            var id = _nextClientId++;
            _clients[id] = new LocalClientConnectionInfo(id);
            OnConnect.Invoke();
            OnClientConnected.Invoke(id);
        }
        
        public override void StartHost(int magicNumber)
        {
            Type = EndType.Host;
            OnServerStart?.Invoke();
            CreatePlayer();
            CreateOtherPlayer();
        }
        
        private async void CreateOtherPlayer()
        {
            await Task.Delay(500);
            _dispatcher.Enqueue(CreatePlayer);
        }
        
        
        public override void StartServer()
        {
            Type = EndType.Server;
            OnServerStart?.Invoke();
        }

        public override void StartClient(int magicNumber)
        {
            if (Type == EndType.None)
                throw new Exception("Cannot start client without a host or server");
            
            Type = EndType.Host;
            CreatePlayer();
            CreateOtherPlayer();
        }

        public override void Close()
        {
            switch (Type)
            {
                case EndType.Host:
                    OnServerStop?.Invoke();
                    OnDisconnect?.Invoke("disconnect");
                    break;
                case EndType.Server:
                    OnDisconnect?.Invoke("disconnect");
                    break;
                case EndType.Client:
                    OnDisconnect?.Invoke("disconnect");
                    break;
                case EndType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override IClientConnectionInfo GetClientInfo(int id)
        {
            return _clients[id];
        }

        public override int GetClientCount()
        {
            return _clients.Count;
        }

        public override IEnumerable<IClientConnectionInfo> GetClients()
        {
           return _clients.Values;
        }

        public override void ClientDisconnect(string reason)
        {
            
        }

        public override void ServerDisconnect(int id, string reason)
        {
            
        }

        public override void SendClientPacket(IPacket packet, bool reliable = false)
        {
            _dispatcher.Enqueue(() => OnServerPacketReceived.Invoke(0, packet));
        }

        public override void SendServerPacket(IPacket packet, int target = -1, bool reliable = false)
        {
            _dispatcher.Enqueue(() => OnClientPacketReceived.Invoke(packet));
        }
        
        private void Update()
        {
            while (_dispatcher.Count > 0)
            {
                _dispatcher.Dequeue().Invoke();
            }
        }
    }
}