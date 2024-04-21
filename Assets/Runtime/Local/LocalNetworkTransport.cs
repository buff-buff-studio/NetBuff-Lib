using System;
using System.Collections.Generic;
using NetBuff.Discover;
using NetBuff.Interface;
using NetBuff.Packets;
using UnityEngine;

namespace NetBuff.Local
{
    [Icon("Assets/Editor/Icons/LocalNetworkTransport.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/transports/#local")]
    public class LocalNetworkTransport : NetworkTransport
    {
        private readonly Queue<Action> _dispatcher = new();

        public class LocalClientConnectionInfo : IClientConnectionInfo
        {
            public int Latency => 0;

            public long PacketSent => 0;
            
            public long PacketReceived => 0;
            
            public long PacketLoss => 0;
            
            public int Id { get; }
            
            public LocalClientConnectionInfo(int id)
            {
                Id = id;
            }
        }

        #region Inspector Fields
        [SerializeField] 
        protected int clientCount = 1;
        #endregion
       
        #region Internal Fields
        private int _loadedClients = 0;
        private int _nextClientId;
        private readonly Dictionary<int, LocalClientConnectionInfo> _clients = new Dictionary<int, LocalClientConnectionInfo>();
        #endregion

        #region Helper Properties
        public int ClientCount
        {
            get => clientCount;
            set
            {
                if(Type is EndType.Client or EndType.Host)
                    throw new Exception("Cannot change client count while clients are running");
                
                clientCount = value;
            }
        }
        #endregion
        
        private void CreatePlayers()
        {
            for (var i = 0; i < clientCount; i++)
            {
                var id = _nextClientId++;
                _clients[id] = new LocalClientConnectionInfo(id);
                OnConnect.Invoke();
                OnClientConnected.Invoke(id);
            }
        }

        public override ServerDiscover GetServerDiscoverer()
        {
            return null;
        }

        public override void StartHost(int magicNumber)
        {
            if(clientCount == 0)
                throw new Exception("Client count is 0");
                
            Type = EndType.Host;
            OnServerStart?.Invoke();
            
            CreatePlayers();
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
            
            if(clientCount == 0)
                throw new Exception("Client count is 0");
            
            Type = EndType.Host;

            CreatePlayers();
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
            if (packet is NetworkPreExistingResponsePacket)
            {
                var curr = _loadedClients++;
                _dispatcher.Enqueue(() => OnServerPacketReceived.Invoke(curr, packet));
                return;
            }

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