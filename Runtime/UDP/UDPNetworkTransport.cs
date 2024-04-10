
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LiteNetLib;
using LiteNetLib.Utils;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;
using UnityEngine.Assertions;

namespace NetBuff.UDP
{
    public class UDPNetworkTransport : NetworkTransport
    {
        private const int _BUFFER_SIZE = 65535;
        private static readonly byte[] _Buffer0 = new byte[_BUFFER_SIZE];
        private static readonly byte[] _Buffer1 = new byte[_BUFFER_SIZE];
        private static readonly BinaryWriter _Writer0 = new(new MemoryStream(_Buffer0));
        private static readonly BinaryWriter _Writer1 = new(new MemoryStream(_Buffer1));
        
        private class UDPClientInfo : IClientConnectionInfo
        {
            public int Id { get;}
            public int Latency { get; set;}
            public NetPeer Peer { get;}
            
            public long PacketSent => Peer.Statistics.PacketsSent;
            public long PacketReceived => Peer.Statistics.PacketsReceived;
            public long PacketLoss => Peer.Statistics.PacketLoss;

            public UDPClientInfo(int id, NetPeer peer)
            {
                Id = id;
                Peer = peer;
            }
            
            public readonly Queue<IPacket> queueReliable = new Queue<IPacket>();
            public readonly Queue<IPacket> queueUnreliable = new Queue<IPacket>();
        }
        
        [Header("SETTINGS")]
        public string address = "127.0.0.1";
        public int port = 7777;
        public string password = "";
        
        private UDPClient _client;
        private UDPServer _server;
        
        public override void StartHost()
        {
            StartServer();
            StartClient();
            Type = EndType.Host;
        }

        public override void StartServer()
        {
            if (_server != null)
                return;
            
            _server = new UDPServer(address, port, this, password, Name);
            Type = Type == EndType.None ? EndType.Server : EndType.Host;
            OnServerStart?.Invoke();
        }

        public override void StartClient()
        {
            if (_client != null)
                return;
            
            _client = new UDPClient(address, port, this);
            Type = Type == EndType.None ? EndType.Client : EndType.Host;
        }

        public override void Close()
        {
            if(Type is EndType.Host or EndType.Server)
                OnServerStop?.Invoke();
            _client?.Close();
            _server?.Close();
            _client = null;
            _server = null;
            Type = EndType.None;
        }

        public override IClientConnectionInfo GetClientInfo(int id)
        {
            return _server.GetClientInfo(id);
        }

        public override int GetClientCount()
        {
            return _server.GetClientCount();
        }

        public override IEnumerable<IClientConnectionInfo> GetClients()
        {
            return _server.GetClients();
        }

        public override void ClientDisconnect(string reason)
        {
            _client.Close();
        }

        public override void ServerDisconnect(int id, string reason)
        {
            _server.Disconnect(id, reason);
        }

        public override void SendClientPacket(IPacket packet, bool reliable = false)
        {
            _client.SendPacket(packet, reliable);
        }

        public override void SendServerPacket(IPacket packet, int target = -1, bool reliable = false)
        {
            _server.SendPacket(packet, target, reliable);
        }
        
        void Update()
        {
            if (_client != null)
                _client.Tick();
            if (_server != null)
                _server.Tick();
        }
        
        private static IEnumerable<ArraySegment<byte>> ProcessQueue(Queue<IPacket> queue, int maxSize)
        {
            if (queue.Count == 0)
                yield break;
            
            // If maxSize is -1, we don't need to worry about packet size
            if (maxSize == -1)
            {
                _Writer0.BaseStream.Position = 0;
                while (queue.Count > 0)
                {
                    var packet = queue.Dequeue();
                    var id = PacketRegistry.GetId(packet);
                    _Writer0.Write(id);
                    packet.Serialize(_Writer0);
                }
                
                yield return new ArraySegment<byte>(_Buffer0, 0, (int) _Writer0.BaseStream.Position);
                yield break;
            }
            
            var end = 0;
            while (queue.Count > 0)
            {
                _Writer1.BaseStream.Position = 0;
                var packet = queue.Dequeue();
                var id = PacketRegistry.GetId(packet);
                _Writer1.Write(id);
                packet.Serialize(_Writer1);
                
                var len = _Writer1.BaseStream.Position;
                if(end + len > maxSize)
                {
                    yield return new ArraySegment<byte>(_Buffer0, 0, end);
                    end = 0;
                }
                
                Assert.IsTrue(end + len <= maxSize, $"Packet too large {packet}: {len}");
                Buffer.BlockCopy(_Buffer1, 0, _Buffer0, end, (int) len);
                end += (int) len;
            }
            
            if (end > 0)
                yield return new ArraySegment<byte>(_Buffer0, 0, end);
        }
        
        private class UDPServer : INetEventListener
        {
            private NetManager _manager;
            private readonly Dictionary<int, UDPClientInfo> _clients = new Dictionary<int, UDPClientInfo>();
            private readonly UDPNetworkTransport _transport;
            private readonly int _maxClients = 2;
            private readonly string _password;
            private readonly string _name;
            public UDPServer(string address, int port, UDPNetworkTransport transport, string password, string name)
            {
                _password = password;
                _transport = transport;
                
                _manager = new NetManager(this)
                {
                    EnableStatistics = true,
                    UpdateTime = 8,
                    UnconnectedMessagesEnabled = true
                };
                _manager.Start(IPAddress.Parse(address), IPAddress.IPv6Any, port);
                
                _name = name.Length > 32 ? name[..32] : name;
            }
            
            public void Close()
            {
                _manager.DisconnectAll();
                _manager.Stop();
                _manager = null;
            }

            public void OnPeerConnected(NetPeer peer)
            {
                _clients.Add(peer.Id, new UDPClientInfo(peer.Id, peer));
                _transport.OnClientConnected?.Invoke(peer.Id);
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                _transport.OnClientDisconnected?.Invoke(peer.Id);
                _clients.Remove(peer.Id);
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
            {
                Debug.LogError("[SERVER] Error: " + socketError);
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
            {
                var binaryReader = new BinaryReader(new MemoryStream(reader.GetRemainingBytes()));
            
                while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                {
                    var id = binaryReader.ReadInt32();
                    var packet = PacketRegistry.CreatePacket(id);
                    packet.Deserialize(binaryReader);
                    _transport.OnServerPacketReceived?.Invoke(peer.Id, packet);
                }
            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {
                try
                {
                    if (reader.GetString(50) == "server_search")
                    {
                        var hasPassword = !string.IsNullOrEmpty(_password);
                        var writer = new NetDataWriter();
                        writer.Put("server_answer");
                        writer.Put(_name);
                        writer.Put(_clients.Count); //player count
                        writer.Put(_maxClients); //player max count
                        writer.Put((int) PlatformExtensions.GetPlatform());
                        writer.Put(hasPassword);
                        _manager.SendUnconnectedMessage(writer, remoteEndPoint);
                    }
                }
                catch(Exception e)
                {
                    Debug.LogError(e);
                }
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
                _clients[peer.Id].Latency = latency;
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.Accept();
            }

            public void Tick()
            {
                _manager.PollEvents();

                foreach (var client in _clients)
                {
                    foreach (var data in ProcessQueue(client.Value.queueReliable, -1))
                        client.Value.Peer.Send(data, DeliveryMethod.ReliableOrdered);
                    foreach (var data in ProcessQueue(client.Value.queueUnreliable, client.Value.Peer.GetMaxSinglePacketSize(DeliveryMethod.Unreliable)))
                        client.Value.Peer.Send(data, DeliveryMethod.Unreliable);
                }
            }
            
            public void SendPacket(IPacket packet, int id = -1, bool reliable = false)
            {
                if (id == -1)
                {
                    foreach (var client in _clients)
                    {
                        if (reliable)
                            client.Value.queueReliable.Enqueue(packet);
                        else
                            client.Value.queueUnreliable.Enqueue(packet);
                    }
                }
                else 
                {
                    if (reliable)
                        _clients[id].queueReliable.Enqueue(packet);
                    else
                        _clients[id].queueUnreliable.Enqueue(packet);
                }
            }

            public void Disconnect(int id, string reason)
            {
                _clients[id].Peer.Disconnect(Encoding.UTF8.GetBytes(reason));
            }

            public IClientConnectionInfo GetClientInfo(int id)
            {
                return _clients.GetValueOrDefault(id);
            }
            
            public int GetClientCount()
            {
                return _clients.Count;
            }

            public IEnumerable<IClientConnectionInfo> GetClients()
            {
                return _clients.Values;
            }
        }

        private class UDPClient : INetEventListener
        {
            private NetManager _manager;
            private UDPClientInfo _clientInfo;
            private readonly UDPNetworkTransport _transport;
            
            public UDPClient(string address, int port, UDPNetworkTransport transport)
            {
                _transport = transport;
                _manager = new NetManager(this)
                {
                    EnableStatistics = true,
                    UpdateTime = 8
                };
                _manager.Start();
                _manager.Connect(address, port, "internal_key");
            }
            
            public void Close()
            {
                _manager.Stop();
                _manager = null;
                _transport.OnDisconnect?.Invoke();
            }
            
            public void OnPeerConnected(NetPeer peer)
            {
                _clientInfo = new UDPClientInfo(-1, peer);
                _transport.ClientConnectionInfo = _clientInfo;
                _transport.OnConnect?.Invoke();
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {  
                _transport.OnDisconnect?.Invoke();
                _transport.ClientConnectionInfo = _clientInfo = null;
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
            {
                Debug.LogError("[CLIENT] Error: " + socketError);
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
            {
                var binaryReader = new BinaryReader(new MemoryStream(reader.GetRemainingBytes()));
            
                while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                {
                    var id = binaryReader.ReadInt32();
                    var packet = PacketRegistry.CreatePacket(id);
                    Assert.IsNotNull(packet, $"Packet with id {id} not found");
                    packet.Deserialize(binaryReader);
                    _transport.OnClientPacketReceived?.Invoke(packet);
                }
            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {
                Debug.LogError("[CLIENT] Unconnected message received");
                //TODO: Implement
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
                _clientInfo.Latency = latency;
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.Accept();
            }

            public void Tick()
            {
                _manager.PollEvents();
                
                if (_clientInfo != null)
                {
                    foreach (var data in ProcessQueue(_clientInfo.queueReliable, -1))
                        _manager.FirstPeer.Send(data, DeliveryMethod.ReliableOrdered);
                    foreach (var data in ProcessQueue(_clientInfo.queueUnreliable, _manager.FirstPeer.GetMaxSinglePacketSize(DeliveryMethod.Unreliable)))
                        _manager.FirstPeer.Send(data, DeliveryMethod.Unreliable);
                }
            }
            
            public void SendPacket(IPacket packet, bool reliable = false)
            {
                if (reliable)
                    _clientInfo.queueReliable.Enqueue(packet);
                else
                    _clientInfo.queueUnreliable.Enqueue(packet);
            }
        }
    }
}