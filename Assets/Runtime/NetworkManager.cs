using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AYellowpaper.SerializedCollections;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using NetBuff.Packets;
using NetBuff.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NetBuff
{
    /// <summary>
    ///     Main NetBuff component that manages the network environment, network objects, network behaviours callbacks, packet
    ///     sending and receiving, session data, and scene management.
    ///     This component should be placed in the scene that will be used as the main scene for the network environment.
    ///     NetworkManager is a singleton class, meaning that only one instance of this component can exist in the scene.
    /// </summary>
    [Icon("Assets/Editor/Icons/NetworkManager.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-manager")]
    public class NetworkManager : MonoBehaviour
    {
        private static readonly FieldInfo _IDField =
            typeof(NetworkIdentity).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo _OwnerIdField =
            typeof(NetworkIdentity).GetField("ownerId", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo _PrefabIdField =
            typeof(NetworkIdentity).GetField("prefabId", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo _ClientIdField =
            typeof(SessionData).GetField("_clientId", BindingFlags.NonPublic | BindingFlags.Instance);

        #region Inspector Fields
        [Header("SETTINGS")]
        [SerializeField]
        protected new string name = "server";

        [SerializeField]
        protected int versionMagicNumber;

        [SerializeField]
        protected int defaultTickRate = 50;

        [SerializeField]
        protected bool spawnsPlayer = true;

        [SerializeField]
        protected bool supportsSessionRestoration = true;

        [Header("REFERENCES")]
        [SerializeField]
        protected NetworkTransport transport;

        [SerializeField]
        protected NetworkPrefabRegistry prefabRegistry;

        [SerializeField]
        protected GameObject playerPrefab;
        #endregion

        #region Internal Fields
        private int[] _localClientIds = Array.Empty<int>();
        private static readonly MemoryStream _SessionStream = new();
        private static readonly BinaryWriter _SessionWriter = new(_SessionStream);
        private readonly Dictionary<int, SessionData> _localSessionData = new();
        private readonly List<SessionData> _disconnectedSessionData = new();
        private readonly Dictionary<int, SessionData> _sessionData = new();

        [SerializeField]
        [HideInInspector]
        private List<string> loadedScenes = new();

        [SerializeField]
        [HideInInspector]
        private string mainScene;

        private readonly Dictionary<Type, PacketListener> _packetListeners = new();

        [SerializeField]
        [HideInInspector]
        private SerializedDictionary<NetworkId, NetworkIdentity> networkObjects = new();

        [SerializeField]
        [HideInInspector]
        private List<NetworkId> removedPreExistingObjects = new();

        [SerializeField]
        [HideInInspector]
        private List<NetworkBehaviour> dirtyBehaviours = new();

        #if UNITY_EDITOR
        [SerializeField]
        [HideInInspector]
        private NetworkTransport.EnvironmentType environmentTypeAfterReload = NetworkTransport.EnvironmentType.None;

        [SerializeField]
        [HideInInspector]
        protected bool isClientReloaded;

        [Serializable]
        private class PersistentSessionData
        {
            public int clientId;
            public byte[] data;
        }

        [SerializeField]
        [HideInInspector]
        private SerializedDictionary<int, PersistentSessionData> persistentSessionData = new();

        [SerializeField]
        [HideInInspector]
        private List<PersistentSessionData> persistentDisconnectedSessionData = new();
        #endif
        #endregion

        #region Helper Properties
        /// <summary>
        ///     Singleton instance of the NetworkManager component.
        /// </summary>
        public static NetworkManager Instance { get; private set; }

        /// <summary>
        ///     Name of the network environment. Used to name the server.
        /// </summary>
        public string Name
        {
            get => name;
            set => name = value;
        }

        /// <summary>
        ///     Used to check if versions and projects and compatible.
        ///     When a client connects to a server, the server will check if the client's version magic number matches the server's
        ///     version magic number.
        /// </summary>
        public int VersionMagicNumber
        {
            get => versionMagicNumber;
            set => versionMagicNumber = value;
        }

        /// <summary>
        ///     Default tick rate of the network environment.
        ///     Used by components that require a tick rate, such as NetworkTransform.
        /// </summary>
        public int DefaultTickRate
        {
            get => defaultTickRate;
            set => defaultTickRate = value;
        }

        /// <summary>
        ///     Determines if the server should spawn a player object for the client when they connect.
        /// </summary>
        public bool SpawnsPlayer
        {
            get => spawnsPlayer;
            set => spawnsPlayer = value;
        }

        /// <summary>
        ///     Determines if the network environment supports session restoration.
        /// </summary>
        public bool SupportsSessionRestoration
        {
            get => supportsSessionRestoration;
            set => supportsSessionRestoration = value;
        }

        /// <summary>
        ///     Determines the network transport used by the network environment.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public NetworkTransport Transport
        {
            get => transport;
            set
            {
                if (EnvironmentType != NetworkTransport.EnvironmentType.None)
                    throw new Exception("Cannot change transport while running");
                transport = value;
            }
        }

        /// <summary>
        ///     Determines the network prefab registry used by the network environment.
        /// </summary>
        public NetworkPrefabRegistry PrefabRegistry
        {
            get => prefabRegistry;
            set => prefabRegistry = value;
        }

        /// <summary>
        ///     Determines the player prefab used by the network environment.
        ///     The prefab needs to be registered in the network prefab registry.
        /// </summary>
        public GameObject PlayerPrefab
        {
            get => playerPrefab;
            set => playerPrefab = value;
        }

        /// <summary>
        ///     Determines if the local client is running.
        /// </summary>
        public bool IsClientRunning { get; protected set; }

        /// <summary>
        ///     Determines if the local server is running.
        /// </summary>
        public bool IsServerRunning { get; protected set; }

        /// <summary>
        ///     Returns all network objects that have to be synchronized.
        /// </summary>
        public IList<NetworkBehaviour> DirtyBehaviours => dirtyBehaviours;

        /// <summary>
        ///     Returns the environment type of the network transport.
        /// </summary>
        public NetworkTransport.EnvironmentType EnvironmentType => transport.Type;

        /// <summary>
        ///     Returns the connection info of the client.
        ///     Can only be accessed on the client.
        /// </summary>
        [ClientOnly]
        public IConnectionInfo ClientConnectionInfo => transport.ClientConnectionInfo;

        /// <summary>
        ///     Returns all the local client ids.
        ///     Can only be accessed on the client.
        /// </summary>
        [ClientOnly]
        public ReadOnlySpan<int> LocalClientIds => _localClientIds;

        /// <summary>
        ///     Returns the name of the main scene.
        ///     The main scene is the scene where the NetworkManager component is placed.
        /// </summary>
        public string MainScene => mainScene;

        /// <summary>
        ///     Returns all the loaded scenes.
        /// </summary>
        public IEnumerable<string> LoadedScenes => loadedScenes;

        /// <summary>
        ///     Returns the number of loaded scenes.
        /// </summary>
        public int LoadedSceneCount => loadedScenes.Count;

        /// <summary>
        ///     Returns the current last loaded scene.
        ///     If a object is spawned or moved to the -1 scene, it will be placed on the last loaded scene.
        /// </summary>
        public string LastLoadedScene => loadedScenes.Count == 0 ? MainScene : loadedScenes.LastOrDefault();
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            //Clears all the actions
            NetworkAction.ClearAll();

            Instance = this;
            PacketRegistry.Clear();

            var types = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                from type in assembly.GetTypes()
                where type.IsClass && !type.IsAbstract && typeof(IPacket).IsAssignableFrom(type)
                select type).ToList();
            types.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

            foreach (var type in types)
                PacketRegistry.RegisterPacket(type);

            if (transport == null)
            {
                enabled = false;
                throw new Exception("Transport is not set");
            }

            transport.OnServerPacketReceived += OnServerReceivePacket;
            transport.OnClientPacketReceived += OnClientReceivePacket;
            transport.OnClientConnected += OnClientConnected;
            transport.OnClientDisconnected += OnClientDisconnected;
            transport.OnConnect += OnConnect;
            transport.OnDisconnect += OnDisconnect;
            transport.OnServerStart += OnServerStart;
            transport.OnServerStop += OnServerStop;
            transport.OnServerError += OnServerError;
            transport.OnClientError += OnClientError;

            #if UNITY_EDITOR
            switch (environmentTypeAfterReload)
            {
                case NetworkTransport.EnvironmentType.Host:
                    StartHost();
                    isClientReloaded = true;
                    break;
                case NetworkTransport.EnvironmentType.Server:
                    StartServer();
                    isClientReloaded = false;
                    break;

                case NetworkTransport.EnvironmentType.None:
                    isClientReloaded = false;
                    networkObjects.Clear();
                    foreach (var identity in FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include,
                                 FindObjectsSortMode.None))
                    {
                        if (networkObjects.TryGetValue(identity.Id, out var i))
                            throw new Exception("Duplicate NetworkObject found: " + identity.gameObject.name + " " +
                                                i.gameObject.name);
                        networkObjects.Add(identity.Id, identity);
                    }

                    break;
                case NetworkTransport.EnvironmentType.Client:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            environmentTypeAfterReload = NetworkTransport.EnvironmentType.None;

            foreach (var data in persistentSessionData)
            {
                var stream = new MemoryStream(data.Value.data);
                var reader = new BinaryReader(stream);
                var sessionData = OnCreateEmptySessionData();
                sessionData.Deserialize(reader, true);
                _ClientIdField.SetValue(sessionData, data.Value.clientId);
                _sessionData[data.Key] = sessionData;
            }

            foreach (var data in persistentDisconnectedSessionData)
            {
                var stream = new MemoryStream(data.data);
                var reader = new BinaryReader(stream);
                var sessionData = OnCreateEmptySessionData();
                sessionData.Deserialize(reader, true);
                _ClientIdField.SetValue(sessionData, data.clientId);
                _disconnectedSessionData.Add(sessionData);
            }

            persistentSessionData.Clear();
            persistentDisconnectedSessionData.Clear();
            #else
            networkObjects.Clear();
            foreach (var identity in FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (networkObjects.TryGetValue(identity.Id, out var i))
                    throw new Exception("Duplicate NetworkObject found: " + identity.gameObject.name + " " +
                                        i.gameObject.name);
                networkObjects.Add(identity.Id, identity);
            }
            #endif

            mainScene = gameObject.scene.name;
            loadedScenes.Add(mainScene);

            if (SupportsSessionRestoration)
                _disconnectedSessionData.AddRange(_sessionData.Values);
            else
                _disconnectedSessionData.Clear();
            _sessionData.Clear();
        }

        private void OnDisable()
        {
            if (transport == null)
                return;

            #if UNITY_EDITOR
            environmentTypeAfterReload = EnvironmentType switch
            {
                NetworkTransport.EnvironmentType.Host => NetworkTransport.EnvironmentType.Host,
                NetworkTransport.EnvironmentType.Server => NetworkTransport.EnvironmentType.Server,
                _ => NetworkTransport.EnvironmentType.None
            };

            foreach (var data in _sessionData)
            {
                var stream = new MemoryStream();
                var writer = new BinaryWriter(stream);
                data.Value.Serialize(writer, true);
                persistentSessionData[data.Key] = new PersistentSessionData
                {
                    clientId = data.Key,
                    data = stream.ToArray()
                };
            }

            foreach (var data in _disconnectedSessionData)
            {
                var stream = new MemoryStream();
                var writer = new BinaryWriter(stream);
                data.Serialize(writer, true);
                persistentDisconnectedSessionData.Add(new PersistentSessionData
                {
                    clientId = data.ClientId,
                    data = stream.ToArray()
                });
            }
            #endif
            transport.Close();
            Instance = null;
        }

        private void Update()
        {
            foreach (var behaviour in dirtyBehaviours)
                behaviour.UpdateDirtyValues();

            dirtyBehaviours.Clear();
        }
        #endregion

        #region Helper Methods
        /// <summary>
        ///     Starts the network environment as client.
        /// </summary>
        public void StartClient()
        {
            transport.StartClient(versionMagicNumber);
        }

        /// <summary>
        ///     Starts the network environment as server.
        /// </summary>
        public void StartServer()
        {
            transport.StartServer();
        }

        /// <summary>
        ///     Starts the network environment as host (server and client).
        /// </summary>
        public void StartHost()
        {
            transport.StartHost(versionMagicNumber);
        }

        /// <summary>
        ///     Closes the network environment.
        /// </summary>
        public void Close()
        {
            transport.Close();
        }
        #endregion

        #region Listeners
        /// <summary>
        ///     Returns the packet listener for the given packet type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PacketListener<T> GetPacketListener<T>() where T : IPacket
        {
            if (_packetListeners.TryGetValue(typeof(T), out var listener))
                return (PacketListener<T>)listener;

            listener = new PacketListener<T>();
            _packetListeners.Add(typeof(T), listener);

            return (PacketListener<T>)listener;
        }

        /// <summary>
        ///     Returns the packet listener for the given packet type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public PacketListener GetPacketListener(Type type)
        {
            if (_packetListeners.TryGetValue(type, out var listener))
                return listener;

            listener = (PacketListener)Activator.CreateInstance(typeof(PacketListener<>).MakeGenericType(type));
            _packetListeners.Add(type, listener);

            return listener;
        }
        #endregion

        #region Network Object Methods
        /// <summary>
        ///     Returns the network identity object with the given id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public NetworkIdentity GetNetworkObject(NetworkId id)
        {
            return networkObjects.GetValueOrDefault(id);
        }

        /// <summary>
        ///     Returns all the network identity objects.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<NetworkIdentity> GetNetworkObjects()
        {
            return networkObjects.Values;
        }

        /// <summary>
        ///     Returns the number of network identity objects.
        /// </summary>
        /// <returns></returns>
        public int GetNetworkObjectCount()
        {
            return networkObjects.Count;
        }

        /// <summary>
        ///     Returns all the network identity objects owned by the given client.
        ///     If the client id is -1, it returns all the objects owned by the server.
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        public IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int owner)
        {
            return networkObjects.Values.Where(identity => identity.OwnerId == owner);
        }
        #endregion

        #region Virtual Methods
        /// <summary>
        ///     Called when a error occurs on the server.
        /// </summary>
        /// <param name="error"></param>
        protected virtual void OnServerError(string error)
        {
            Debug.LogError($"[Server] {error}");
        }

        /// <summary>
        ///     Called when a error occurs on the client.
        /// </summary>
        /// <param name="error"></param>
        protected virtual void OnClientError(string error)
        {
            Debug.LogError($"[Client] {error}");
        }

        /// <summary>
        ///     Called when the server starts.
        /// </summary>
        protected virtual void OnServerStart()
        {
            IsServerRunning = true;

            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSpawned(false);
        }

        /// <summary>
        ///     Called when the server stops.
        /// </summary>
        protected virtual void OnServerStop()
        {
            IsServerRunning = false;
            if (transport.Type is NetworkTransport.EnvironmentType.Server)
                OnClearEnvironment();
        }

        /// <summary>
        ///     Called when a network object is spawned.
        ///     /// The isRetroactive parameter is true if the client is joining the server after the object is already spawned.
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="retroactive"></param>
        protected virtual void OnNetworkObjectSpawned(NetworkIdentity identity, bool retroactive)
        {
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnSpawned(retroactive);

            foreach (var behaviour in identity.Behaviours)
                behaviour.OnActiveChanged(identity.gameObject.activeInHierarchy);

            foreach (var obj in networkObjects.Values)
                foreach (var behaviour in obj.Behaviours)
                    behaviour.OnAnyObjectSpawned(identity, retroactive);

            NetworkAction.OnObjectSpawn.Invoke(identity.Id, identity);
        }

        /// <summary>
        ///     Called when a network object is despawned.
        /// </summary>
        /// <param name="identity"></param>
        protected virtual void OnNetworkObjectDespawned(NetworkIdentity identity)
        {
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnDespawned();

            NetworkAction.OnObjectDespawn.Invoke(identity.Id, identity);
        }

        /// <summary>
        ///     Called when a client connects to the server.
        ///     Only called on the server.
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        protected virtual void OnClientConnected(int clientId)
        {
            var idPacket = new NetworkClientIdPacket { ClientId = clientId };
            transport.ServerSendPacket(idPacket, clientId, true);
        }

        /// <summary>
        ///     Called when a client disconnects from the server.
        ///     Only called on the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="reason"></param>
        [ServerOnly]
        protected virtual void OnClientDisconnected(int clientId, string reason)
        {
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnClientDisconnected(clientId);

            var toDestroy = GetNetworkObjectsOwnedBy(clientId).ToList();
            foreach (var id in toDestroy)
                DespawnNetworkObjectForClients(id.Id);

            var data = _sessionData.TryGetValue(clientId, out var d) ? d : null;
            if (SupportsSessionRestoration && data != null)
                _disconnectedSessionData.Add(data);
            _sessionData.Remove(clientId);
        }

        /// <summary>
        ///     Called when the server spawns a player object for the client.
        ///     Only called on the server.
        ///     Can be used to customize the player object spawn, such as setting the position and rotation.
        /// </summary>
        /// <param name="clientId"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        protected virtual void OnSpawnPlayer(int clientId)
        {
            if (!prefabRegistry.IsPrefabValid(playerPrefab))
                throw new Exception("Player prefab is not valid");
            SpawnNetworkObjectForClients(prefabRegistry.GetPrefabId(playerPrefab), Vector3.zero, Quaternion.identity,
                Vector3.one, clientId, 0);
        }

        /// <summary>
        ///     Called when the client connects to the server.
        ///     Only called on the client.
        /// </summary>
        [ClientOnly]
        protected virtual void OnConnect()
        {
            IsClientRunning = true;


            var packet = OnCreateSessionEstablishRequest();
            ClientSendPacket(packet);
        }

        /// <summary>
        ///     Called when the client disconnects from the server.
        ///     Only called on the client.
        /// </summary>
        /// <param name="reason"></param>
        [ClientOnly]
        protected virtual void OnDisconnect(string reason)
        {
            IsClientRunning = false;

            #if UNITY_EDITOR
            if (environmentTypeAfterReload == NetworkTransport.EnvironmentType.None)
            #endif
            {
                foreach (var identity in networkObjects.Values)
                    foreach (var behaviour in identity.Behaviours)
                        behaviour.OnActiveChanged(false);

                OnClearEnvironment();
            }
        }

        /// <summary>
        ///     Called when the server receives a packet from a client.
        ///     Only called on the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="packet"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        protected virtual void OnServerReceivePacket(int clientId, IPacket packet)
        {
            switch (packet)
            {
                case NetworkSessionEstablishRequestPacket establishPacket:
                {
                    var response = OnSessionEstablishingRequest(establishPacket);
                    if (response.Type == SessionEstablishingResponse.SessionEstablishingResponseType.Reject)
                    {
                        transport.ServerDisconnect(clientId, response.Reason);
                        return;
                    }

                    var data = (SupportsSessionRestoration ? OnRestoreSessionData(clientId, establishPacket) : null) ??
                               OnCreateNewSessionData(clientId, establishPacket);
                    _ClientIdField.SetValue(data, clientId);

                    _sessionData[clientId] = data ?? throw new Exception("Session data is null");
                    _disconnectedSessionData.Remove(data);

                    SendSessionDataToClient(clientId);
                    _SendClientPreExistingInfo(clientId);
                    return;
                }

                case NetworkPreExistingResponsePacket _:
                {
                    #if UNITY_EDITOR
                    if (!isClientReloaded)
                        if (spawnsPlayer)
                            OnSpawnPlayer(clientId);
                    #else
                    if (spawnsPlayer) OnSpawnPlayer(clientId);
                    #endif
                    foreach (var identity in networkObjects.Values)
                        foreach (var behaviour in identity.Behaviours)
                            behaviour.OnClientConnected(clientId);
                    return;
                }

                case NetworkBehaviourDataPacket valuesPacket:
                {
                    if (!networkObjects.TryGetValue(valuesPacket.Id, out _)) return;
                    BroadcastServerPacketExceptFor(valuesPacket, clientId, true);
                    return;
                }

                case NetworkObjectSpawnPacket spawnPacket:
                {
                    if (networkObjects.ContainsKey(spawnPacket.Id)) return;
                    if (!prefabRegistry.IsPrefabValid(spawnPacket.PrefabId)) return;
                    BroadcastServerPacket(spawnPacket, true);
                    return;
                }

                case NetworkObjectDespawnPacket destroyPacket:
                {
                    if (!networkObjects.TryGetValue(destroyPacket.Id, out var identity)) return;
                    if (identity.OwnerId != clientId)
                    {
                        Debug.LogWarning(
                            $"Client {clientId} tried to destroy object {destroyPacket.Id} which it does not own");
                        return;
                    }

                    DespawnNetworkObjectForClients(destroyPacket.Id);
                    return;
                }

                case NetworkObjectActivePacket activePacket:
                {
                    if (!networkObjects.TryGetValue(activePacket.Id, out var identity)) return;
                    if (identity.OwnerId != clientId)
                    {
                        Debug.LogWarning(
                            $"Client {clientId} tried to change active state of object {activePacket.Id} which it does not own");
                        return;
                    }

                    SetNetworkObjectActiveForClients(activePacket.Id, activePacket.IsActive);
                    return;
                }

                case NetworkObjectOwnerPacket authorityPacket:
                {
                    if (!networkObjects.TryGetValue(authorityPacket.Id, out var identity)) return;
                    if (identity.OwnerId != clientId)
                    {
                        Debug.LogWarning(
                            $"Client {clientId} tried to change owner of object {authorityPacket.Id} which it does not own");
                        return;
                    }

                    SetNetworkObjectOwnerForClients(authorityPacket.Id, authorityPacket.OwnerId);
                    return;
                }

                case NetworkObjectMoveScenePacket moveObjectScenePacket:
                {
                    if (!networkObjects.TryGetValue(moveObjectScenePacket.Id, out var identity)) return;
                    if (identity.OwnerId != clientId)
                    {
                        Debug.LogWarning(
                            $"Client {clientId} tried to move object {moveObjectScenePacket.Id} which it does not own to another scene");
                        return;
                    }

                    MoveObjectToScene(moveObjectScenePacket.Id, moveObjectScenePacket.SceneId);
                    return;
                }

                case IOwnedPacket ownedPacket:
                {
                    if (!networkObjects.TryGetValue(ownedPacket.Id, out var identity)) return;

                    foreach (var behaviour in identity.Behaviours)
                        behaviour.OnServerReceivePacket(ownedPacket, clientId);
                    return;
                }
            }

            GetPacketListener(packet.GetType()).CallOnServerReceive(packet, clientId);
        }

        /// <summary>
        ///     Called when the client receives a packet from the server.
        ///     Only called on the client.
        /// </summary>
        /// <param name="packet"></param>
        protected virtual void OnClientReceivePacket(IPacket packet)
        {
            switch (packet)
            {
                case NetworkClientIdPacket clientPacket:
                    _HandleNetworkClientIdPacket(clientPacket);
                    return;

                case NetworkSessionDataPacket sessionDataPacket:
                {
                    _HandleNetworkSessionDataPacket(sessionDataPacket);
                    return;
                }

                case NetworkBehaviourDataPacket valuesPacket:
                    _HandleNetworkBehaviourDataPacket(valuesPacket);
                    return;

                case NetworkObjectSpawnPacket spawnPacket:
                    _HandleSpawnPacket(spawnPacket, false);
                    return;

                case NetworkObjectDespawnPacket destroyPacket:
                    _HandleDespawnPacket(destroyPacket);
                    return;

                case NetworkObjectActivePacket activePacket:
                    _HandleActivePacket(activePacket);
                    return;

                case NetworkObjectOwnerPacket authorityPacket:
                    _HandleOwnerPacket(authorityPacket);
                    return;

                case NetworkPreExistingInfoPacket preExistingInfoPacket:
                    _HandlePreExistingInfoPacket(preExistingInfoPacket);
                    return;

                case NetworkLoadScenePacket loadScenePacket:
                    _HandleLoadScenePacket(loadScenePacket);
                    return;

                case NetworkObjectMoveScenePacket moveObjectScenePacket:
                    _HandleObjectMoveScenePacket(moveObjectScenePacket);
                    return;

                case NetworkUnloadScenePacket unloadScenePacket:
                    _HandleUnloadScenePacket(unloadScenePacket);
                    return;

                case IOwnedPacket ownedPacket:
                {
                    if (!networkObjects.TryGetValue(ownedPacket.Id, out var identity))
                        return;

                    foreach (var behaviour in identity.Behaviours)
                        behaviour.OnClientReceivePacket(ownedPacket);
                    return;
                }
            }

            GetPacketListener(packet.GetType()).CallOnClientReceive(packet);
        }

        /// <summary>
        ///     Called when the network environment is cleared.
        ///     Occurs when the server stops or the client disconnects.
        /// </summary>
        protected virtual void OnClearEnvironment()
        {
            SceneManager.LoadScene(MainScene);
        }

        /// <summary>
        ///     Called when a network object is spawned.
        ///     Can be used to customize the object spawn, such as setting the position and rotation.
        ///     Note: Changes made here are not synchronized through the network automatically and may be overwritten by the object
        ///     owner.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="prefabId"></param>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="active"></param>
        /// <param name="owner"></param>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        protected virtual GameObject OnSpawnObject(NetworkId id, NetworkId prefabId, GameObject prefab,
            Vector3 position,
            Quaternion rotation, Vector3 scale, ref bool active, ref int owner, ref int sceneId)
        {
            var obj = Instantiate(prefab, position, rotation);
            obj.transform.localScale = scale;
            return obj;
        }

        /// <summary>
        ///     Called when a network object is despawned.
        /// </summary>
        /// <param name="o"></param>
        protected virtual void OnDespawnObject(GameObject o)
        {
            Destroy(o);
        }

        /// <summary>
        ///     Used to create a empty session data object.
        ///     No data should be initialized here.
        /// </summary>
        /// <returns></returns>
        protected virtual SessionData OnCreateEmptySessionData()
        {
            return new SessionData();
        }

        /// <summary>
        ///     Used to create a session establish request packet.
        ///     Called when the client connects to the server.
        ///     Called only on the client.
        /// </summary>
        /// <returns></returns>
        [ClientOnly]
        protected virtual NetworkSessionEstablishRequestPacket OnCreateSessionEstablishRequest()
        {
            return new NetworkSessionEstablishRequestPacket();
        }

        /// <summary>
        ///     Called when the session data of the client changes.
        ///     Changes only occur when the server updates the session data via ApplyChanges
        ///     Called only on the client.
        /// </summary>
        /// <param name="data"></param>
        [ClientOnly]
        protected virtual void OnLocalSessionDataChanged(SessionData data)
        {
        }

        /// <summary>
        ///     Used to restore the session data of the client.
        ///     If the client has disconnected and the server supports session restoration, this method is called to restore the
        ///     session data.
        ///     If no session data is found, the client will be treated as a new client.
        ///     Note that the client id is not kept between sessions, only the session custom data.
        ///     Called only on the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="requestPacket"></param>
        /// <returns></returns>
        [ServerOnly]
        protected virtual SessionData OnRestoreSessionData(int clientId,
            NetworkSessionEstablishRequestPacket requestPacket)
        {
            return GetAllDisconnectedSessionData<SessionData>().FirstOrDefault(data => data.ClientId == clientId);
        }

        /// <summary>
        ///     Called when no restored session data is found.
        ///     Called only on the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="requestPacket"></param>
        /// <returns></returns>
        [ServerOnly]
        protected virtual SessionData OnCreateNewSessionData(int clientId,
            NetworkSessionEstablishRequestPacket requestPacket)
        {
            return new SessionData();
        }

        /// <summary>
        ///     Called when the server receives a session establish request packet.
        ///     Here the server can accept or reject the client connection.
        ///     If the server rejects the connection, the client will be disconnected.
        ///     Called only on the server.
        /// </summary>
        /// <param name="requestPacket"></param>
        /// <returns></returns>
        [ServerOnly]
        protected virtual SessionEstablishingResponse OnSessionEstablishingRequest(
            NetworkSessionEstablishRequestPacket requestPacket)
        {
            return new SessionEstablishingResponse
            {
                Type = SessionEstablishingResponse.SessionEstablishingResponseType.Accept
            };
        }
        #endregion

        #region Packet Handling
        private void _SendClientPreExistingInfo(int clientId)
        {
            var prePacket = new NetworkPreExistingInfoPacket
            {
                PreExistingObjects = networkObjects.Values.Where(identity => identity.PrefabId.IsEmpty).Select(
                    identity =>
                    {
                        var t = identity.transform;
                        var o = identity.gameObject;
                        return new NetworkPreExistingInfoPacket.PreExistingState
                        {
                            Id = identity.Id,
                            PrefabId = identity.PrefabId,
                            OwnerId = identity.OwnerId,
                            Position = t.position,
                            Rotation = t.rotation,
                            Scale = t.localScale,
                            IsActive = o.activeSelf,
                            SceneId = GetSceneId(o.scene.name)
                        };
                    }).ToArray(),
                RemovedObjects = removedPreExistingObjects.ToArray(),
                SceneNames = loadedScenes.ToArray()
            };

            var spawns = new List<NetworkObjectSpawnPacket>();

            foreach (var identity in networkObjects.Values)
            {
                if (identity.PrefabId.IsEmpty) continue;

                var t = identity.transform;
                var o = identity.gameObject;
                spawns.Add(new NetworkObjectSpawnPacket
                {
                    Id = identity.Id,
                    PrefabId = identity.PrefabId,
                    OwnerId = identity.OwnerId,
                    Position = t.position,
                    Rotation = t.rotation,
                    Scale = t.localScale,
                    IsActive = o.activeSelf,
                    SceneId = GetSceneId(o.scene.name)
                });
            }

            prePacket.SpawnedObjects = spawns.ToArray();

            var values = new List<NetworkBehaviourDataPacket>();

            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                {
                    var packet = behaviour.GetBehaviourDataPacket();
                    if (packet == null)
                        continue;
                    values.Add(packet);
                }

            prePacket.NetworkValues = values.ToArray();

            ServerSendPacket(prePacket, clientId, true);
        }

        private void _HandleNetworkBehaviourDataPacket(NetworkBehaviourDataPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;
            foreach (var behaviour in identity.Behaviours)
                if (behaviour.BehaviourId == packet.BehaviourId)
                    behaviour.ApplyDirtyValues(packet.Payload);
        }

        private void _HandleActivePacket(NetworkObjectActivePacket activePacket)
        {
            if (!networkObjects.TryGetValue(activePacket.Id, out var identity)) return;

            if (identity.gameObject.activeSelf == activePacket.IsActive)
                return;

            identity.gameObject.SetActive(activePacket.IsActive);

            foreach (var behaviour in identity.Behaviours)
                behaviour.OnActiveChanged(activePacket.IsActive);

            NetworkAction.OnObjectChangeActive.Invoke(identity.Id, identity);
        }

        private async void _HandlePreExistingInfoPacket(NetworkPreExistingInfoPacket preExistingInfoPacket)
        {
            foreach (var sceneName in preExistingInfoPacket.SceneNames)
                await _LoadSceneLocally(sceneName, false);

            foreach (var preExistingObject in preExistingInfoPacket.PreExistingObjects)
            {
                if (!networkObjects.ContainsKey(preExistingObject.Id))
                    continue;

                var obj = networkObjects[preExistingObject.Id].gameObject;
                obj.transform.localScale = preExistingObject.Scale;
                var identity = obj.GetComponent<NetworkIdentity>();
                _OwnerIdField.SetValue(identity, preExistingObject.OwnerId);
                _PrefabIdField.SetValue(identity, preExistingObject.PrefabId);
                identity.gameObject.SetActive(preExistingObject.IsActive);
                var scene = GetSceneName(preExistingObject.SceneId);
                if (scene != obj.scene.name && loadedScenes.Contains(scene))
                    SceneManager.MoveGameObjectToScene(obj, SceneManager.GetSceneByName(scene));

                OnNetworkObjectSpawned(identity, true);
            }

            foreach (var removedObject in preExistingInfoPacket.RemovedObjects)
                if (networkObjects.TryGetValue(removedObject, out var identity))
                {
                    networkObjects.Remove(removedObject);
                    OnNetworkObjectDespawned(identity);
                    Destroy(identity.gameObject);
                }

            foreach (var spawnedObject in preExistingInfoPacket.SpawnedObjects)
                _HandleSpawnPacket(spawnedObject, true);

            foreach (var valuesPacket in preExistingInfoPacket.NetworkValues)
                _HandleNetworkBehaviourDataPacket(valuesPacket);

            ClientSendPacket(new NetworkPreExistingResponsePacket(), true);
        }

        private void _HandleNetworkClientIdPacket(NetworkClientIdPacket packet)
        {
            var list = new List<int>(_localClientIds) { packet.ClientId };
            _localClientIds = list.ToArray();
        }

        private void _HandleNetworkSessionDataPacket(NetworkSessionDataPacket packet)
        {
            var reader = new BinaryReader(new MemoryStream(packet.Data.Array!));

            if (_localSessionData.TryGetValue(packet.ClientId, out var data))
            {
                data.Deserialize(reader, false);
            }
            else
            {
                data = OnCreateEmptySessionData();
                _ClientIdField.SetValue(data, packet.ClientId);
                _localSessionData[packet.ClientId] = data;
                data.Deserialize(reader, false);
            }

            OnLocalSessionDataChanged(data);
        }

        private void _HandleSpawnPacket(NetworkObjectSpawnPacket packet, bool retroactive)
        {
            if (networkObjects.ContainsKey(packet.Id))
                return;

            var prefab = prefabRegistry.GetPrefab(packet.PrefabId);
            if (!prefab.TryGetComponent<NetworkIdentity>(out _))
                throw new Exception(
                    $"Prefab {prefab.name} ({packet.PrefabId}) does not have a NetworkIdentity component");

            var ownerId = packet.OwnerId;
            var sceneId = packet.SceneId;
            var active = packet.IsActive;

            var obj = OnSpawnObject(packet.Id, packet.PrefabId, prefab, packet.Position, packet.Rotation, packet.Scale,
                ref active,
                ref ownerId, ref sceneId);
            if (obj == null)
                return;

            var identity = obj.GetComponent<NetworkIdentity>();
            var scene = GetSceneName(sceneId);

            if (scene != obj.scene.name && loadedScenes.Contains(scene))
                SceneManager.MoveGameObjectToScene(obj, SceneManager.GetSceneByName(scene));

            if (identity != null)
            {
                _IDField.SetValue(identity, packet.Id);
                _OwnerIdField.SetValue(identity, ownerId);
                _PrefabIdField.SetValue(identity, packet.PrefabId);
                networkObjects.Add(identity.Id, identity);
                identity.gameObject.SetActive(active);
                OnNetworkObjectSpawned(identity, retroactive);
            }
            else
            {
                obj.SetActive(active);
            }
        }

        private void _HandleOwnerPacket(NetworkObjectOwnerPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity))
                return;

            var oldOwner = identity.OwnerId;
            _OwnerIdField.SetValue(identity, packet.OwnerId);

            foreach (var behaviour in identity.Behaviours)
                behaviour.OnOwnershipChanged(oldOwner, packet.OwnerId);

            NetworkAction.OnObjectChangeOwner.Invoke(identity.Id, identity);
        }

        private void _HandleDespawnPacket(NetworkObjectDespawnPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity))
                return;

            if (identity.PrefabId.IsEmpty)
                removedPreExistingObjects.Add(packet.Id);
            networkObjects.Remove(packet.Id);

            OnNetworkObjectDespawned(identity);

            OnDespawnObject(identity.gameObject);
        }

        private void _HandleLoadScenePacket(NetworkLoadScenePacket packet)
        {
            _ = _LoadSceneLocally(packet.SceneName, true);
        }

        private void _HandleUnloadScenePacket(NetworkUnloadScenePacket packet)
        {
            _UnloadSceneLocally(packet.SceneName);
        }

        private void _HandleObjectMoveScenePacket(NetworkObjectMoveScenePacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;
            var obj = identity.gameObject;
            var scene = GetSceneName(packet.SceneId);
            var realId = GetSceneId(scene);
            var prevId = GetSceneId(obj.scene.name);
            if (scene != obj.scene.name && loadedScenes.Contains(scene))
                SceneManager.MoveGameObjectToScene(obj, SceneManager.GetSceneByName(scene));
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnSceneChanged(prevId, realId);

            NetworkAction.OnObjectSceneChanged.Invoke(identity.Id, identity);
        }
        #endregion

        #region Object Utils
        /// <summary>
        ///     Spawns a network object for all clients.
        ///     Shall only be used internally.
        ///     Called only on the server.
        /// </summary>
        /// <param name="prefabId"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="owner"></param>
        /// <param name="scene"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        protected void SpawnNetworkObjectForClients(NetworkId prefabId, Vector3 position, Quaternion rotation,
            Vector3 scale, int owner = -1, int scene = -1)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            var packet = new NetworkObjectSpawnPacket
            {
                Id = NetworkId.New(),
                PrefabId = prefabId,
                OwnerId = owner,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                IsActive = prefabRegistry.GetPrefab(prefabId).activeSelf,
                SceneId = scene
            };

            BroadcastServerPacket(packet, true);
        }

        /// <summary>
        ///     Sets the owner of the network object for all clients.
        ///     Shall only be used internally.
        ///     Called only on the server.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="owner"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        protected void SetNetworkObjectOwnerForClients(NetworkId id, int owner)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            var packet = new NetworkObjectOwnerPacket
            {
                Id = id,
                OwnerId = owner
            };

            BroadcastServerPacket(packet, true);
        }

        /// <summary>
        ///     Sets the active state of the network object for all clients.
        ///     Shall only be used internally.
        ///     Called only on the server.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="active"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        protected void SetNetworkObjectActiveForClients(NetworkId id, bool active)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            var packet = new NetworkObjectActivePacket
            {
                Id = id,
                IsActive = active
            };

            BroadcastServerPacket(packet, true);
        }

        /// <summary>
        ///     Despawns the network object for all clients.
        ///     Shall only be used internally.
        ///     Called only on the server.
        /// </summary>
        /// <param name="id"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        protected void DespawnNetworkObjectForClients(NetworkId id)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            var packet = new NetworkObjectDespawnPacket
            {
                Id = id
            };

            BroadcastServerPacket(packet, true);
        }

        /// <summary>
        ///     Used to move a network object to another scene.
        ///     Shall only be used internally.
        ///     Called only on the server.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneId"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        protected void MoveObjectToScene(NetworkId id, int sceneId)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            var packet = new NetworkObjectMoveScenePacket
            {
                Id = id,
                SceneId = sceneId
            };
            BroadcastServerPacket(packet, true);
        }
        #endregion

        #region Send Utils
        /// <summary>
        ///     Sends a packet to the server.
        ///     Called only on the client.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        /// <exception cref="Exception"></exception>
        [ClientOnly]
        public void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            if (!IsClientRunning)
                throw new Exception("This method can only be called on the client");

            transport.ClientSendPacket(packet, reliable);
        }

        /// <summary>
        ///     Sends a packet to a client.
        ///     If the target is -1, the packet is sent to all clients.
        ///     Called only on the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="target"></param>
        /// <param name="reliable"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public void ServerSendPacket(IPacket packet, int target = -1, bool reliable = false)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            transport.ServerSendPacket(packet, target, reliable);

            if (EnvironmentType == NetworkTransport.EnvironmentType.Server)
                OnClientReceivePacket(packet);
        }

        /// <summary>
        ///     Broadcasts a packet to all clients.
        ///     Called only on the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            transport.BroadcastServerPacket(packet, reliable);

            if (EnvironmentType == NetworkTransport.EnvironmentType.Server)
                OnClientReceivePacket(packet);
        }

        /// <summary>
        ///     Broadcasts a packet to all clients except for the given client.
        ///     Called only on the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="except"></param>
        /// <param name="reliable"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public void BroadcastServerPacketExceptFor(IPacket packet, int except, bool reliable = false)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            foreach (var client in transport.GetClients())
                if (client.Id != except)
                    ServerSendPacket(packet, client.Id, reliable);

            if (EnvironmentType == NetworkTransport.EnvironmentType.Server)
                OnClientReceivePacket(packet);
        }
        #endregion

        #region Scene Management
        /// <summary>
        ///     Returns the scene id of the given scene name.
        ///     If the scene name is not found, returns -1.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public int GetSceneId(string sceneName)
        {
            return loadedScenes.IndexOf(sceneName);
        }

        /// <summary>
        ///     Returns the scene name of the given scene id.
        ///     If the scene id is -1, returns the last loaded scene.
        ///     If the scene id is 0, returns the main scene.
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string GetSceneName(int sceneId)
        {
            if (sceneId == -1)
                return LastLoadedScene;

            if (sceneId < 0 || sceneId >= loadedScenes.Count)
                throw new Exception("Invalid scene id");

            return loadedScenes[sceneId];
        }

        /// <summary>
        ///     Loads a scene through the network.
        ///     Called only on the server.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public NetworkAction<string, int> LoadScene(string sceneName)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            var packet = new NetworkLoadScenePacket
            {
                SceneName = sceneName
            };

            BroadcastServerPacket(packet, true);
            var action = new NetworkAction<string, int>(sceneName);
            NetworkAction.OnSceneLoaded.Register(sceneName, action, true);
            return action;
        }

        /// <summary>
        ///     Unloads a scene through the network.
        ///     Called only on the server.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public NetworkAction<string, int> UnloadScene(string sceneName)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            if (sceneName == mainScene)
                throw new Exception("Cannot unload the source scene");

            var packet = new NetworkUnloadScenePacket
            {
                SceneName = sceneName
            };

            BroadcastServerPacket(packet, true);

            var action = new NetworkAction<string, int>(sceneName);
            NetworkAction.OnSceneUnloaded.Register(sceneName, action, true);
            return action;
        }

        /// <summary>
        ///     Checks if the scene is loaded.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public bool IsSceneLoaded(string sceneName)
        {
            return loadedScenes.Contains(sceneName);
        }

        private async Awaitable _LoadSceneLocally(string sceneName, bool needToCall)
        {
            if (loadedScenes.Contains(sceneName))
                return;

            loadedScenes.Add(sceneName);

            var async = SceneManager.LoadSceneAsync(sceneName, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!async.isDone) await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();

            var scene = SceneManager.GetSceneByName(sceneName);

            foreach (var root in scene.GetRootGameObjects())
                foreach (var identity in root.GetComponentsInChildren<NetworkIdentity>())
                {
                    if (networkObjects.ContainsKey(identity.Id))
                        continue;

                    if (removedPreExistingObjects.Contains(identity.Id))
                        removedPreExistingObjects.Remove(identity.Id);

                    networkObjects.Add(identity.Id, identity);

                    if (needToCall)
                        OnNetworkObjectSpawned(identity, false);
                }

            var sceneId = GetSceneId(sceneName);
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSceneLoaded(sceneId);

            NetworkAction.OnSceneLoaded.Invoke(sceneName, sceneId);
        }

        private async void _UnloadSceneLocally(string sceneName)
        {
            if (!loadedScenes.Contains(sceneName))
                return;

            loadedScenes.Remove(sceneName);

            var scene = SceneManager.GetSceneByName(sceneName);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var identity in root.GetComponentsInChildren<NetworkIdentity>())
                {
                    if (!networkObjects.ContainsKey(identity.Id))
                        continue;
                    networkObjects.Remove(identity.Id);
                    OnNetworkObjectDespawned(identity);
                }

            var sceneId = GetSceneId(sceneName);

            await SceneManager.UnloadSceneAsync(scene);
            await Awaitable.NextFrameAsync();

            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSceneUnloaded(sceneId);

            NetworkAction.OnSceneUnloaded.Invoke(sceneName, sceneId);
        }
        #endregion

        #region Client Utils
        /// <summary>
        ///     Returns the id of all connected clients.
        ///     Called only on the server.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public IEnumerable<int> GetConnectedClients()
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return transport.GetClients().Select(client => client.Id);
        }

        /// <summary>
        ///     Returns the count of all connected clients.
        ///     Called only on the server.
        /// </summary>
        /// <returns></returns>
        [ServerOnly]
        public int GetConnectedClientCount()
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return transport.GetClientCount();
        }
        #endregion

        #region Session Management
        /// <summary>
        ///     Try to get the session data of the client.
        ///     Called only on the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="data"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public bool TryGetSessionData<T>(int clientId, out T data) where T : SessionData
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            if (_sessionData.TryGetValue(clientId, out var s))
                return (data = s as T) != null;

            data = null;
            return false;
        }

        /// <summary>
        ///     Returns the session data of all connected clients.
        ///     Called only on the server.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public IEnumerable<T> GetAllSessionData<T>() where T : SessionData
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return _sessionData.Values.OfType<T>();
        }

        /// <summary>
        ///     Returns the session data of all disconnected clients.
        ///     Called only on the server.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public IEnumerable<T> GetAllDisconnectedSessionData<T>() where T : SessionData
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return _disconnectedSessionData.OfType<T>();
        }

        /// <summary>
        ///     Clears all disconnected session data.
        ///     Called only on the server.
        /// </summary>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public void ClearAllDisconnectedSessionData()
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            _disconnectedSessionData.Clear();
        }

        /// <summary>
        ///     Re-syncs the session data of the client.
        ///     Called only on the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public void SendSessionDataToClient(int clientId)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            if (!_sessionData.TryGetValue(clientId, out var data))
                throw new Exception("Session data not found");

            _SessionWriter.BaseStream.SetLength(0);
            data.Serialize(_SessionWriter, false);

            var packet = new NetworkSessionDataPacket
            {
                ClientId = clientId,
                Data = new ArraySegment<byte>(_SessionStream.GetBuffer(), 0, (int)_SessionStream.Length)
            };

            ServerSendPacket(packet, clientId, true);
        }

        /// <summary>
        ///     Re-syncs the session data of the client.
        ///     Called only on the server.
        /// </summary>
        /// <param name="data"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public void SendSessionDataToClient(SessionData data)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            _SessionWriter.BaseStream.SetLength(0);
            data.Serialize(_SessionWriter, false);

            var packet = new NetworkSessionDataPacket
            {
                ClientId = data.ClientId,
                Data = new ArraySegment<byte>(_SessionStream.GetBuffer(), 0, (int)_SessionStream.Length)
            };

            ServerSendPacket(packet, data.ClientId, true);
        }

        /// <summary>
        ///     Returns the session data of the local client.
        ///     Called only on the client.
        /// </summary>
        /// <param name="clientId"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [ClientOnly]
        public T GetLocalSessionData<T>(int clientId = -1) where T : SessionData
        {
            if (!IsClientRunning)
                throw new Exception("This method can only be called on the client");

            if (clientId == -1)
                clientId = _localClientIds[0];

            if (!_localSessionData.TryGetValue(clientId, out var data))
                throw new Exception("Session data not found");

            return (T)data;
        }
        #endregion
    }
}