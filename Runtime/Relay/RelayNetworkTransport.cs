using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetBuff.Discover;
using NetBuff.Interface;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace NetBuff.Relays
{
    /// <summary>
    ///     Uses the Unity Relay service to manage connections between clients and servers.
    ///     Responsible for internally managing the connection between the server and clients.
    ///     Holds the connection information of clients and provides methods for sending and receiving packets.
    /// </summary>
    [Icon("Assets/Editor/Icons/UDPNetworkTransport.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/subpackages/relay")]
    public class RelayNetworkTransport : NetworkTransport
    {
        #region Jobs - Server
        /// <summary>
        /// Job used to update connections. 
        /// </summary>
        [BurstCompile]
        struct ServerUpdateConnectionsJob : IJob
        {
            /// <summary>
            /// Used to bind, listen, and send data to connections.
            /// </summary>
            public NetworkDriver driver;

            /// <summary>
            /// Client connections to this server.
            /// </summary>
            public NativeList<NetworkConnection> connections;

            /// <summary>
            /// Temporary storage for connection events that occur on job threads so they may be dequeued on the main thread.
            /// </summary>
            public NativeQueue<UtpConnectionEvent>.ParallelWriter connectionsEventsQueue;

            /// <summary>
            /// Remove a connection from the server.
            /// </summary>
            /// <param name="id"></param>
            [BurstDiscard]
            private void Remove(int id)
            {
                var t = (RelayNetworkTransport)NetworkManager.Instance.Transport;
                t.OnClientDisconnected?.Invoke(id, "connection_shutdown");
            }

            /// <summary>
            /// Main job method to update connections.
            /// </summary>
            public void Execute()
            {
                //Iterate through connections list
                for (var i = 0; i < connections.Length; i++)
                {
                    //If a connection is no longer established, remove it
                    if (driver.GetConnectionState(connections[i]) == NetworkConnection.State.Disconnected)
                    {
                        Remove(connections[i].GetHashCode());
                        connections.RemoveAtSwapBack(i--);
                    }
                }

                // Accept new connections
                NetworkConnection networkConnection;
                while ((networkConnection = driver.Accept()) != default(NetworkConnection))
                {
                    //Set up connection event
                    UtpConnectionEvent connectionEvent = new UtpConnectionEvent()
                    {
                        eventType = (byte)ConnectionEventType.OnConnected,
                        connectionId = networkConnection.GetHashCode()
                    };

                    //Queue connection event
                    connectionsEventsQueue.Enqueue(connectionEvent);

                    //Add connection to connection list
                    connections.Add(networkConnection);
                }
            }
        }

        /// <summary>
        /// Job to query incoming events for all connections. 
        /// </summary>
        [BurstCompile]
        struct ServerUpdateJob : IJobParallelForDefer
        {
            /// <summary>
            /// Used to bind, listen, and send data to connections.
            /// </summary>
            public NetworkDriver.Concurrent driver;

            /// <summary>
            /// client connections to this server.
            /// </summary>
            public NativeArray<NetworkConnection> connections;

            /// <summary>
            /// Temporary storage for connection events that occur on job threads so they may be dequeued on the main thread.
            /// </summary>
            public NativeQueue<UtpConnectionEvent>.ParallelWriter connectionsEventsQueue;

            /// <summary>
            /// Process all incoming events/messages on this connection.
            /// </summary>
            /// <param name="index">The current index being accessed in the array.</param>
            public void Execute(int index)
            {
                NetworkEvent.Type netEvent;
                while ((netEvent = driver.PopEventForConnection(connections[index], out DataStreamReader stream)) !=
                       NetworkEvent.Type.Empty)
                {
                    if (netEvent == NetworkEvent.Type.Data)
                    {
                        var nativeMessage = new NativeArray<byte>(stream.Length, Allocator.Temp);
                        stream.ReadBytes(nativeMessage);

                        //Set up connection event
                        var connectionEvent = new UtpConnectionEvent()
                        {
                            eventType = (byte)ConnectionEventType.OnReceivedData,
                            eventData = _GetFixedList(nativeMessage),
                            connectionId = connections[index].GetHashCode()
                        };

                        //Queue connection event
                        connectionsEventsQueue.Enqueue(connectionEvent);
                    }
                    else if (netEvent == NetworkEvent.Type.Disconnect)
                    {
                        //Set up disconnect event
                        var connectionEvent = new UtpConnectionEvent()
                        {
                            eventType = (byte)ConnectionEventType.OnDisconnected,
                            connectionId = connections[index].GetHashCode()
                        };

                        //Queue disconnect event
                        connectionsEventsQueue.Enqueue(connectionEvent);
                    }
                }
            }

            /// <summary>
            /// Convert unmanaged native array to 4096 Byte list. Uses unsafe code.
            /// </summary>
            /// <param name="data">The data to convert.</param>
            /// <returns>An unmanaged fixed list of data.</returns>
            private FixedList4096Bytes<byte> _GetFixedList(NativeArray<byte> data)
            {
                var retVal = new FixedList4096Bytes<byte>();

                if (data.Length > 0)
                {
                    unsafe
                    {
                        retVal.AddRange(data.GetUnsafePtr(), data.Length);
                    }
                }

                return retVal;
            }
        }

        /// <summary>
        /// Job to send data to a client connection.
        /// </summary>
        [BurstCompile]
        struct ServerSendJob : IJob
        {
            /// <summary>
            /// Used to bind, listen, and send data to connections.
            /// </summary>
            public NetworkDriver driver;

            /// <summary>
            /// The network pipeline to stream data.
            /// </summary>
            public NetworkPipeline pipeline;

            /// <summary>
            /// The client's network connection instance.
            /// </summary>
            public NetworkConnection connection;

            /// <summary>
            /// The segment of data to send over (deallocates after use).
            /// </summary>
            [DeallocateOnJobCompletion]
            public NativeArray<byte> data;

            public void Execute()
            {
                var writeStatus = driver.BeginSend(pipeline, connection, out var writer);

                //If Acquire was success
                if (writeStatus == (int)Unity.Networking.Transport.Error.StatusCode.Success)
                {
                    writer.WriteBytes(data);
                    driver.EndSend(writer);
                }
            }
        }
        #endregion

        #region Jobs - Client
        /// <summary>
        /// Job to update client connections.
        /// </summary>
        [BurstCompile]
        struct ClientUpdateJob : IJob
        {
            /// <summary>
            /// Used to bind, listen, and send data to connections.
            /// </summary>
            public NetworkDriver driver;

            /// <summary>
            /// client connections to this server.
            /// </summary>
            public NetworkConnection connection;

            /// <summary>
            /// Temporary storage for connection events that occur on job threads so they may be dequeued on the main thread.
            /// </summary>
            public NativeQueue<UtpConnectionEvent>.ParallelWriter connectionEventsQueue;

            /// <summary>
            /// Process all incoming events/messages on this connection.
            /// </summary>
            public void Execute()
            {
                //Back out if connection is invalid
                if (!connection.IsCreated)
                {
                    return;
                }

                NetworkEvent.Type netEvent;
                while ((netEvent = connection.PopEvent(driver, out DataStreamReader stream)) != NetworkEvent.Type.Empty)
                {
                    //Create new event
                    UtpConnectionEvent connectionEvent;

                    switch (netEvent)
                    {
                        //Connect event
                        case (NetworkEvent.Type.Connect):
                        {
                            connectionEvent = new UtpConnectionEvent()
                            {
                                eventType = (byte)ConnectionEventType.OnConnected,
                                connectionId = connection.GetHashCode()
                            };

                            //Queue event
                            connectionEventsQueue.Enqueue(connectionEvent);

                            break;
                        }

                        //Data received event
                        case (NetworkEvent.Type.Data):
                        {
                            //Create managed array of data
                            NativeArray<byte> nativeMessage = new NativeArray<byte>(stream.Length, Allocator.Temp);

                            //Read data from stream
                            stream.ReadBytes(nativeMessage);

                            connectionEvent = new UtpConnectionEvent()
                            {
                                eventType = (byte)ConnectionEventType.OnReceivedData,
                                connectionId = connection.GetHashCode(),
                                eventData = _GetFixedList(nativeMessage)
                            };

                            //Queue event
                            connectionEventsQueue.Enqueue(connectionEvent);
                            break;
                        }

                        //Disconnect event
                        case (NetworkEvent.Type.Disconnect):
                        {
                            connectionEvent = new UtpConnectionEvent()
                            {
                                eventType = (byte)ConnectionEventType.OnDisconnected,
                                connectionId = connection.GetHashCode()
                            };

                            //Queue event
                            connectionEventsQueue.Enqueue(connectionEvent);

                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Retrieve a fixed list of data from a native array.
            /// </summary>
            /// <param name="data"></param>
            /// <returns></returns>
            private FixedList4096Bytes<byte> _GetFixedList(NativeArray<byte> data)
            {
                FixedList4096Bytes<byte> retVal = new FixedList4096Bytes<byte>();

                if (data.Length > 0)
                {
                    unsafe
                    {
                        retVal.AddRange(data.GetUnsafePtr(), data.Length);
                    }
                }

                return retVal;
            }
        }

        /// <summary>
        /// Job to send data to a server connection.
        /// </summary>
        [BurstCompile]
        struct ClientSendJob : IJob
        {
            /// <summary>
            /// Used to bind, listen, and send data to connections.
            /// </summary>
            public NetworkDriver driver;

            /// <summary>
            /// The network pipeline to stream data.
            /// </summary>
            public NetworkPipeline pipeline;

            /// <summary>
            /// The client's network connection instance.
            /// </summary>
            public NetworkConnection connection;

            /// <summary>
            /// The segment of data to send over (deallocates after use).
            /// </summary>
            [DeallocateOnJobCompletion]
            public NativeArray<byte> data;

            public void Execute()
            {
                //Back out if connection is invalid
                if (!connection.IsCreated)
                {
                    return;
                }

                var writeStatus = driver.BeginSend(pipeline, connection, out var writer);

                //If endpoint was success, write data to stream
                if (writeStatus == (int)Unity.Networking.Transport.Error.StatusCode.Success)
                {
                    writer.WriteBytes(data);
                    driver.EndSend(writer);
                }
            }
        }
        #endregion

        #region Types
        /// <summary>
        /// Represents the channels used for sending data.
        /// </summary>
        private static class Channels
        {
            public const int RELIABLE = 0;
            public const int UNRELIABLE = 1;
        }

        /// <summary>
        /// Represents the type of event that occurred on a connection.
        /// </summary>
        private enum ConnectionEventType
        {
            OnConnected,
            OnReceivedData,
            OnDisconnected
        }

        /// <summary>
        /// Used to store connection events for processing on the main thread.
        /// </summary>
        private struct UtpConnectionEvent
        {
            /// <summary>
            /// The event type.
            /// </summary>
            public byte eventType;

            /// <summary>
            /// Event data, only used for OnReceived event.
            /// </summary>
            public FixedList4096Bytes<byte> eventData;

            /// <summary>
            /// The connection ID of the connection corresponding to this event.
            /// </summary>
            public int connectionId;
        }

        /// <summary>
        /// Base class for UTP ends.
        /// </summary>
        private abstract class UtpEnd
        {
            public NativeQueue<UtpConnectionEvent> connectionsEventsQueue;
            public NetworkDriver driver;
            public NetworkPipeline reliablePipeline;
            public NetworkPipeline unreliablePipeline;
            public JobHandle jobHandle;
            public RelayNetworkTransport transport;
        }

        /// <summary>
        /// Manages the server side of the UTP transport.
        /// </summary>
        private class UtpServer : UtpEnd
        {
            /// <summary>
            /// Holds all client connections.
            /// </summary>
            public NativeList<NetworkConnection> connections;

            /// <summary>
            /// Default number of pipelines.
            /// </summary>
            private const int _NUM_PIPELINES = 2;

            /// <summary>
            /// Default max header size for pipelines.
            /// </summary>
            private readonly int[] _driverMaxHeaderSize = new int[_NUM_PIPELINES];

            /// <summary>
            /// Try to get a connection by its connection ID.
            /// </summary>
            /// <param name="connectionId"></param>
            /// <param name="connection"></param>
            /// <returns></returns>
            public bool TryGetConnection(int connectionId, out NetworkConnection connection)
            {
                connection = _FindConnection(connectionId);
                return connection.GetHashCode() == connectionId;
            }

            /// <summary>
            /// Internal method to find a connection by its connection ID.
            /// </summary>
            /// <param name="connectionId"></param>
            /// <returns></returns>
            private NetworkConnection _FindConnection(int connectionId)
            {
                jobHandle.Complete();

                if (connections.IsCreated)
                {
                    foreach (var connection in connections)
                    {
                        if (connection.GetHashCode() == connectionId)
                        {
                            return connection;
                        }
                    }
                }

                return default;
            }

            /// <summary>
            /// Updates the server, processing incoming events.
            /// </summary>
            public void Update()
            {
                if (!driver.IsCreated)
                    return;

                // First complete the job that was initialized in the previous frame
                jobHandle.Complete();

                // Trigger Mirror callbacks for events that resulted in the last jobs work
                _ProcessIncomingEvents();

                jobHandle.Complete();
                _driverMaxHeaderSize[Channels.RELIABLE] = driver.MaxHeaderSize(reliablePipeline);
                _driverMaxHeaderSize[Channels.UNRELIABLE] = driver.MaxHeaderSize(unreliablePipeline);

                // Create a new jobs
                var serverUpdateJob = new ServerUpdateJob
                {
                    driver = driver.ToConcurrent(),
                    connections = connections.AsDeferredJobArray(),
                    connectionsEventsQueue = connectionsEventsQueue.AsParallelWriter()
                };

                var connectionJob = new ServerUpdateConnectionsJob
                {
                    driver = driver,
                    connections = connections,
                    connectionsEventsQueue = connectionsEventsQueue.AsParallelWriter()
                };

                // Schedule jobs
                jobHandle = driver.ScheduleUpdate();

                // We are explicitly scheduling ServerUpdateJob before ServerUpdateConnectionsJob so that disconnect events are enqueued before the corresponding NetworkConnection is removed
                jobHandle = serverUpdateJob.Schedule(connections, 1, jobHandle);
                jobHandle = connectionJob.Schedule(jobHandle);
            }

            /// <summary>
            /// Internal method to process incoming events.
            /// </summary>
            private void _ProcessIncomingEvents()
            {
                while (connectionsEventsQueue.TryDequeue(out var connectionEvent))
                {
                    switch (connectionEvent.eventType)
                    {
                        //Connect action 
                        case ((byte)ConnectionEventType.OnConnected):
                        {
                            transport.OnClientConnected?.Invoke(connectionEvent.connectionId);
                            break;
                        }

                        //Receive data action
                        case ((byte)ConnectionEventType.OnReceivedData):
                        {
                            var data = connectionEvent.eventData.ToArray();
                            var binaryReader = new BinaryReader(new MemoryStream(data));

                            while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                            {
                                var id = binaryReader.ReadInt32();
                                var packet = PacketRegistry.CreatePacket(id);

                                packet.Deserialize(binaryReader);
                                transport.OnServerPacketReceived?.Invoke(connectionEvent.connectionId, packet);
                            }

                            break;
                        }

                        //Disconnect action
                        case ((byte)ConnectionEventType.OnDisconnected):
                        {
                            transport.OnClientDisconnected?.Invoke(connectionEvent.connectionId, "connection_shutdown");
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Used to disconnect a client connection.
            /// </summary>
            /// <param name="connectionId"></param>
            public void Disconnect(int connectionId)
            {
                foreach (var connection in connections)
                {
                    if (connection.GetHashCode() == connectionId)
                    {
                        connection.Disconnect(driver);

                        //Set up connection event
                        UtpConnectionEvent connectionEvent = new UtpConnectionEvent()
                        {
                            eventType = (byte)ConnectionEventType.OnDisconnected,
                            connectionId = connection.GetHashCode()
                        };

                        //Queue connection event
                        connectionsEventsQueue.Enqueue(connectionEvent);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Manages the client side of the UTP transport.
        /// </summary>
        private class UtpClient : UtpEnd
        {
            /// <summary>
            /// The client connection.
            /// </summary>
            public NetworkConnection connection;

            /// <summary>
            /// Default number of pipelines.
            /// </summary>
            private const int _NUM_PIPELINES = 2;

            /// <summary>
            /// Default max header size for pipelines.
            /// </summary>
            private int[] _driverMaxHeaderSize = new int[_NUM_PIPELINES];

            /// <summary>
            /// Updates the client, processing incoming events.
            /// </summary>
            public void Update()
            {
                if (!connection.IsCreated)
                    return;

                // First complete the job that was initialized in the previous frame
                jobHandle.Complete();

                if (connection.GetState(driver) == NetworkConnection.State.Disconnected)
                    transport.OnDisconnect?.Invoke(ConnectionEndMode.Shutdown, "shutdown");

                // Trigger Mirror callbacks for events that resulted in the last jobs work
                _ProcessIncomingEvents();

                //If driver is active, cache its max header size for UTP transport
                if (driver.IsCreated)
                {
                    jobHandle.Complete();
                    _driverMaxHeaderSize[Channels.RELIABLE] = driver.MaxHeaderSize(reliablePipeline);
                    _driverMaxHeaderSize[Channels.UNRELIABLE] = driver.MaxHeaderSize(unreliablePipeline);
                }

                // Need to ensure the driver did not become inactive
                if (!driver.IsCreated)
                {
                    _driverMaxHeaderSize = new int[_NUM_PIPELINES];
                    return;
                }

                // Create a new job
                var job = new ClientUpdateJob
                {
                    driver = driver,
                    connection = connection,
                    connectionEventsQueue = connectionsEventsQueue.AsParallelWriter()
                };

                // Schedule job
                jobHandle = driver.ScheduleUpdate();
                jobHandle = job.Schedule(jobHandle);
            }

            /// <summary>
            /// Internal method to process incoming events.
            /// </summary>
            private void _ProcessIncomingEvents()
            {
                while (connectionsEventsQueue.TryDequeue(out var connectionEvent))
                {
                    switch (connectionEvent.eventType)
                    {
                        //Connect action 
                        case ((byte)ConnectionEventType.OnConnected):
                        {
                            transport.OnConnect.Invoke();
                            break;
                        }

                        //Receive data action
                        case ((byte)ConnectionEventType.OnReceivedData):
                        {
                            var data = connectionEvent.eventData.ToArray();
                            var binaryReader = new BinaryReader(new MemoryStream(data));

                            while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                            {
                                var id = binaryReader.ReadInt32();
                                var packet = PacketRegistry.CreatePacket(id);

                                packet.Deserialize(binaryReader);
                                transport.OnClientPacketReceived?.Invoke(packet);
                            }

                            break;
                        }

                        //Disconnect action
                        case ((byte)ConnectionEventType.OnDisconnected):
                        {
                            transport.OnDisconnect.Invoke(ConnectionEndMode.Shutdown, "connection_shutdown");
                            break;
                        }
                    }
                }
            }
        }
        #endregion

        #region Buffers
        /// <summary>
        /// Buffer used for sending data.
        /// </summary>
        private static readonly byte[] _Buffer0 = new byte[65535];

        /// <summary>
        /// Writer used for writing data to the buffer.
        /// </summary>
        private static readonly BinaryWriter _Writer0 = new(new MemoryStream(_Buffer0));
        #endregion

        /// <summary>
        /// Used to determine if the transport is using a relay server.
        /// When true, the transport will use the relay server to manage connections.
        /// When false, the transport will use the address and port to manage connections.
        /// </summary>
        public bool usingRelay = true;

        /// <summary>
        /// The timeout for the connection.
        /// </summary>
        public int timeout = 3000;

        /// <summary>
        /// The address of the server.
        /// </summary>
        public string address = "localhost";

        /// <summary>
        /// The port of the server.
        /// </summary>
        public ushort port = 7777;

        /// <summary>
        /// Internal server instance.
        /// </summary>
        private UtpServer _server;

        /// <summary>
        ///  Internal client instance.
        /// </summary>
        private UtpClient _client;

        /// <summary>
        /// Internal join code.
        /// </summary>
        private string _internalJoinCode;

        /// <summary>
        /// Initialize the transport.
        /// </summary>
        private async void OnEnable()
        {
            _server = new UtpServer { transport = this };
            _client = new UtpClient { transport = this };

            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Dispose of native memory and sign out of the relay service.
        /// </summary>
        private void OnDisable()
        {
            _DisposeNativeMemory();

            try
            {
                if (AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SignOut();
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Update the transport, processing incoming events.
        /// </summary>
        private void Update()
        {
            if (Type is EnvironmentType.Server or EnvironmentType.Host)
                _server.Update();
            if (Type is EnvironmentType.Client or EnvironmentType.Host)
                _client.Update();
        }

        public override ServerDiscoverer GetServerDiscoverer()
        {
            return null;
        }

        /// <summary>
        /// Disposes all native memory used by the transport.
        /// </summary>
        private void _DisposeNativeMemory()
        {
            _server.jobHandle.Complete();
            _client.jobHandle.Complete();

            if (_server.connections.IsCreated)
                _server.connections.Dispose();

            if (_server.connectionsEventsQueue.IsCreated)
                _server.connectionsEventsQueue.Dispose();

            if (_client.connectionsEventsQueue.IsCreated)
                _client.connectionsEventsQueue.Dispose();

            if (_client.driver.IsCreated)
                _client.driver.Dispose();

            if (_server.driver.IsCreated)
                _server.driver.Dispose();

            if (_client.connection.IsCreated)
                _client.connection = default;
        }

        public override void StartHost(int magicNumber)
        {
            StartServer();

            if (usingRelay)
            {
                GetAllocationFromJoinCode(_internalJoinCode,
                    () => { StartClient(magicNumber); }, null);
            }
            else
                StartClient(magicNumber);
        }

        public override void StartServer()
        {
            if (Type != EnvironmentType.None)
                throw new InvalidOperationException("Server or Client is already running");

            var settings = new NetworkSettings();
            settings.WithNetworkConfigParameters(disconnectTimeoutMS: timeout);
            settings.WithFragmentationStageParameters(payloadCapacity: 16384);

            //Create IPV4 endpoint
            var endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = port;

            if (usingRelay)
            {
                //Instantiate relay network data
                var relayServerData =
                    RelayUtils.HostRelayData(ServerAllocation, RelayServerEndpoint.NetworkOptions.Udp);

                //Initialize relay network
                settings.WithRelayParameters(ref relayServerData);

                //Instantiate network driver
                _server.driver = NetworkDriver.Create(settings);
            }
            else
            {
                //Instantiate network driver
                _server.driver = NetworkDriver.Create(settings);
                endpoint.Port = port;
            }

            _server.connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
            _server.connectionsEventsQueue = new NativeQueue<UtpConnectionEvent>(Allocator.Persistent);

            _server.reliablePipeline = _server.driver.CreatePipeline(typeof(FragmentationPipelineStage),
                typeof(ReliableSequencedPipelineStage));
            _server.unreliablePipeline = _server.driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));

            _server.driver.Bind(endpoint);
            if (!_server.driver.Bound)
            {
                OnServerStop?.Invoke(ConnectionEndMode.InternalError, $"Cannot bind to port {endpoint.Port}");
                return;
            }

            _server.driver.Listen();
            if (!_server.driver.Listening)
            {
                OnServerStop?.Invoke(ConnectionEndMode.InternalError, $"Failed to listen");
                return;
            }

            Type = EnvironmentType.Server;
            OnServerStart?.Invoke();
        }

        public override void StartClient(int magicNumber)
        {
            if (Type is EnvironmentType.Host or EnvironmentType.Client)
                throw new InvalidOperationException("Client is already running");

            if (usingRelay)
            {
                _client.connectionsEventsQueue = new NativeQueue<UtpConnectionEvent>(Allocator.Persistent);

                var relayServerData =
                    RelayUtils.PlayerRelayData(JoinAllocation, RelayServerEndpoint.NetworkOptions.Udp);

                var settings = new NetworkSettings();
                settings.WithRelayParameters(ref relayServerData);
                settings.WithFragmentationStageParameters(payloadCapacity: 16384);

                _client.driver = NetworkDriver.Create(settings);
                _client.reliablePipeline = _client.driver.CreatePipeline(typeof(FragmentationPipelineStage),
                    typeof(ReliableSequencedPipelineStage));
                _client.unreliablePipeline = _client.driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));

                _client.connection = _client.driver.Connect(relayServerData.Endpoint);

                if (_client.connection.IsCreated)
                {
                    Type = Type is EnvironmentType.None ? EnvironmentType.Client : EnvironmentType.Host;
                }
                else
                    OnDisconnect?.Invoke(ConnectionEndMode.Shutdown, "connection_failed");
            }
            else
            {
                if (string.IsNullOrEmpty(address))
                {
                    OnDisconnect?.Invoke(ConnectionEndMode.Shutdown, "empty_code");
                    return;
                }

                if (address == "localhost")
                {
                    address = "127.0.0.1";
                }

                if (!NetworkEndPoint.TryParse(address, port, out var endpoint))
                {
                    OnDisconnect?.Invoke(ConnectionEndMode.Shutdown, "invalid_address");
                    return;
                }

                _client.connectionsEventsQueue = new NativeQueue<UtpConnectionEvent>(Allocator.Persistent);

                var settings = new NetworkSettings();
                settings.WithNetworkConfigParameters(disconnectTimeoutMS: timeout);

                _client.driver = NetworkDriver.Create(settings);
                _client.reliablePipeline = _client.driver.CreatePipeline(typeof(FragmentationPipelineStage),
                    typeof(ReliableSequencedPipelineStage));
                _client.unreliablePipeline = _client.driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));

                _client.connection = _client.driver.Connect(endpoint);

                if (_client.connection.IsCreated)
                {
                    Type = Type is EnvironmentType.None ? EnvironmentType.Client : EnvironmentType.Host;
                }
                else
                {
                    OnDisconnect?.Invoke(ConnectionEndMode.Shutdown, "connection_failed");
                }
            }
        }

        /// <summary>
        ///     Holds a connection information along with the remote client id.
        /// </summary>
        private class RelayConnectionInfo : IClientConnectionInfo
        {
            public int Latency { get; } = 0;
            public long PacketSent { get; } = 0;
            public long PacketReceived { get; } = 0;
            public long PacketLoss { get; } = 0;
            public int Id { get; set; }
        }

        public override void Close()
        {
            _DisposeNativeMemory();

            switch (Type)
            {
                case EnvironmentType.Server:
                {
                    OnServerStop?.Invoke(ConnectionEndMode.Shutdown, "shutdown");
                    break;
                }
                case EnvironmentType.Client:
                {
                    OnDisconnect?.Invoke(ConnectionEndMode.Shutdown, "shutdown");
                    break;
                }
                case EnvironmentType.Host:
                {
                    OnServerStop?.Invoke(ConnectionEndMode.Shutdown, "shutdown");
                    OnDisconnect?.Invoke(ConnectionEndMode.Shutdown, "shutdown");
                    break;
                }
            }

            _server = new UtpServer { transport = this };
            _client = new UtpClient { transport = this };
            Type = EnvironmentType.None;
        }

        public override IClientConnectionInfo GetClientInfo(int id)
        {
            if (!_server.driver.IsCreated)
                return null;

            _server.jobHandle.Complete();
            if (_server.TryGetConnection(id, out _))
            {
                return new RelayConnectionInfo { Id = id };
            }

            return null;
        }

        public override int GetClientCount()
        {
            if (!_server.driver.IsCreated)
                return 0;

            _server.jobHandle.Complete();
            return _server.connections.Length;
        }

        public override IEnumerable<IClientConnectionInfo> GetClients()
        {
            if (!_server.driver.IsCreated)
                yield break;

            _server.jobHandle.Complete();
            foreach (var connection in _server.connections)
            {
                yield return new RelayConnectionInfo { Id = connection.GetHashCode() };
            }
        }

        public override void ClientDisconnect(string reason)
        {
            if (!_client.driver.IsCreated)
                return;

            _client.jobHandle.Complete();
            Close();
        }

        public override void ServerDisconnect(int id, string reason)
        {
            if (!_server.driver.IsCreated)
                return;

            _server.jobHandle.Complete();
            _server.Disconnect(id);
        }

        public override void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            _client.jobHandle.Complete();

            _Writer0.BaseStream.Position = 0;
            var id = PacketRegistry.GetId(packet);
            _Writer0.Write(id);
            packet.Serialize(_Writer0);
            var segment = new ArraySegment<byte>(_Buffer0, 0, (int)_Writer0.BaseStream.Position);

            //Get pipeline for job
            var pipeline = reliable ? _client.reliablePipeline : _client.unreliablePipeline;

            //Convert ArraySegment to NativeArray for burst compile
            var segmentArray = new NativeArray<byte>(segment.Count, Allocator.Persistent);
            NativeArray<byte>.Copy(segment.Array, segment.Offset, segmentArray, 0, segment.Count);

            // Create a new job
            var job = new ClientSendJob
            {
                driver = _client.driver,
                pipeline = pipeline,
                connection = _client.connection,
                data = segmentArray
            };

            // Schedule job
            _client.jobHandle = job.Schedule(_client.jobHandle);
        }

        public override void ServerSendPacket(IPacket packet, int target = -1, bool reliable = false)
        {
            _Writer0.BaseStream.Position = 0;
            var id = PacketRegistry.GetId(packet);
            _Writer0.Write(id);
            packet.Serialize(_Writer0);
            var segment = new ArraySegment<byte>(_Buffer0, 0, (int)_Writer0.BaseStream.Position);

            if (target == -1)
            {
                _server.jobHandle.Complete();

                foreach (var con in _server.connections)
                {
                    _SendTo(segment, con.GetHashCode(), reliable);
                }
            }
            else
                _SendTo(segment, target, reliable);
        }

        private void _SendTo(ArraySegment<byte> segment, int connectionId, bool reliable)
        {
            _server.jobHandle.Complete();


            if (_server.TryGetConnection(connectionId, out NetworkConnection connection))
            {
                //Get pipeline for job
                var pipeline = reliable ? _server.reliablePipeline : _server.unreliablePipeline;

                //Convert ArraySegment to NativeArray for burst compile
                var segmentArray = new NativeArray<byte>(segment.Count, Allocator.Persistent);
                NativeArray<byte>.Copy(segment.Array, segment.Offset, segmentArray, 0, segment.Count);

                // Create a new job
                var job = new ServerSendJob
                {
                    driver = _server.driver,
                    pipeline = pipeline,
                    connection = connection,
                    data = segmentArray
                };

                // Schedule job
                _server.jobHandle = job.Schedule(_server.jobHandle);
            }
        }

        #region Relay
        /// <summary>
        /// The allocation managed by a host who is running as a client and server.
        /// </summary>
        public Allocation ServerAllocation { get; set; }

        /// <summary>
        /// The allocation managed by a client who is connecting to a server.
        /// </summary>
        public JoinAllocation JoinAllocation { get; set; }

        /// <summary>
        /// A callback for when a Relay server is allocated and a join code is fetched.
        /// </summary>
        public Action<string, string> OnRelayServerAllocated { get; set; }

        /// <summary>
        /// The interface to the Relay services API.
        /// </summary>
        public IRelayServiceSDK RelayServiceSDK { get; } = new WrappedRelayServiceSDK();

        /// <summary>
        /// Retrieve the <seealso cref="Unity.Services.Relay.Models.JoinAllocation"/> corresponding to the specified join code.
        /// </summary>
        /// <param name="joinCode">The join code that will be used to retrieve the JoinAllocation.</param>
        /// <param name="onSuccess">A callback to invoke when the Relay allocation is successfully retrieved from the join code.</param>
        /// <param name="onFailure">A callback to invoke when the Relay allocation is unsuccessfully retrieved from the join code.</param>
        public void GetAllocationFromJoinCode(string joinCode, Action onSuccess, Action onFailure)
        {
            usingRelay = true;
            StartCoroutine(GetAllocationFromJoinCodeTask(joinCode, onSuccess, onFailure));
        }

        private IEnumerator GetAllocationFromJoinCodeTask(string joinCode, Action onSuccess, Action onFailure)
        {
            var joinAllocation = RelayServiceSDK.JoinAllocationAsync(joinCode);

            while (!joinAllocation.IsCompleted)
            {
                yield return null;
            }

            if (joinAllocation.IsFaulted)
            {
                joinAllocation.Exception!.Flatten().Handle((_) => true);

                onFailure?.Invoke();
                yield break;
            }

            JoinAllocation = joinAllocation.Result;
            onSuccess?.Invoke();
        }

        /// <summary>
        /// Get a list of Regions from the Relay Service.
        /// </summary>
        /// <param name="onSuccess">A callback to invoke when the list of regions is successfully retrieved.</param>
        /// <param name="onFailure">A callback to invoke when the list of regions is unsuccessfully retrieved.</param>
        public void GetRelayRegions(Action<List<Region>> onSuccess, Action onFailure)
        {
            StartCoroutine(GetRelayRegionsTask(onSuccess, onFailure));
        }

        /// <summary>
        /// Internal method to get a list of Regions from the Relay Service.
        /// </summary>
        /// <param name="onSuccess"></param>
        /// <param name="onFailure"></param>
        /// <returns></returns>
        private IEnumerator GetRelayRegionsTask(Action<List<Region>> onSuccess, Action onFailure)
        {
            var listRegions = RelayServiceSDK.ListRegionsAsync();

            while (!listRegions.IsCompleted)
            {
                yield return null;
            }

            if (listRegions.IsFaulted)
            {
                listRegions.Exception!.Flatten().Handle((_) => true);

                onFailure?.Invoke();
                yield break;
            }

            onSuccess?.Invoke(listRegions.Result);
        }

        /// <summary>
        /// Allocate a Relay Server.
        /// </summary>
        /// <param name="maxPlayers">The max number of players that may connect to this server.</param>
        /// <param name="regionId">The region to allocate the server in. May be null.</param>
        /// <param name="onSuccess">A callback to invoke when the Relay server is successfully allocated.</param>
        /// <param name="onFailure">A callback to invoke when the Relay server is unsuccessfully allocated.</param>
        public void AllocateRelayServer(int maxPlayers, string regionId, Action<string> onSuccess, Action onFailure)
        {
            usingRelay = true;
            StartCoroutine(AllocateRelayServerTask(maxPlayers, regionId, onSuccess, onFailure));
        }

        private IEnumerator AllocateRelayServerTask(int maxPlayers, string regionId, Action<string> onSuccess,
            Action onFailure)
        {
            Task<Allocation> createAllocation = RelayServiceSDK.CreateAllocationAsync(maxPlayers, regionId);

            while (!createAllocation.IsCompleted)
            {
                yield return null;
            }

            if (createAllocation.IsFaulted)
            {
                createAllocation.Exception!.Flatten().Handle((_) => true);

                onFailure?.Invoke();
                yield break;
            }

            ServerAllocation = createAllocation.Result;
            StartCoroutine(GetJoinCodeTask(onSuccess, onFailure));
        }

        private IEnumerator GetJoinCodeTask(Action<string> onSuccess, Action onFailure)
        {
            var getJoinCode = RelayServiceSDK.GetJoinCodeAsync(ServerAllocation.AllocationId);

            while (!getJoinCode.IsCompleted)
            {
                yield return null;
            }

            if (getJoinCode.IsFaulted)
            {
                getJoinCode.Exception!.Flatten().Handle((_) => true);

                onFailure?.Invoke();

                yield break;
            }

            _internalJoinCode = getJoinCode.Result;
            onSuccess?.Invoke(getJoinCode.Result);
        }
        #endregion
    }

    /// <summary>
    /// Class to wrap the Relay SDK for use in Unity.
    /// </summary>
    public class WrappedRelayServiceSDK : IRelayServiceSDK
    {
        /// <summary>
        /// Create a new allocation.
        /// </summary>
        /// <param name="maxConnections"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public Task<Allocation> CreateAllocationAsync(int maxConnections, string region = null)
        {
            return Relay.Instance.CreateAllocationAsync(maxConnections, region);
        }

        /// <summary>
        /// Retrieve the join code for an allocation.
        /// </summary>
        /// <param name="allocationId"></param>
        /// <returns></returns>
        public Task<string> GetJoinCodeAsync(Guid allocationId)
        {
            return Relay.Instance.GetJoinCodeAsync(allocationId);
        }

        /// <summary>
        /// Retrieve the allocation from a join code.
        /// </summary>
        /// <param name="joinCode"></param>
        /// <returns></returns>
        public Task<JoinAllocation> JoinAllocationAsync(string joinCode)
        {
            return Relay.Instance.JoinAllocationAsync(joinCode);
        }

        /// <summary>
        /// List all regions available for allocation.
        /// </summary>
        /// <returns></returns>
        public Task<List<Region>> ListRegionsAsync()
        {
            return Relay.Instance.ListRegionsAsync();
        }
    }
}