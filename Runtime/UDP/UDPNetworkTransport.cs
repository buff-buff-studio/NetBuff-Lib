using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LiteNetLib;
using LiteNetLib.Utils;
using NetBuff.Discover;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;
using UnityEngine.Assertions;

namespace NetBuff.UDP
{
    /// <summary>
    ///  Uses the UDP (User Datagram Protocol) protocol to manage the connection between the server and clients.
    /// Responsible for internally managing the connection between the server and clients.
    /// Holds the connection information of clients and provides methods for sending and receiving packets.
    /// </summary>
    [Icon("Assets/Editor/Icons/UDPNetworkTransport.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/transports/#udp")]
    public class UDPNetworkTransport : NetworkTransport
    {
        private const int _BUFFER_SIZE = 65535;
        private static readonly byte[] _Buffer0 = new byte[_BUFFER_SIZE];
        private static readonly byte[] _Buffer1 = new byte[_BUFFER_SIZE];
        private static readonly BinaryWriter _Writer0 = new(new MemoryStream(_Buffer0));
        private static readonly BinaryWriter _Writer1 = new(new MemoryStream(_Buffer1));

        #region Inspector Fields
        [Header("SETTINGS")]
        [SerializeField]
        protected string address = "127.0.0.1";

        [SerializeField]
        protected int port = 7777;

        [SerializeField]
        protected string password = "";

        [SerializeField]
        protected int maxClients = 10;
        #endregion

        #region Internal Fields
        private UDPClient _client;
        private UDPServer _server;
        #endregion

        #region Helper Properties
        /// <summary>
        /// The address of the server.
        /// </summary>
        public string Address
        {
            get => address;
            set => address = value;
        }

        /// <summary>
        /// The port of the server / client connection.
        /// </summary>
        public int Port
        {
            get => port;
            set => port = value;
        }

        /// <summary>
        /// The password of the server.
        /// The password that the client is using to connect to the server.
        /// </summary>
        public string Password
        {
            get => password;
            set => password = value;
        }
        
        /// <summary>
        /// The maximum number of clients that can connect to the server at the same time.
        /// </summary>
        public int MaxClients
        {
            get => maxClients;
            set => maxClients = value;
        }
        #endregion

        #region Unity Callbacks
        private void Update()
        {
            _client?.Tick();
            _server?.Tick();
        }
        #endregion

        public override ServerDiscoverer GetServerDiscoverer()
        {
            return new UDPServerDiscoverer(NetworkManager.Instance.VersionMagicNumber, port);
        }

        public override void StartHost(int magicNumber)
        {
            StartServer();
            StartClient(magicNumber);
            Type = EnvironmentType.Host;
        }

        public override void StartServer()
        {
            if (_server != null)
                throw new Exception("Server already started");

            _server = new UDPServer(address, port, this, password, NetworkManager.Instance.Name, maxClients);
            Type = Type == EnvironmentType.None ? EnvironmentType.Server : EnvironmentType.Host;
            OnServerStart?.Invoke();
        }

        public override void StartClient(int magicNumber)
        {
            if (_client != null)
                throw new Exception("Client already started");

            _client = new UDPClient(magicNumber, address, port, this, password);
            Type = Type == EnvironmentType.None ? EnvironmentType.Client : EnvironmentType.Host;
        }

        public override void Close()
        {
            if (Type is EnvironmentType.Host or EnvironmentType.Server)
                OnServerStop?.Invoke();
            _client?.Close();
            _server?.Close();
            _client = null;
            _server = null;
            Type = EnvironmentType.None;
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

        public override void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            _client.SendPacket(packet, reliable);
        }

        public override void ServerSendPacket(IPacket packet, int target = -1, bool reliable = false)
        {
            _server.SendPacket(packet, target, reliable);
        }

        private static IEnumerable<ArraySegment<byte>> _ProcessQueue(Queue<IPacket> queue, int maxSize)
        {
            if (queue.Count == 0)
                yield break;

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

                yield return new ArraySegment<byte>(_Buffer0, 0, (int)_Writer0.BaseStream.Position);
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
                if (end + len > maxSize)
                {
                    yield return new ArraySegment<byte>(_Buffer0, 0, end);
                    end = 0;
                }

                Assert.IsTrue(end + len <= maxSize, $"Packet too large {packet}: {len}");
                Buffer.BlockCopy(_Buffer1, 0, _Buffer0, end, (int)len);
                end += (int)len;
            }

            if (end > 0)
                yield return new ArraySegment<byte>(_Buffer0, 0, end);
        }

        private static string _ToSnakeCase(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length < 2) return text.ToLowerInvariant();
            var sb = new StringBuilder();
            sb.Append(char.ToLowerInvariant(text[0]));
            for (var i = 1; i < text.Length; ++i)
            {
                var c = text[i];
                if (char.IsUpper(c))
                {
                    sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private class UDPClientConnectionInfo : IClientConnectionInfo
        {
            public readonly Queue<IPacket> queueReliable = new();
            public readonly Queue<IPacket> queueUnreliable = new();

            public UDPClientConnectionInfo(int id, NetPeer peer)
            {
                Id = id;
                Peer = peer;
            }

            public NetPeer Peer { get; }
            public string Address => Peer.Address.ToString();
            public int Id { get; }
            public int Latency { get; set; }
            public long PacketSent => Peer.Statistics.PacketsSent;
            public long PacketReceived => Peer.Statistics.PacketsReceived;
            public long PacketLoss => Peer.Statistics.PacketLoss;
        }

        private class UDPServer : INetEventListener
        {
            private readonly Dictionary<int, UDPClientConnectionInfo> _clients = new();
            private readonly int _maxClients;
            private readonly string _name;
            private readonly string _password;
            private readonly UDPNetworkTransport _transport;
            private NetManager _manager;

            public UDPServer(string address, int port, UDPNetworkTransport transport, string password, string name,
                int maxClients)
            {
                _password = password;
                _transport = transport;
                _maxClients = maxClients;

                _manager = new NetManager(this)
                {
                    EnableStatistics = true,
                    UpdateTime = 8,
                    UnconnectedMessagesEnabled = true
                };

                _manager.Start(IPAddress.Parse(address), IPAddress.IPv6Any, port);
                _name = name.Length > 32 ? name[..32] : name;
            }

            public void OnPeerConnected(NetPeer peer)
            {
                _clients.Add(peer.Id, new UDPClientConnectionInfo(peer.Id, peer));
                _transport.OnClientConnected?.Invoke(peer.Id);
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                _transport.OnClientDisconnected?.Invoke(peer.Id, _ToSnakeCase(disconnectInfo.Reason.ToString()));
                _clients.Remove(peer.Id);
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
            {
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber,
                DeliveryMethod deliveryMethod)
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

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
                UnconnectedMessageType messageType)
            {
                if (reader.GetString(50) == "server_search")
                {
                    var magicNumber = reader.GetInt();
                    if (NetworkManager.Instance.VersionMagicNumber != magicNumber)
                        return;

                    var hasPassword = !string.IsNullOrEmpty(_password);
                    var writer = new NetDataWriter();
                    writer.Put("server_answer");
                    writer.Put(_name);
                    writer.Put(_clients.Count);
                    writer.Put(_maxClients);
                    writer.Put((int)PlatformExtensions.GetPlatform());
                    writer.Put(hasPassword);
                    _manager.SendUnconnectedMessage(writer, remoteEndPoint);
                }
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
                _clients[peer.Id].Latency = latency;
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                var data = request.Data;
                var writer = new NetDataWriter();

                var magicNumber = data.GetInt();
                if (NetworkManager.Instance.VersionMagicNumber != magicNumber)
                {
                    writer.Put("wrong_magic_number");
                    request.Reject(writer);
                    return;
                }

                if (_clients.Count >= _maxClients)
                {
                    writer.Put("server_full");
                    request.Reject(writer);
                    return;
                }

                var hasPassword = !string.IsNullOrEmpty(_password);
                if (hasPassword)
                {
                    var password = data.GetString();
                    if (password != _password)
                    {
                        writer.Put("wrong_password");
                        request.Reject(writer);
                        return;
                    }
                }

                request.Accept();
            }

            public void Close()
            {
                _manager.DisconnectAll();
                _manager.Stop();
                _manager = null;
            }

            public void Tick()
            {
                _manager.PollEvents();

                foreach (var client in _clients)
                {
                    foreach (var data in _ProcessQueue(client.Value.queueReliable, -1))
                        client.Value.Peer.Send(data, DeliveryMethod.ReliableOrdered);
                    foreach (var data in _ProcessQueue(client.Value.queueUnreliable,
                                 client.Value.Peer.GetMaxSinglePacketSize(DeliveryMethod.Unreliable)))
                        client.Value.Peer.Send(data, DeliveryMethod.Unreliable);
                }
            }

            public void SendPacket(IPacket packet, int id = -1, bool reliable = false)
            {
                if (id == -1)
                {
                    foreach (var client in _clients)
                        if (reliable)
                            client.Value.queueReliable.Enqueue(packet);
                        else
                            client.Value.queueUnreliable.Enqueue(packet);
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
            private readonly UDPNetworkTransport _transport;
            private UDPClientConnectionInfo _clientConnectionInfo;
            private NetManager _manager;

            public UDPClient(int magicNumber, string address, int port, UDPNetworkTransport transport, string password)
            {
                _transport = transport;
                _manager = new NetManager(this)
                {
                    EnableStatistics = true,
                    UpdateTime = 8
                };
                _manager.Start();

                var writer = new NetDataWriter();
                writer.Put(magicNumber);
                writer.Put(password);
                _manager.Connect(address, port, writer);
            }

            public void OnPeerConnected(NetPeer peer)
            {
                _clientConnectionInfo = new UDPClientConnectionInfo(-1, peer);
                _transport.ClientConnectionInfo = _clientConnectionInfo;
                _transport.OnConnect?.Invoke();
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                var reason = _ToSnakeCase(disconnectInfo.Reason.ToString());
                if (disconnectInfo.AdditionalData.AvailableBytes > 0)
                {
                    var reader = disconnectInfo.AdditionalData.RawData;
                    reason = Encoding.UTF8.GetString(reader);
                }

                _transport.OnDisconnect?.Invoke(reason);
                _transport.ClientConnectionInfo = _clientConnectionInfo = null;
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
            {
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber,
                DeliveryMethod deliveryMethod)
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

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
                UnconnectedMessageType messageType)
            {
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
                _clientConnectionInfo.Latency = latency;
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.Accept();
            }

            public void Close()
            {
                if (_manager == null)
                    return;
                _manager.Stop();
                _manager = null;
                _transport.OnDisconnect?.Invoke("disconnected");
            }

            public void Tick()
            {
                _manager.PollEvents();

                if (_clientConnectionInfo != null)
                {
                    foreach (var data in _ProcessQueue(_clientConnectionInfo.queueReliable, -1))
                        _manager.FirstPeer.Send(data, DeliveryMethod.ReliableOrdered);
                    foreach (var data in _ProcessQueue(_clientConnectionInfo.queueUnreliable,
                                 _manager.FirstPeer.GetMaxSinglePacketSize(DeliveryMethod.Unreliable)))
                        _manager.FirstPeer.Send(data, DeliveryMethod.Unreliable);
                }
            }

            public void SendPacket(IPacket packet, bool reliable = false)
            {
                if (reliable)
                    _clientConnectionInfo.queueReliable.Enqueue(packet);
                else
                    _clientConnectionInfo.queueUnreliable.Enqueue(packet);
            }
        }
    }
}