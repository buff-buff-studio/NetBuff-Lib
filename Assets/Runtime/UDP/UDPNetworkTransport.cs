﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NetBuff.Discover;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;
using UnityEngine.Assertions;

namespace NetBuff.UDP
{
    /// <summary>
    ///     Uses the UDP (User Datagram Protocol) protocol to manage the connection between the server and clients.
    ///     Responsible for internally managing the connection between the server and clients.
    ///     Holds the connection information of clients and provides methods for sending and receiving packets.
    /// </summary>
    [Icon("Assets/Editor/Icons/UDPNetworkTransport.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/transports/#udp")]
    public class UDPNetworkTransport : NetworkTransport
    {
        #region Unity Callbacks
        private void Update()
        {
            if (_client != null)
            {
                _client.PoolEvents();
                if (_client != null)
                {
                    foreach (var data in _ProcessQueue(_clientQueueReliable, -1))
                        _client.SendPacketReliable(_PACKET_RELIABLE_MESSAGE, data);
                    foreach (var data in _ProcessQueue(_clientQueueUnreliable, _MAX_PACKET_SIZE))
                        _client.SendPacketUnreliable(data);
                }
            }

            if (_server == null) return;
            {
                _server.PoolEvents();
                if (_server != null)
                {
                    foreach (var client in _clients.Values)
                    {
                        foreach (var data in _ProcessQueue(client.queueReliable, -1))
                            _server.SendPacketReliable(_PACKET_RELIABLE_MESSAGE, data, client.Id);
                        foreach (var data in _ProcessQueue(client.queueUnreliable, _MAX_PACKET_SIZE))
                            _server.SendPacketUnreliable(data, client.Id);
                    }
                }
            }
        }
        #endregion

        public override ServerDiscoverer GetServerDiscoverer()
        {
            return new UDPServerDiscoverer(NetworkManager.Instance.VersionMagicNumber, port);
        }

        public override void StartHost(int magicNumber)
        {
            _StartServer(true);
        }

        public override void StartServer()
        {
            _StartServer(false);
        }

        public override void StartClient(int magicNumber)
        {
            if (_client != null)
                throw new Exception("Client already started");

            _client = new UDPClient();

            _client.onConnected += () =>
            {
                ClientConnectionInfo = new LocalUDPClientConnectionInfo(-1, _client);
                OnConnect?.Invoke();
            };

            _client.onDisconnected += reason =>
            {
                OnDisconnect?.Invoke(ConnectionEndMode.Shutdown, reason);
                _client = null;
            };

            _client.onPacketReceived += data =>
            {
                var binaryReader = new BinaryReader(new MemoryStream(data.data, data.offset, data.length));

                while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                {
                    var id = binaryReader.ReadInt32();
                    var packet = PacketRegistry.CreatePacket(id);

                    packet.Deserialize(binaryReader);
                    OnClientPacketReceived?.Invoke(packet);
                }
            };

            _client.onError += reason =>
            {
                OnDisconnect?.Invoke(ConnectionEndMode.InternalError, reason);
                _client = null;
            };

            var writer = new BinaryWriter(new MemoryStream());
            writer.Write(password ?? "");
            try
            {
                _client.Connect(IPAddress.Parse(address), port, ((MemoryStream)writer.BaseStream).ToArray());
                Type = Type == EnvironmentType.None ? EnvironmentType.Client : EnvironmentType.Host;
            }
            catch (Exception e)
            {
                OnDisconnect?.Invoke(ConnectionEndMode.InternalError, e.Message);
                _client = null;
            }
        }

        public override void Close()
        {
            _server?.Close();
            _client?.Disconnect("disconnect");

            Type = EnvironmentType.None;

            _server = null;
            _client = null;
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
            _client?.Disconnect(reason);
        }

        public override void ServerDisconnect(int id, string reason)
        {
            _server?.DisconnectClient(id, reason);
        }

        public override void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            if (reliable)
                _clientQueueReliable.Enqueue(packet);
            else
                _clientQueueUnreliable.Enqueue(packet);
        }

        public override void ServerSendPacket(IPacket packet, int target = -1, bool reliable = false)
        {
            if (target == -1)
            {
                foreach (var client in _clients.Values)
                    if (reliable)
                        client.queueReliable.Enqueue(packet);
                    else
                        client.queueUnreliable.Enqueue(packet);
            }
            else
            {
                if (reliable)
                    _clients[target].queueReliable.Enqueue(packet);
                else
                    _clients[target].queueUnreliable.Enqueue(packet);
            }
        }

        #region Const Fields
        private const int _MAX_PACKET_SIZE = 1010;
        private const int _TIMEOUT_TIME = 5 * 1_000 * 10_000;
        private const int _RESEND_TIME = 25 * 10_000;
        private const int _RESEND_MAX_PER_FRAME = 5;
        private const int _KEEP_ALIVE_TIME = 500 * 10_000;

        private const byte _CHANNEL_UNRELIABLE = 1;
        private const byte _CHANNEL_RELIABLE = 2;
        private const byte _CHANNEL_RELIABLE_FRAGMENT_HEADER = 3;
        private const byte _CHANNEL_ACK = 4;
        private const byte _CHANNEL_KEEP_ALIVE = 5;
        private const byte _CHANNEL_SERVER_INFO = 7;

        private const byte _PACKET_RELIABLE_CONNECTION_REQUEST = 1;
        private const byte _PACKET_RELIABLE_CONNECTION_RESPONSE = 2;
        private const byte _PACKET_RELIABLE_DISCONNECT = 3;
        private const byte _PACKET_RELIABLE_MESSAGE = 10;
        #endregion

        #region Buffers
        private static readonly byte[] _FragmentUnitBuffer = new byte[65535];
        private static readonly byte[] _Buffer0 = new byte[65535];
        private static readonly byte[] _Buffer1 = new byte[65535];
        private static readonly BinaryWriter _Writer0 = new(new MemoryStream(_Buffer0));
        private static readonly BinaryWriter _Writer1 = new(new MemoryStream(_Buffer1));
        #endregion

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

        private readonly Queue<IPacket> _clientQueueReliable = new();
        private readonly Queue<IPacket> _clientQueueUnreliable = new();
        private readonly Dictionary<int, UDPClientConnectionInfo> _clients = new();
        #endregion

        #region Helper Properties
        /// <summary>
        ///     The address of the server.
        /// </summary>
        public string Address
        {
            get => address;
            set => address = value;
        }

        /// <summary>
        ///     The port of the server / client connection.
        /// </summary>
        public int Port
        {
            get => port;
            set => port = value;
        }

        /// <summary>
        ///     The password of the server.
        ///     The password that the client is using to connect to the server.
        /// </summary>
        public string Password
        {
            get => password;
            set => password = value;
        }

        /// <summary>
        ///     The maximum number of clients that can connect to the server at the same time.
        /// </summary>
        public int MaxClients
        {
            get => maxClients;
            set => maxClients = value;
        }
        #endregion

        #region Internal Methods
        private void _StartServer(bool startClient)
        {
            if (_server != null)
                throw new Exception("Server already started");

            _server = new UDPServer();

            _server.onServerStarted += () =>
            {
                OnServerStart?.Invoke();

                if (startClient)
                    StartClient(NetworkManager.Instance.VersionMagicNumber);
            };

            _server.onServerStopped += () =>
            {
                OnServerStop?.Invoke(ConnectionEndMode.Shutdown, "shutdown");
                _server = null;
            };

            _server.onClientConnected += clientId =>
            {
                var peer = _server.GetPeer(clientId);
                _clients.Add(clientId, new UDPClientConnectionInfo(clientId, peer));
                OnClientConnected?.Invoke(clientId);
            };

            _server.onClientDisconnected += (clientId, reason) =>
            {
                _clients.Remove(clientId);
                OnClientDisconnected?.Invoke(clientId, reason);
            };

            _server.onPacketReceived += (clientId, data) =>
            {
                var binaryReader = new BinaryReader(new MemoryStream(data.data, data.offset, data.length));

                while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                {
                    var id = binaryReader.ReadInt32();
                    var packet = PacketRegistry.CreatePacket(id);

                    packet.Deserialize(binaryReader);
                    OnServerPacketReceived?.Invoke(clientId, packet);
                }
            };

            _server.onReceiveConnectionRequest += (_, data) =>
            {
                var inputPassword = new BinaryReader(new MemoryStream(data)).ReadString();

                if (GetClientCount() >= maxClients)
                    return new ConnectionRequestAnswer
                    {
                        accepted = false,
                        reason = "server_full"
                    };

                if (!string.IsNullOrEmpty(password) && inputPassword != password)
                    return new ConnectionRequestAnswer
                    {
                        accepted = false,
                        reason = "wrong_password"
                    };

                return new ConnectionRequestAnswer
                {
                    accepted = true
                };
            };

            _server.getServerDiscovererAnswer = () =>
            {
                var hasPassword = !string.IsNullOrEmpty(password);
                var writer = new BinaryWriter(new MemoryStream());
                writer.Write("server_answer");
                writer.Write(NetworkManager.Instance.Name);
                writer.Write(_clients.Count);
                writer.Write(maxClients);
                writer.Write((int)PlatformExtensions.GetPlatform());
                writer.Write(hasPassword);

                return ((MemoryStream)writer.BaseStream).ToArray();
            };

            _server.onError += (reason) =>
            {
                OnServerStop?.Invoke(ConnectionEndMode.InternalError, reason);
                _server = null;
            };

            try
            {
                _server.Host(IPAddress.Parse(address), port);
                Type = Type == EnvironmentType.None ? EnvironmentType.Server : EnvironmentType.Host;
            }
            catch (Exception e)
            {
                OnServerStop?.Invoke(ConnectionEndMode.InternalError, e.Message);
                _server = null;
            }
        }

        private static IEnumerable<UDPSpan> _ProcessQueue(Queue<IPacket> queue, int maxSize)
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

                yield return new UDPSpan(_Buffer0, 0, (int)_Writer0.BaseStream.Position);
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
                    yield return new UDPSpan(_Buffer0, 0, end);
                    end = 0;
                }

                Assert.IsTrue(end + len <= maxSize, $"Packet too large {packet}: {len}");
                Buffer.BlockCopy(_Buffer1, 0, _Buffer0, end, (int)len);
                end += (int)len;
            }

            if (end > 0)
                yield return new UDPSpan(_Buffer0, 0, end);
        }
        #endregion

        #region Types
        private class LocalUDPClientConnectionInfo : IClientConnectionInfo
        {
            public LocalUDPClientConnectionInfo(int id, UDPClient client)
            {
                Id = id;
                Client = client;
            }

            private UDPClient Client { get; }
            public int Id { get; }
            public int Latency => Client.latency;
            public long PacketSent => Client.sentPacketCount;
            public long PacketReceived => Client.receivedPacketCount;
            public long PacketLoss => Client.lostPacketCount;
        }

        private class UDPClientConnectionInfo : IClientConnectionInfo
        {
            public readonly Queue<IPacket> queueReliable = new();
            public readonly Queue<IPacket> queueUnreliable = new();

            public UDPClientConnectionInfo(int id, UDPPeer peer)
            {
                Id = id;
                Peer = peer;
            }

            private UDPPeer Peer { get; }
            public int Id { get; }
            public int Latency => Peer.latency;
            public long PacketSent => Peer.sentPacketCount;
            public long PacketReceived => Peer.receivedPacketCount;
            public long PacketLoss => Peer.lostPacketCount;
        }
        #endregion

        #region Types
        private class ReliableSent
        {
            public byte[] data;
            public long sentTicks;
        }

        private class ReliableReceived
        {
            public byte[] data;
            public byte type;
            public int waitingFragmentUntil = -1;
        }

        private class ConnectionRequestAnswer
        {
            public bool accepted;
            public string reason;
        }

        private enum Phase
        {
            Off,
            Starting,
            Running,
            Stopping
        }

        private struct UDPSpan
        {
            public readonly byte[] data;
            public readonly int offset;
            public readonly int length;

            public UDPSpan(byte[] data)
            {
                this.data = data;
                offset = 0;
                length = data.Length;
            }

            public UDPSpan(byte[] data, int offset, int length)
            {
                this.data = data;
                this.offset = offset;
                this.length = length;
            }
        }

        private class UDPPeer
        {
            public readonly EndPoint endPoint;
            public readonly int id;

            public readonly Dictionary<int, ReliableReceived> receivedReliable = new(32);
            public readonly Queue<byte[]> receivedUnreliable = new();

            public readonly Dictionary<int, ReliableSent> sentReliable = new(32);
            public int expectedSequenceNumber;

            public long lastKeepAlive;

            public long lastReceivedTicks;
            public short latency;
            public int lostPacketCount;

            public int nextSequenceNumber;
            public int receivedPacketCount;

            public int sentPacketCount;

            public int waitingFragmentSince = -1;
            public int waitingFragmentUntil = -1;

            public UDPPeer(int id, EndPoint endPoint)
            {
                this.id = id;
                this.endPoint = endPoint;

                lastReceivedTicks = DateTime.Now.Ticks;
                lastKeepAlive = DateTime.Now.Ticks;
            }
        }

        private class UDPServer
        {
            private readonly Queue<Action> _actions = new();
            private readonly byte[] _mainThreadBuffer = new byte[1024];
            private readonly Dictionary<int, UDPPeer> _peers = new();
            private readonly Dictionary<EndPoint, UDPPeer> _peersByIp = new();

            private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            private int _nextClientId;
            private Thread _thread;
            private bool _isRunning;

            public void Host(IPAddress address, int port)
            {
                _socket.Bind(new IPEndPoint(address, port));
                onServerStarted?.Invoke();

                _isRunning = true;
                _thread = new Thread(_Loop);
                _thread.Start();
            }

            public void Close()
            {
                if (!_isRunning)
                    return;

                for (var i = 0; i < _peers.Count; i++)
                {
                    var peer = _peers.ElementAt(i).Value;
                    _SendDisconnectRequest("disconnect", peer.id);
                    i--;
                }

                _socket.Close();
                onServerStopped?.Invoke();
                _isRunning = false;
            }

            public void DisconnectClient(int clientId, string reason)
            {
                _SendDisconnectRequest(reason, clientId);
            }

            public UDPPeer GetPeer(int clientId)
            {
                return _peers.GetValueOrDefault(clientId);
            }

            private void _Loop()
            {
                try
                {
                    var threadBuffer = new byte[1024];
                    while (true)
                    {
                        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                        var received = _socket.ReceiveFrom(threadBuffer, ref remote);

                        var peer = _peersByIp.GetValueOrDefault(remote);
                        var channel = threadBuffer[0];
                        var now = DateTime.Now.Ticks;

                        switch (channel)
                        {
                            case _CHANNEL_KEEP_ALIVE:
                                if (peer != null)
                                {
                                    var nowRemote = BitConverter.ToInt64(threadBuffer, 3);

                                    peer.latency = (short)((now - nowRemote) / 20_000);
                                    peer.lastReceivedTicks = now;
                                }

                                break;

                            case _CHANNEL_SERVER_INFO:
                                var discovererAnswer = getServerDiscovererAnswer.Invoke();
                                _InternalSendSpan(new UDPSpan(discovererAnswer), remote);
                                break;

                            case _CHANNEL_UNRELIABLE:
                                if (peer != null)
                                {
                                    var b = new byte[received - 1];
                                    Buffer.BlockCopy(threadBuffer, 1, b, 0, received - 1);

                                    peer.lastReceivedTicks = now;
                                    peer.receivedUnreliable.Enqueue(b);
                                }

                                break;

                            case _CHANNEL_RELIABLE_FRAGMENT_HEADER:
                            {
                                var type = threadBuffer[1];
                                var sequence = (threadBuffer[2] << 24) | (threadBuffer[3] << 16) |
                                               (threadBuffer[4] << 8) | threadBuffer[5];

                                //Send ACK
                                var ack = new byte[5];
                                ack[0] = _CHANNEL_ACK;
                                ack[1] = threadBuffer[2];
                                ack[2] = threadBuffer[3];
                                ack[3] = threadBuffer[4];
                                ack[4] = threadBuffer[5];

                                _InternalSendSpan(new UDPSpan(ack), remote);
                                peer.lastReceivedTicks = now;

                                if (!peer.receivedReliable.ContainsKey(sequence))
                                {
                                    var count = threadBuffer[6];

                                    var b = new byte[received - 7];
                                    Buffer.BlockCopy(threadBuffer, 7, b, 0, received - 7);
                                    peer.receivedReliable.Add(sequence,
                                        new ReliableReceived
                                            { data = b, waitingFragmentUntil = sequence + count - 1, type = type });
                                }
                            }
                                break;

                            case _CHANNEL_RELIABLE:
                            {
                                var type = threadBuffer[1];
                                var sequence = (threadBuffer[2] << 24) | (threadBuffer[3] << 16) |
                                               (threadBuffer[4] << 8) | threadBuffer[5];

                                //Send ACK
                                var ack = new byte[5];
                                ack[0] = _CHANNEL_ACK;
                                ack[1] = threadBuffer[2];
                                ack[2] = threadBuffer[3];
                                ack[3] = threadBuffer[4];
                                ack[4] = threadBuffer[5];

                                _InternalSendSpan(new UDPSpan(ack), remote);

                                if (peer == null)
                                {
                                    var id = _nextClientId++;
                                    peer = new UDPPeer(id, remote);
                                    _peers.Add(id, peer);
                                    _peersByIp.Add(remote, peer);
                                }

                                peer.lastReceivedTicks = now;

                                if (!peer.receivedReliable.ContainsKey(sequence))
                                {
                                    var b = new byte[received - 6];
                                    Buffer.BlockCopy(threadBuffer, 6, b, 0, received - 6);

                                    peer.receivedReliable.Add(sequence,
                                        new ReliableReceived { data = b, type = type });
                                }
                            }
                                break;

                            case _CHANNEL_ACK:
                                if (peer != null)
                                {
                                    var sequence = (threadBuffer[1] << 24) | (threadBuffer[2] << 16) |
                                                   (threadBuffer[3] << 8) | threadBuffer[4];

                                    lock (peer.sentReliable)
                                    {
                                        if (peer.sentReliable.ContainsKey(sequence))
                                            peer.sentReliable.Remove(sequence);
                                    }

                                    peer.lastReceivedTicks = now;
                                }

                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (_isRunning)
                        _actions.Enqueue(() =>
                        {
                            onError?.Invoke(e.Message);
                            _isRunning = false;
                            _socket.Close();
                            _thread = null;
                        });
                }

                // ReSharper disable once FunctionNeverReturns
            }

            private void _InternalSendSpan(UDPSpan span, EndPoint remote)
            {
                _socket.SendTo(span.data, span.offset, span.length, SocketFlags.None, remote);
            }

            private void _SendDisconnectRequest(string reason, int clientId)
            {
                var reasonBytes = Encoding.UTF8.GetBytes(reason);

                SendPacketReliable(_PACKET_RELIABLE_DISCONNECT, new UDPSpan(reasonBytes), clientId);
                _RemoveClient(clientId, reason);
            }

            private void _SendConnectionResponse(int clientId)
            {
                var empty = Array.Empty<byte>();
                SendPacketReliable(_PACKET_RELIABLE_CONNECTION_RESPONSE, new UDPSpan(empty), clientId);
            }

            private void _RemoveClient(int clientId, string reason)
            {
                var peer = _peers.GetValueOrDefault(clientId);

                if (peer == null)
                    return;

                _peers.Remove(peer.id);
                _peersByIp.Remove(peer.endPoint);
                onClientDisconnected?.Invoke(peer.id, reason);
            }

            public void SendPacketUnreliable(UDPSpan span, int clientId)
            {
                var peer = _peers.GetValueOrDefault(clientId);

                if (span.length > _MAX_PACKET_SIZE)
                    throw new Exception("Packet too big");

                _SendUnreliable(span, peer);
            }

            public void SendPacketReliable(byte type, UDPSpan span, int clientId)
            {
                var peer = _peers.GetValueOrDefault(clientId);

                if (span.length > _MAX_PACKET_SIZE)
                {
                    var fragments = (int)Math.Ceiling((double)span.length / _MAX_PACKET_SIZE);
                    for (var i = 0; i < fragments; i++)
                    {
                        var fragmentSize = Math.Min(_MAX_PACKET_SIZE, span.length - i * _MAX_PACKET_SIZE);

                        var fragment = new UDPSpan(span.data, span.offset + i * _MAX_PACKET_SIZE, fragmentSize);
                        if (i == 0)
                            _SendReliableHeader(type, fragment, peer, (byte)fragments);
                        else
                            _SendReliable(type, fragment, peer);
                    }
                }
                else
                {
                    _SendReliable(type, span, peer);
                }
            }

            private void _SendReliableHeader(byte type, UDPSpan fragment, UDPPeer peer, byte count)
            {
                var id = peer.nextSequenceNumber++;
                peer.sentPacketCount++;

                var buffer = new byte[fragment.length + 7];

                buffer[0] = _CHANNEL_RELIABLE_FRAGMENT_HEADER;
                buffer[1] = type;

                buffer[2] = (byte)(id >> 24);
                buffer[3] = (byte)(id >> 16);
                buffer[4] = (byte)(id >> 8);
                buffer[5] = (byte)id;

                buffer[6] = count;

                Buffer.BlockCopy(fragment.data, fragment.offset, buffer, 7, fragment.length);

                lock (peer.sentReliable)
                {
                    peer.sentReliable.Add(id, new ReliableSent { sentTicks = DateTime.Now.Ticks, data = buffer });
                }

                _InternalSendSpan(new UDPSpan(buffer), peer.endPoint);
            }

            private void _SendReliable(byte type, UDPSpan fragment, UDPPeer peer)
            {
                var id = peer.nextSequenceNumber++;
                peer.sentPacketCount++;

                var buffer = new byte[fragment.length + 6];

                buffer[0] = _CHANNEL_RELIABLE;
                buffer[1] = type;

                buffer[2] = (byte)(id >> 24);
                buffer[3] = (byte)(id >> 16);
                buffer[4] = (byte)(id >> 8);
                buffer[5] = (byte)id;

                Buffer.BlockCopy(fragment.data, fragment.offset, buffer, 6, fragment.length);

                lock (peer.sentReliable)
                {
                    peer.sentReliable.Add(id, new ReliableSent { sentTicks = DateTime.Now.Ticks, data = buffer });
                }

                _InternalSendSpan(new UDPSpan(buffer), peer.endPoint);
            }

            private void _SendUnreliable(UDPSpan fragment, UDPPeer peer)
            {
                peer.sentPacketCount++;

                _mainThreadBuffer[0] = _CHANNEL_UNRELIABLE;
                Buffer.BlockCopy(fragment.data, fragment.offset, _mainThreadBuffer, 1, fragment.length);
                _InternalSendSpan(new UDPSpan(_mainThreadBuffer, 0, 1 + fragment.length), peer.endPoint);
            }


            public void PoolEvents()
            {
                while (_actions.Count > 0)
                    _actions.Dequeue().Invoke();

                for (var i = 0; i < _peers.Count; i++)
                {
                    var peer = _peers.ElementAt(i).Value;
                    var now = DateTime.Now.Ticks;

                    //Process timeout
                    if (now - peer.lastReceivedTicks > _TIMEOUT_TIME)
                    {
                        _SendDisconnectRequest("timeout", peer.id);
                        i--;
                        continue;
                    }

                    //Keep alive
                    if (now - peer.lastKeepAlive > _KEEP_ALIVE_TIME)
                    {
                        //send current ticks as bytes
                        now = DateTime.Now.Ticks;

                        var bytes = BitConverter.GetBytes(now);
                        var keepAlive = new byte[11];
                        keepAlive[0] = _CHANNEL_KEEP_ALIVE;
                        keepAlive[1] = (byte)(peer.latency >> 8);
                        keepAlive[2] = (byte)peer.latency;
                        Buffer.BlockCopy(bytes, 0, keepAlive, 3, 8);

                        //Keep alive
                        _InternalSendSpan(new UDPSpan(keepAlive, 0, 11), peer.endPoint);
                        peer.lastKeepAlive = now;
                    }

                    //Process resend
                    lock (peer.sentReliable)
                    {
                        var c = Mathf.Min(peer.sentReliable.Count, _RESEND_MAX_PER_FRAME);
                        for (var j = 0; j < c; j++)
                        {
                            var packet = peer.sentReliable.ElementAt(j).Value;
                            if (now - packet.sentTicks <= _RESEND_TIME)
                                continue;

                            _InternalSendSpan(new UDPSpan(packet.data), peer.endPoint);
                            packet.sentTicks = now;
                        }
                    }

                    //Process received unreliable
                    while (peer.receivedUnreliable.Count > 0)
                    {
                        var data = peer.receivedUnreliable.Dequeue();

                        peer.receivedPacketCount++;
                        onPacketReceived?.Invoke(peer.id, new UDPSpan(data));
                    }

                    //Process received reliable
                    while (peer.receivedReliable.ContainsKey(peer.expectedSequenceNumber))
                    {
                        var packet = peer.receivedReliable[peer.expectedSequenceNumber];

                        if (packet.waitingFragmentUntil != -1)
                        {
                            peer.waitingFragmentSince = peer.expectedSequenceNumber;
                            peer.waitingFragmentUntil = packet.waitingFragmentUntil;
                        }

                        if (peer.waitingFragmentUntil == -1)
                        {
                            peer.receivedPacketCount++;
                            _HandleReliablePacket(packet.type, new UDPSpan(packet.data), peer.id);
                            peer.receivedReliable.Remove(peer.expectedSequenceNumber);
                        }
                        else if (peer.expectedSequenceNumber == peer.waitingFragmentUntil)
                        {
                            var offset = 0;

                            for (var j = peer.waitingFragmentSince; j <= peer.waitingFragmentUntil; j++)
                            {
                                var fragment = peer.receivedReliable[j].data;
                                Buffer.BlockCopy(fragment, 0, _FragmentUnitBuffer, offset, fragment.Length);
                                offset += fragment.Length;
                                peer.receivedReliable.Remove(j);
                            }

                            peer.receivedPacketCount++;
                            _HandleReliablePacket(packet.type, new UDPSpan(_FragmentUnitBuffer, 0, offset - 2),
                                peer.id);
                            peer.waitingFragmentUntil = -1;
                        }

                        peer.expectedSequenceNumber++;
                    }
                }
            }

            private void _HandleReliablePacket(byte type, UDPSpan body, int clientId)
            {
                switch (type)
                {
                    case _PACKET_RELIABLE_CONNECTION_REQUEST:
                        var answer = onReceiveConnectionRequest.Invoke(clientId, body.data);
                        if (answer.accepted)
                        {
                            _SendConnectionResponse(clientId);
                            onClientConnected?.Invoke(clientId);
                        }
                        else
                        {
                            _SendDisconnectRequest(answer.reason, clientId);
                        }

                        break;

                    case _PACKET_RELIABLE_DISCONNECT:
                        _RemoveClient(clientId, Encoding.UTF8.GetString(body.data));
                        break;

                    case _PACKET_RELIABLE_MESSAGE:
                        onPacketReceived?.Invoke(clientId, body);
                        break;
                }
            }

            #region Callbacks
            public Action onServerStarted;
            public Action onServerStopped;

            public Action<int> onClientConnected;
            public Action<int, string> onClientDisconnected;
            public Action<int, UDPSpan> onPacketReceived;
            public Action<string> onError;

            public Func<int, byte[], ConnectionRequestAnswer> onReceiveConnectionRequest =
                (_, _) => new ConnectionRequestAnswer { accepted = true };

            public Func<byte[]> getServerDiscovererAnswer;
            #endregion
        }

        private class UDPClient
        {
            private readonly Queue<Action> _actions = new();

            private readonly byte[] _mainThreadBuffer = new byte[1024];
            private readonly Dictionary<int, ReliableReceived> _receivedReliable = new(32);
            private readonly Queue<byte[]> _receivedUnreliable = new();

            private readonly Dictionary<int, ReliableSent> _sentReliable = new(32);

            private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            private int _expectedSequenceNumber;

            private long _lastReceivedTicks = DateTime.Now.Ticks;
            private int _nextSequenceNumber;
            private EndPoint _server;
            private Thread _thread;

            private int _waitingFragmentSince = -1;
            private int _waitingFragmentUntil = -1;

            public int latency;
            public int lostPacketCount;
            public int receivedPacketCount;
            public int sentPacketCount;
            private Phase _phase;

            public void Connect(IPAddress address, int port, byte[] payload)
            {
                _server = new IPEndPoint(address, port);

                _phase = Phase.Starting;
                _socket.Connect(address, port);
                _SendConnectionRequest(payload);
                _phase = Phase.Running;

                _thread = new Thread(_Loop);
                _thread.Start();
            }

            public void Disconnect(string reason)
            {
                if (_phase is Phase.Running)
                    _SendDisconnectRequest(reason);
            }

            private void _Loop()
            {
                try
                {
                    var threadBuffer = new byte[1024];
                    while (true)
                    {
                        var received = _socket.Receive(threadBuffer);
                        var channel = threadBuffer[0];
                        var now = DateTime.Now.Ticks;

                        switch (channel)
                        {
                            case _CHANNEL_UNRELIABLE:
                            {
                                var b = new byte[received - 1];
                                Buffer.BlockCopy(threadBuffer, 1, b, 0, received - 1);

                                _lastReceivedTicks = now;
                                _receivedUnreliable.Enqueue(b);
                            }
                                break;

                            case _CHANNEL_RELIABLE_FRAGMENT_HEADER:
                            {
                                var type = threadBuffer[1];
                                var sequence = (threadBuffer[2] << 24) | (threadBuffer[3] << 16) |
                                               (threadBuffer[4] << 8) | threadBuffer[5];

                                //Send ACK
                                var ack = new byte[5];
                                ack[0] = _CHANNEL_ACK;
                                ack[1] = threadBuffer[2];
                                ack[2] = threadBuffer[3];
                                ack[3] = threadBuffer[4];
                                ack[4] = threadBuffer[5];

                                _lastReceivedTicks = now;
                                _InternalSendSpan(new UDPSpan(ack));

                                if (!_receivedReliable.ContainsKey(sequence))
                                {
                                    var count = threadBuffer[6];

                                    var b = new byte[received - 7];
                                    Buffer.BlockCopy(threadBuffer, 7, b, 0, received - 7);
                                    _receivedReliable.Add(sequence,
                                        new ReliableReceived
                                            { data = b, waitingFragmentUntil = sequence + count - 1, type = type });
                                }
                            }
                                break;

                            case _CHANNEL_RELIABLE:
                            {
                                var type = threadBuffer[1];
                                var sequence = (threadBuffer[2] << 24) | (threadBuffer[3] << 16) |
                                               (threadBuffer[4] << 8) | threadBuffer[5];

                                //Send ACK
                                var ack = new byte[5];
                                ack[0] = _CHANNEL_ACK;
                                ack[1] = threadBuffer[2];
                                ack[2] = threadBuffer[3];
                                ack[3] = threadBuffer[4];
                                ack[4] = threadBuffer[5];

                                _lastReceivedTicks = now;
                                _InternalSendSpan(new UDPSpan(ack));

                                if (!_receivedReliable.ContainsKey(sequence))
                                {
                                    var b = new byte[received - 6];
                                    Buffer.BlockCopy(threadBuffer, 6, b, 0, received - 6);

                                    _receivedReliable.Add(sequence,
                                        new ReliableReceived { type = type, data = b });
                                }
                            }
                                break;

                            case _CHANNEL_ACK:
                            {
                                var sequence = (threadBuffer[1] << 24) | (threadBuffer[2] << 16) |
                                               (threadBuffer[3] << 8) | threadBuffer[4];

                                _lastReceivedTicks = now;

                                lock (_sentReliable)
                                {
                                    if (_sentReliable.ContainsKey(sequence))
                                        _sentReliable.Remove(sequence);
                                }
                            }
                                break;

                            case _CHANNEL_KEEP_ALIVE:
                            {
                                latency = (threadBuffer[1] << 8) | threadBuffer[2];
                                _lastReceivedTicks = now;
                                _InternalSendSpan(new UDPSpan(threadBuffer, 0, 11));
                            }
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (_phase is Phase.Running or Phase.Starting)
                        _actions.Enqueue(() =>
                        {
                            onError?.Invoke(e.Message);
                            _phase = Phase.Off;
                            _socket.Close();
                            _thread = null;
                        });
                }

                // ReSharper disable once FunctionNeverReturns
            }

            private void _InternalSendSpan(UDPSpan span)
            {
                _socket.SendTo(span.data, span.offset, span.length, SocketFlags.None, _server!);
            }

            private void _SendConnectionRequest(byte[] payload)
            {
                SendPacketReliable(_PACKET_RELIABLE_CONNECTION_REQUEST, new UDPSpan(payload));
            }

            private void _SendDisconnectRequest(string reason)
            {
                var reasonBytes = Encoding.UTF8.GetBytes(reason);
                SendPacketReliable(_PACKET_RELIABLE_DISCONNECT, new UDPSpan(reasonBytes));

                if (_phase is not Phase.Running)
                    return;

                _phase = Phase.Stopping;
                onDisconnected?.Invoke(reason);
                _socket.Close();
                _phase = Phase.Off;
            }

            public void SendPacketUnreliable(UDPSpan span)
            {
                if (span.length > _MAX_PACKET_SIZE)
                    throw new Exception("Packet too big");

                _SendUnreliable(span);
            }

            public void SendPacketReliable(byte type, UDPSpan span)
            {
                if (span.length > _MAX_PACKET_SIZE)
                {
                    var fragments = (int)Math.Ceiling((double)span.length / _MAX_PACKET_SIZE);
                    for (var i = 0; i < fragments; i++)
                    {
                        var fragmentSize = Math.Min(_MAX_PACKET_SIZE, span.length - i * _MAX_PACKET_SIZE);
                        var fragment = new UDPSpan(span.data, span.offset + i * _MAX_PACKET_SIZE, fragmentSize);

                        if (i == 0)
                            _SendReliableHeader(type, fragment, (byte)fragments);
                        else
                            _SendReliable(type, fragment);
                    }
                }
                else
                {
                    _SendReliable(type, span);
                }
            }

            private void _SendReliableHeader(byte type, UDPSpan fragment, byte count)
            {
                var id = _nextSequenceNumber++;
                sentPacketCount++;

                var buffer = new byte[fragment.length + 7];
                buffer[0] = _CHANNEL_RELIABLE_FRAGMENT_HEADER;
                buffer[1] = type;

                buffer[2] = (byte)(id >> 24);
                buffer[3] = (byte)(id >> 16);
                buffer[4] = (byte)(id >> 8);
                buffer[5] = (byte)id;

                buffer[6] = count;

                Buffer.BlockCopy(fragment.data, fragment.offset, buffer, 7, fragment.length);

                lock (_sentReliable)
                {
                    _sentReliable.Add(id, new ReliableSent { sentTicks = DateTime.Now.Ticks, data = buffer });
                }

                _InternalSendSpan(new UDPSpan(buffer));
            }

            private void _SendReliable(byte type, UDPSpan span)
            {
                var id = _nextSequenceNumber++;
                sentPacketCount++;

                var buffer = new byte[span.length + 6];

                buffer[0] = _CHANNEL_RELIABLE;
                buffer[1] = type;

                buffer[2] = (byte)(id >> 24);
                buffer[3] = (byte)(id >> 16);
                buffer[4] = (byte)(id >> 8);
                buffer[5] = (byte)id;

                Buffer.BlockCopy(span.data, span.offset, buffer, 6, span.length);

                lock (_sentReliable)
                {
                    _sentReliable.Add(id, new ReliableSent { sentTicks = DateTime.Now.Ticks, data = buffer });
                }

                _InternalSendSpan(new UDPSpan(buffer));
            }

            private void _SendUnreliable(UDPSpan span)
            {
                sentPacketCount++;

                _mainThreadBuffer[0] = _CHANNEL_UNRELIABLE;
                Buffer.BlockCopy(span.data, span.offset, _mainThreadBuffer, 1, span.length);
                _InternalSendSpan(new UDPSpan(_mainThreadBuffer, 0, 1 + span.length));
            }

            public void PoolEvents()
            {
                //Actions
                while (_actions.Count > 0)
                    _actions.Dequeue().Invoke();

                var now = DateTime.Now.Ticks;

                //Process timeout
                if (now - _lastReceivedTicks > _TIMEOUT_TIME)
                    _SendDisconnectRequest("timeout");

                //Process resend
                lock (_sentReliable)
                {
                    var c = Mathf.Min(_sentReliable.Count, _RESEND_MAX_PER_FRAME);
                    for (var i = 0; i < c; i++)
                    {
                        var packet = _sentReliable.ElementAt(i).Value;
                        if (now - packet.sentTicks <= _RESEND_TIME)
                            continue;

                        _InternalSendSpan(new UDPSpan(packet.data));
                        packet.sentTicks = now;
                    }
                }

                //Process received unreliable
                while (_receivedUnreliable.Count > 0)
                {
                    var data = _receivedUnreliable.Dequeue();
                    receivedPacketCount++;
                    onPacketReceived?.Invoke(new UDPSpan(data));
                }

                //Process received reliable
                while (_receivedReliable.ContainsKey(_expectedSequenceNumber))
                {
                    var packet = _receivedReliable[_expectedSequenceNumber];

                    if (packet.waitingFragmentUntil != -1)
                    {
                        _waitingFragmentSince = _expectedSequenceNumber;
                        _waitingFragmentUntil = packet.waitingFragmentUntil;
                    }

                    if (_waitingFragmentUntil == -1)
                    {
                        receivedPacketCount++;
                        _HandleReliablePacket(packet.type, new UDPSpan(packet.data));
                        _receivedReliable.Remove(_expectedSequenceNumber);
                    }
                    else if (_expectedSequenceNumber == _waitingFragmentUntil)
                    {
                        var offset = 0;

                        for (var i = _waitingFragmentSince; i <= _waitingFragmentUntil; i++)
                        {
                            var fragment = _receivedReliable[i].data;
                            Buffer.BlockCopy(fragment, 0, _FragmentUnitBuffer, offset, fragment.Length);
                            offset += fragment.Length;
                            _receivedReliable.Remove(i);
                        }

                        receivedPacketCount++;
                        _HandleReliablePacket(packet.type, new UDPSpan(_FragmentUnitBuffer, 0, offset));
                        _waitingFragmentUntil = -1;
                    }

                    _expectedSequenceNumber++;
                }
            }

            private void _HandleReliablePacket(byte type, UDPSpan body)
            {
                switch (type)
                {
                    case _PACKET_RELIABLE_CONNECTION_RESPONSE:
                        onConnected?.Invoke();
                        break;

                    case _PACKET_RELIABLE_DISCONNECT:
                        if (_phase is Phase.Running or Phase.Starting)
                        {
                            _phase = Phase.Stopping;
                            onDisconnected?.Invoke(Encoding.UTF8.GetString(body.data));
                            _socket.Close();
                            _phase = Phase.Off;
                        }

                        break;

                    default:
                        onPacketReceived?.Invoke(body);
                        break;
                }
            }

            #region Callbacks
            public Action onConnected;
            public Action<string> onDisconnected;
            public Action<UDPSpan> onPacketReceived;
            public Action<string> onError;
            #endregion
        }
        #endregion
    }
}