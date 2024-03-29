﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetBuff.Interface;
using UnityEngine;

namespace NetBuff.Local
{
    public class LocalNetworkTransport : NetworkTransport
    {
        private Queue<Action> _dispatcher = new Queue<Action>();

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
        
        private int _nextClientId = 0;
        private readonly Dictionary<int, LocalClientConnectionInfo> _clients = new Dictionary<int, LocalClientConnectionInfo>();
        
        private void CreatePlayer()
        {
            var id = _nextClientId++;
            _clients[id] = new LocalClientConnectionInfo(id);
            OnConnect.Invoke();
            OnClientConnected.Invoke(id);
            
        }
        
        
        public override void StartHost()
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

        public override void StartClient()
        {
            if (Type == EndType.None)
                throw new Exception("Cannot start client without a host or server");
            
            Type = EndType.Host;
            CreatePlayer();
            CreateOtherPlayer();;
        }

        public override void Close()
        {
            switch (Type)
            {
                case EndType.Host:
                    OnServerStop?.Invoke();
                    OnDisconnect?.Invoke();
                    break;
                case EndType.Server:
                    OnDisconnect?.Invoke();
                    break;
                case EndType.Client:
                    OnDisconnect?.Invoke();
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
            throw new System.NotImplementedException();
        }

        public override void ServerDisconnect(int id, string reason)
        {
            throw new System.NotImplementedException();   
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