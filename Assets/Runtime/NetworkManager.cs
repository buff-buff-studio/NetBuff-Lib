using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private static readonly MemoryStream _SessionStream = new();
        private static readonly BinaryWriter _SessionWriter = new(_SessionStream);
        
        private int[] _localClientIds;
        private Dictionary<int, SessionData> _localSessionData;
        
        private List<SessionData> _disconnectedSessionData;
        private Dictionary<int, SessionData> _sessionData;
        
        private Queue<IPacket> _pendingPacketsClient;
        private Queue<(int, IPacket)> _pendingPacketsServer;
        
        private List<int> _notReadyClients;

        [SerializeField, HideInInspector]
        private List<string> loadedScenes = new();

        [SerializeField, HideInInspector]
        private string mainScene;
        
        [SerializeField, HideInInspector]
        private SerializedDictionary<NetworkId, NetworkIdentity> networkObjects = new();

        [SerializeField, HideInInspector]
        private List<NetworkId> removedPreExistingObjects = new();

        [SerializeField, HideInInspector]
        private List<NetworkBehaviour> dirtyBehaviours = new();

        #if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private NetworkTransport.EnvironmentType environmentTypeAfterReload = NetworkTransport.EnvironmentType.None;

        [SerializeField, HideInInspector]
        protected bool isClientReloaded;

        [Serializable]
        private class PersistentSessionData
        {
            public int clientId;
            public byte[] data;
        }

        [SerializeField, HideInInspector]
        private SerializedDictionary<int, PersistentSessionData> persistentSessionData = new();

        [SerializeField, HideInInspector]
        private List<PersistentSessionData> persistentDisconnectedSessionData = new();
#endif

        [SerializeField, HideInInspector]
        private int busyLock;
        #endregion

        #region Helper Properties
        public static NetworkManager Instance { get; private set; }

        public bool IsReady => busyLock == 0;

        public string Name
        {
            get => name;
            set => name = value;
        }

        public int VersionMagicNumber
        {
            get => versionMagicNumber;
            set => versionMagicNumber = value;
        }

        public int DefaultTickRate
        {
            get => defaultTickRate;
            set => defaultTickRate = value;
        }

        public bool SpawnsPlayer
        {
            get => spawnsPlayer;
            set => spawnsPlayer = value;
        }

        public bool SupportsSessionRestoration
        {
            get => supportsSessionRestoration;
            set => supportsSessionRestoration = value;
        }

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

        public NetworkPrefabRegistry PrefabRegistry
        {
            get => prefabRegistry;
            set => prefabRegistry = value;
        }

        public GameObject PlayerPrefab
        {
            get => playerPrefab;
            set => playerPrefab = value;
        }

        public bool IsClientRunning { get; protected set; }

        public bool IsServerRunning { get; protected set; }

        public IList<NetworkBehaviour> DirtyBehaviours => dirtyBehaviours;

        public NetworkTransport.EnvironmentType EnvironmentType => transport.Type;

        [ClientOnly]
        public IConnectionInfo ClientConnectionInfo => transport.ClientConnectionInfo;

        [ClientOnly]
        public ReadOnlySpan<int> LocalClientIds => _localClientIds;

        public string MainScene => mainScene;

        public IEnumerable<string> LoadedScenes => loadedScenes;

        public int LoadedSceneCount => loadedScenes.Count;

        public string LastLoadedScene => loadedScenes.Count == 0 ? MainScene : loadedScenes.LastOrDefault();
        #endregion

        #region Events
        public event Action<bool> OnReadyChanged;
        
        [ServerOnly]
        public event Action<int, bool> OnClientReadyChanged;
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            //Init all internal collections
            _localClientIds = Array.Empty<int>();
            _localSessionData = new Dictionary<int, SessionData>();
            
            _disconnectedSessionData = new List<SessionData>();
            _sessionData = new Dictionary<int, SessionData>();
            
            _pendingPacketsClient = new Queue<IPacket>();
            _pendingPacketsServer = new Queue<(int, IPacket)>();
            
            _notReadyClients = new List<int>();

            //Clears all the events
            NetworkEvent.ClearEvents();

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
            transport.OnClientPacketReceived += OnReceivePacket;
            transport.OnClientConnected += OnClientConnected;
            transport.OnClientDisconnected += OnClientDisconnected;
            transport.OnConnect += OnConnect;
            transport.OnDisconnect += OnDisconnect;
            transport.OnServerStart += OnServerStart;
            transport.OnServerStop += OnServerStop;

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
            if (!loadedScenes.Contains(mainScene))
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
                behaviour.SendDirtyValues();

            dirtyBehaviours.Clear();
        }
        #endregion

        #region Helper Methods
        public void StartClient()
        {
            transport.StartClient(versionMagicNumber);
        }

        public void StartServer()
        {
            transport.StartServer();
        }

        public void StartHost()
        {
            transport.StartHost(versionMagicNumber);
        }

        public void Close()
        {
            transport.Close();
        }
        #endregion

        #region Network Object Methods
        public NetworkIdentity GetNetworkObject(NetworkId id)
        {
            return networkObjects.GetValueOrDefault(id);
        }

        public IEnumerable<NetworkIdentity> GetNetworkObjects()
        {
            return networkObjects.Values;
        }

        public int GetNetworkObjectCount()
        {
            return networkObjects.Count;
        }

        public IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int owner)
        {
            return networkObjects.Values.Where(identity => identity.OwnerId == owner);
        }
        #endregion

        #region Virtual Methods
        protected virtual void OnServerError(string error)
        {
            Debug.LogError($"[NetworkManager] Server side error: {error}");
        }

        protected virtual void OnClientError(string error)
        {
            Debug.LogError($"[NetworkManager] Client side error: {error}");
        }

        protected virtual void OnServerStart()
        {
            IsServerRunning = true;

            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSpawned(false);
        }

        protected virtual void OnServerStop(NetworkTransport.ConnectionEndMode mode, string cause)
        {
            IsServerRunning = false;
            if (transport.Type is NetworkTransport.EnvironmentType.Server)
                OnClearEnvironment(mode, cause);
        }

        protected virtual void OnNetworkObjectSpawned(NetworkIdentity identity, bool retroactive)
        {
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnSpawned(retroactive);

            foreach (var behaviour in identity.Behaviours)
                behaviour.OnActiveChanged(identity.gameObject.activeInHierarchy);

            foreach (var obj in networkObjects.Values)
                foreach (var behaviour in obj.Behaviours)
                    behaviour.OnAnyObjectSpawned(identity, retroactive);
        }

        protected virtual void OnNetworkObjectDespawned(NetworkIdentity identity, bool isRetroactive)
        {
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnDespawned(isRetroactive);
        }

        [ServerOnly]
        protected virtual void OnClientConnected(int clientId)
        {
            var idPacket = new NetworkClientIdPacket { ClientId = clientId };
            transport.ServerSendPacket(idPacket, clientId, true);
        }

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

            if (_notReadyClients.Contains(clientId))
            {
                _notReadyClients.Remove(clientId);
                OnServerIsReadyChanged(clientId, false);
            }
        }

        [ServerOnly]
        protected virtual void OnSpawnPlayer(int clientId)
        {
            if (!prefabRegistry.IsPrefabValid(playerPrefab))
                throw new Exception("Player prefab is not valid");
            SpawnNetworkObjectForClients(prefabRegistry.GetPrefabId(playerPrefab), Vector3.zero, Quaternion.identity,
                Vector3.one, clientId, 0);
        }

        [ClientOnly]
        protected virtual void OnConnect()
        {
            IsClientRunning = true;

            var packet = OnCreateSessionEstablishRequest();
            ClientSendPacket(packet, true);
        }

        [ClientOnly]
        protected virtual void OnDisconnect(NetworkTransport.ConnectionEndMode mode, string reason)
        {
            IsClientRunning = false;

            #if UNITY_EDITOR
            if (environmentTypeAfterReload == NetworkTransport.EnvironmentType.None)
            #endif
            {
                foreach (var identity in networkObjects.Values)
                    foreach (var behaviour in identity.Behaviours)
                        behaviour.OnActiveChanged(false);

                OnClearEnvironment(mode, reason);
            }
        }

        [ServerOnly]
        protected virtual void OnServerReceivePacket(int clientId, IPacket packet)
        {
            switch (packet)
                {
                    case NetworkClientReadyPacket networkClientReadyPacket:
                    {
                        if (networkClientReadyPacket.IsReady)
                        {
                            if (_notReadyClients.Contains(clientId))
                            {
                                _notReadyClients.Remove(clientId);
                                OnServerIsReadyChanged(clientId, true);
                                OnClientReadyChanged?.Invoke(clientId, true);
                            }
                        }
                        else
                        {
                            if (!_notReadyClients.Contains(clientId))
                            {
                                _notReadyClients.Add(clientId);
                                OnServerIsReadyChanged(clientId, false);
                                OnClientReadyChanged?.Invoke(clientId, false);
                            }
                        }
                        return;
                    }

                    case NetworkSessionEstablishRequestPacket establishPacket:
                    {
                        if (busyLock > 0)
                        {
                            _pendingPacketsServer.Enqueue((clientId, packet));
                            return;
                        }

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
                        ServerSendPacket(_GetPreExistingInfoPacket(false), clientId, true);
                        return;
                    }

                    case NetworkPreExistingResponsePacket _:
                    {
                        if (busyLock > 0)
                        {
                            _pendingPacketsServer.Enqueue((clientId, packet));
                            return;
                        }
#if UNITY_EDITOR
                        if (!isClientReloaded)
                            if (spawnsPlayer)
                                OnSpawnPlayer(clientId);
#else
                        if (spawnsPlayer) 
                            OnSpawnPlayer(clientId);
#endif
                        foreach (var identity in networkObjects.Values)
                            foreach (var behaviour in identity.Behaviours)
                                behaviour.OnClientConnected(clientId);
                        return;
                    }

                    case NetworkBehaviourDataPacket valuesPacket:
                    {
                        if (busyLock > 0)
                        {
                            _pendingPacketsServer.Enqueue((clientId, packet));
                            return;
                        }

                        if (!networkObjects.TryGetValue(valuesPacket.Id, out _)) return;
                        BroadcastServerPacketExceptFor(valuesPacket, clientId, true);
                        return;
                    }

                    case NetworkObjectSpawnPacket spawnPacket:
                    {
                        if (busyLock > 0)
                        {
                            _pendingPacketsServer.Enqueue((clientId, packet));
                            return;
                        }

                        if (networkObjects.ContainsKey(spawnPacket.Id)) return;
                        if (!prefabRegistry.IsPrefabValid(spawnPacket.PrefabId)) return;
                        BroadcastServerPacket(spawnPacket, true);
                        return;
                    }

                    case NetworkObjectDespawnPacket destroyPacket:
                    {
                        if (busyLock > 0)
                        {
                            _pendingPacketsServer.Enqueue((clientId, packet));
                            return;
                        }

                        if (!networkObjects.TryGetValue(destroyPacket.Id, out var identity)) return;
                        if (!CheckAuthority(identity.OwnerId, clientId))
                        {
                            Debug.LogWarning(
                                $"[NetworkManager] Client {clientId} tried to destroy object {destroyPacket.Id} which it does not own");
                            return;
                        }

                        DespawnNetworkObjectForClients(destroyPacket.Id);
                        return;
                    }

                    case NetworkObjectActivePacket activePacket:
                    {
                        if (busyLock > 0)
                        {
                            _pendingPacketsServer.Enqueue((clientId, packet));
                            return;
                        }
                        
                        if (!networkObjects.TryGetValue(activePacket.Id, out var identity)) return;
                        if (!CheckAuthority(identity.OwnerId, clientId))
                        {
                            Debug.LogWarning(
                                $"[NetworkManager] Client {clientId} tried to change active state of object {activePacket.Id} which it does not own");
                            return;
                        }

                        SetNetworkObjectActiveForClients(activePacket.Id, activePacket.IsActive);
                        return;
                    }

                    case NetworkObjectOwnerPacket authorityPacket:
                    {
                        if (busyLock > 0)
                        {
                            _pendingPacketsServer.Enqueue((clientId, packet));
                            return;
                        }

                        if (!networkObjects.TryGetValue(authorityPacket.Id, out var identity)) return;
                        if (!CheckAuthority(identity.OwnerId, clientId))
                        {
                            Debug.LogWarning(
                                $"[NetworkManager] Client {clientId} tried to change owner of object {authorityPacket.Id} which it does not own");
                            return;
                        }

                        SetNetworkObjectOwnerForClients(authorityPacket.Id, authorityPacket.OwnerId);
                        return;
                    }

                    case IOwnedPacket ownedPacket:
                    {
                        if (busyLock > 0)
                        {
                            _pendingPacketsServer.Enqueue((clientId, packet));
                            return;
                        }

                        if (!networkObjects.TryGetValue(ownedPacket.Id, out var identity)) return;

                        foreach (var behaviour in identity.Behaviours)
                            behaviour.OnServerReceivePacket(ownedPacket, clientId);
                        return;
                    }
                }

            PacketListener.GetPacketListener(packet.GetType()).CallOnServerReceive(packet, clientId);
        }

        protected bool CheckAuthority(int ownerId, int clientId)
        {
            if (clientId == ownerId)
                return true;
            
            if(_localClientIds.Contains(ownerId) && _localClientIds.Contains(clientId))
                return true;
            
            return false;
        }
        
        protected virtual void OnReceivePacket(IPacket packet)
        {
            if (busyLock > 0)
            {
                _pendingPacketsClient.Enqueue(packet);
                return;
            }

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
                    _HandleNetworkBehaviourDataPacket(valuesPacket, true, false);
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
                    _ = _HandlePreExistingInfoPacket(preExistingInfoPacket);
                    return;

                case NetworkLoadScenePacket loadScenePacket:
                    _ = _HandleLoadScenePacketAsync(loadScenePacket);
                    return;

                case NetworkUnloadScenePacket unloadScenePacket:
                    _ = _HandleUnloadScenePacketAsync(unloadScenePacket);
                    return;

                case IOwnedPacket ownedPacket:
                    {
                        if (!networkObjects.TryGetValue(ownedPacket.Id, out var identity))
                            return;

                        foreach (var behaviour in identity.Behaviours)
                            behaviour.OnReceivePacket(ownedPacket);
                        return;
                    }
            }

            PacketListener.GetPacketListener(packet.GetType()).CallOnClientReceive(packet);
        }

        protected virtual void OnClearEnvironment(NetworkTransport.ConnectionEndMode mode, string cause)
        {
            SceneManager.LoadScene(MainScene);
        }

        protected virtual GameObject OnSpawnObject(NetworkId id, NetworkId prefabId, GameObject prefab,
            Vector3 position,
            Quaternion rotation, Vector3 scale, ref bool active, ref int owner, ref int sceneId)
        {
            var obj = Instantiate(prefab, position, rotation);
            obj.transform.localScale = scale;
            return obj;
        }

        protected virtual void OnDespawnObject(GameObject o)
        {
            Destroy(o);
        }

        protected virtual SessionData OnCreateEmptySessionData()
        {
            return new SessionData();
        }

        [ClientOnly]
        protected virtual NetworkSessionEstablishRequestPacket OnCreateSessionEstablishRequest()
        {
            return new NetworkSessionEstablishRequestPacket();
        }

        [ClientOnly]
        protected virtual void OnLocalSessionDataChanged(SessionData data)
        {
        }

        [ServerOnly]
        protected virtual SessionData OnRestoreSessionData(int clientId,
            NetworkSessionEstablishRequestPacket requestPacket)
        {
            return GetAllDisconnectedSessionData<SessionData>().FirstOrDefault(data => data.ClientId == clientId);
        }

        [ServerOnly]
        protected virtual SessionData OnCreateNewSessionData(int clientId,
            NetworkSessionEstablishRequestPacket requestPacket)
        {
            return new SessionData();
        }

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
        private NetworkPreExistingInfoPacket _GetPreExistingInfoPacket(bool isSnapshot)
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
                LoadedSceneNames = loadedScenes.ToArray(),
                IsSnapshot = isSnapshot
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
                    var packet = behaviour.GetBehaviourDataPacket(isSnapshot);
                    if (packet == null)
                        continue;
                    values.Add(packet);
                }

            prePacket.NetworkValues = values.ToArray();
            return prePacket;
        }

        private void _HandleNetworkBehaviourDataPacket(NetworkBehaviourDataPacket packet, bool callCallback, bool isSnapshot)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;
            foreach (var behaviour in identity.Behaviours)
                if (behaviour.BehaviourId == packet.BehaviourId)
                    behaviour.ApplyDirtyValues(packet.Payload, callCallback, isSnapshot);
        }

        private void _HandleActivePacket(NetworkObjectActivePacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;

            if (identity.gameObject.activeSelf == packet.IsActive)
                return;

            identity.gameObject.SetActive(packet.IsActive);

            foreach (var behaviour in identity.Behaviours)
                behaviour.OnActiveChanged(packet.IsActive);
            
            NetworkEvent.InvokeSafely(packet.EventId, identity);
        }

        private async Task _HandlePreExistingInfoPacket(NetworkPreExistingInfoPacket preExistingInfoPacket)
        {
            if (preExistingInfoPacket.IsSnapshot || !IsServerRunning)
            {
                _StartBusy();
            
                foreach (var loadedScene in loadedScenes.ToArray())
                    if (loadedScene != mainScene)
                        await _UnloadSceneLocally(loadedScene, NetworkId.Empty);

                foreach (var sceneName in preExistingInfoPacket.LoadedSceneNames)
                    await _LoadSceneLocally(sceneName, false, NetworkId.Empty);

                foreach (var preExistingObject in preExistingInfoPacket.PreExistingObjects)
                {
                    if (!networkObjects.ContainsKey(preExistingObject.Id))
                    {
                        Debug.LogWarning($"[NetworkManager] Received pre-existing object with id {preExistingObject.Id} which does not exist");
                        continue;
                    }

                    var obj = networkObjects[preExistingObject.Id].gameObject;
                    var t = obj.transform;
                    t.position = preExistingObject.Position;
                    t.rotation = preExistingObject.Rotation;
                    t.localScale = preExistingObject.Scale;
                    var identity = obj.GetComponent<NetworkIdentity>();
                    _OwnerIdField.SetValue(identity, preExistingObject.OwnerId);
                    _PrefabIdField.SetValue(identity, preExistingObject.PrefabId);
                    identity.gameObject.SetActive(preExistingObject.IsActive);
                    var scene = GetSceneName(preExistingObject.SceneId);
                    if (scene != obj.scene.name && loadedScenes.Contains(scene))
                        SceneManager.MoveGameObjectToScene(obj, SceneManager.GetSceneByName(scene));
                }

                foreach (var removedObject in preExistingInfoPacket.RemovedObjects)
                    if (networkObjects.Remove(removedObject, out var identity))
                    {
                        OnNetworkObjectDespawned(identity, true);
                        Destroy(identity.gameObject);
                    }

                foreach (var spawnedObject in preExistingInfoPacket.SpawnedObjects)
                    _HandleSpawnPacket(spawnedObject, true);

                foreach (var valuesPacket in preExistingInfoPacket.NetworkValues)
                    _HandleNetworkBehaviourDataPacket(valuesPacket, false, preExistingInfoPacket.IsSnapshot);
                
                //Call OnNetworkSpawned for all objects
                foreach (var preExistingObject in preExistingInfoPacket.PreExistingObjects)
                    OnNetworkObjectSpawned(networkObjects[preExistingObject.Id], true);
                
                foreach (var spawnedObject in preExistingInfoPacket.SpawnedObjects)
                    OnNetworkObjectSpawned(networkObjects[spawnedObject.Id], true);
                
                if (preExistingInfoPacket.IsSnapshot)
                {
                    await Awaitable.WaitForSecondsAsync(0.1f);
                    _pendingPacketsClient.Clear();
                    _pendingPacketsServer.Clear();
                }
                
                _EndBusy();
            }

            if (!preExistingInfoPacket.IsSnapshot && IsClientRunning)
                ClientSendPacket(new NetworkPreExistingResponsePacket(), true);
        }

        private void _HandleNetworkClientIdPacket(NetworkClientIdPacket packet)
        {
            var list = new List<int>(_localClientIds) { packet.ClientId };
            _localClientIds = list.ToArray();
        }

        private void _HandleNetworkSessionDataPacket(NetworkSessionDataPacket packet)
        {
            if (!IsClientRunning)
                return;

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

                if (retroactive) 
                    return;
                
                OnNetworkObjectSpawned(identity, false);
                NetworkEvent.InvokeSafely(packet.EventId, identity);
            }
            else
                Debug.LogWarning($"[NetworkManager] Received spawn packet for object {packet.Id} which does not have a NetworkIdentity component");
        }

        private void _HandleOwnerPacket(NetworkObjectOwnerPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity))
                return;

            var oldOwner = identity.OwnerId;
            _OwnerIdField.SetValue(identity, packet.OwnerId);

            foreach (var behaviour in identity.Behaviours)
                behaviour.OnOwnershipChanged(oldOwner, packet.OwnerId);

            NetworkEvent.InvokeSafely(packet.EventId, identity);
        }

        private void _HandleDespawnPacket(NetworkObjectDespawnPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity))
                return;

            if (identity.PrefabId.IsEmpty)
                removedPreExistingObjects.Add(packet.Id);
            networkObjects.Remove(packet.Id);

            OnNetworkObjectDespawned(identity, false);
            OnDespawnObject(identity.gameObject);

            NetworkEvent.InvokeSafely(packet.EventId, identity);
        }

        private async Task _HandleLoadScenePacketAsync(NetworkLoadScenePacket packet)
        {
            _StartBusy();
            await _LoadSceneLocally(packet.SceneName, true, packet.EventId);
            _EndBusy();
        }

        private async Task _HandleUnloadScenePacketAsync(NetworkUnloadScenePacket packet)
        {
            _StartBusy();
            await _UnloadSceneLocally(packet.SceneName, packet.EventId);
            _EndBusy();
        }

        private void _StartBusy()
        {
            busyLock++;
            if (busyLock == 1)
            {
                if (IsClientRunning)
                {
                    ClientSendPacket(new NetworkClientReadyPacket
                    {
                        ClientId = _localClientIds[0],
                        IsReady = false
                    }, true);

                    OnClientIsReadyChanged(false);
                }

                OnReadyChanged?.Invoke(false);
                Debug.LogWarning("[NetworkManager] Network environment is now busy, packets will be delayed until it is ready.");
            }
        }

        private void _EndBusy()
        {
            busyLock--;
            if (busyLock < 0)
                busyLock = 0;

            if (busyLock == 0)
            {
                if (IsClientRunning)
                {
                    ClientSendPacket(new NetworkClientReadyPacket
                    {
                        ClientId = _localClientIds[0],
                        IsReady = true
                    }, true);

                    OnClientIsReadyChanged(true);
                }

                OnReadyChanged?.Invoke(true);
                Debug.LogWarning($"[NetworkManager] Network environment is now ready, packets will be processed ({_pendingPacketsServer.Count} server, {_pendingPacketsClient.Count} client).");

                if (IsServerRunning)
                {
                    while (_pendingPacketsServer.Count > 0 && busyLock == 0)
                    {
                        var (clientId, packet) = _pendingPacketsServer.Dequeue();
                        OnServerReceivePacket(clientId, packet);
                    }
                }

                if (IsServerRunning || IsClientRunning)
                {
                    while (_pendingPacketsClient.Count > 0 && busyLock == 0)
                    {
                        var packet = _pendingPacketsClient.Dequeue();
                        OnReceivePacket(packet);
                    }
                }
            }
        }
        #endregion

        #region Object Utils
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
        #endregion

        #region Send Utils
        [ClientOnly]
        public void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            if (!IsClientRunning)
                throw new Exception("This method can only be called on the client");

            transport.ClientSendPacket(packet, reliable);
        }

        [ServerOnly]
        public void ServerSendPacket(IPacket packet, int target = -1, bool reliable = false)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            transport.ServerSendPacket(packet, target, reliable);

            if (EnvironmentType == NetworkTransport.EnvironmentType.Server)
                OnReceivePacket(packet);
        }

        [ServerOnly]
        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            transport.BroadcastServerPacket(packet, reliable);

            if (EnvironmentType == NetworkTransport.EnvironmentType.Server)
                OnReceivePacket(packet);
        }

        [ServerOnly]
        public void BroadcastServerPacketExceptFor(IPacket packet, int except, bool reliable = false)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            foreach (var client in transport.GetClients())
                if (client.Id != except)
                    ServerSendPacket(packet, client.Id, reliable);

            if (EnvironmentType == NetworkTransport.EnvironmentType.Server)
                OnReceivePacket(packet);
        }
        #endregion

        #region Scene Management
        public int GetSceneId(string sceneName)
        {
            return loadedScenes.IndexOf(sceneName);
        }

        public string GetSceneName(int sceneId)
        {
            if (sceneId == -1)
                return LastLoadedScene;

            if (sceneId < 0 || sceneId >= loadedScenes.Count)
                throw new Exception("Invalid scene id");

            return loadedScenes[sceneId];
        }

        [ServerOnly]
        public NetworkEvent<string> LoadScene(string sceneName)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            var @event = new NetworkEvent<string>();
            var eventId = NetworkEvent.Register(@event);

            var packet = new NetworkLoadScenePacket
            {
                SceneName = sceneName,
                EventId = eventId
            };
            
            BroadcastServerPacket(packet, true);
            return @event;
        }
        
        [ServerOnly]
        public NetworkEvent<string> UnloadScene(string sceneName)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            if (sceneName == mainScene)
                throw new Exception("Cannot unload the source scene");

            var @event = new NetworkEvent<string>();
            var eventId = NetworkEvent.Register(@event);

            var packet = new NetworkUnloadScenePacket
            {
                SceneName = sceneName,
                EventId = eventId
            };

            BroadcastServerPacket(packet, true);
            return @event;
        }
        
        public bool IsSceneLoaded(string sceneName)
        {
            return loadedScenes.Contains(sceneName);
        }

        private async Awaitable _LoadSceneLocally(string sceneName, bool needToCall, NetworkId eventId)
        {
            if (loadedScenes.Contains(sceneName))
                return;

            loadedScenes.Add(sceneName);
            
            try
            {
                var async = SceneManager.LoadSceneAsync(sceneName, new LoadSceneParameters(LoadSceneMode.Additive));
                // ReSharper disable once PossibleNullReferenceException
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
                        {
                            try
                            {
                                OnNetworkObjectSpawned(identity, false);
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                        }
                    }

                var sceneId = GetSceneId(sceneName);
                foreach (var identity in networkObjects.Values)
                    foreach (var behaviour in identity.Behaviours)
                    {
                        try
                        {
                            behaviour.OnSceneLoaded(sceneId);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }

                NetworkEvent.InvokeSafely(eventId, sceneName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkManager] Failed to load scene {sceneName}: {e.Message}");
                loadedScenes.Remove(sceneName);
                throw;
            }
        }

        private async Task _UnloadSceneLocally(string sceneName, NetworkId eventId)
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
                    try
                    {
                        OnNetworkObjectDespawned(identity, false);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

            var sceneId = GetSceneId(sceneName);
            await SceneManager.UnloadSceneAsync(scene);
            await Awaitable.NextFrameAsync();
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    try
                    {
                        behaviour.OnSceneUnloaded(sceneId);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

            NetworkEvent.InvokeSafely(eventId, sceneName);
        }
        #endregion

        #region Client Utils
        [ServerOnly]
        public IEnumerable<int> GetConnectedClients()
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return transport.GetClients().Select(client => client.Id);
        }

        [ServerOnly]
        public int GetConnectedClientCount()
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return transport.GetClientCount();
        }

        [ServerOnly]
        public bool IsClientReady(int clientId)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return !_notReadyClients.Contains(clientId);
        }

        [ServerOnly]
        public IEnumerable<int> GetNotReadyClients()
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return _notReadyClients;
        }

        [ServerOnly]
        public int GetNotReadyClientCount()
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return _notReadyClients.Count;
        }

        [ClientOnly]
        public virtual void OnClientIsReadyChanged(bool isReady)
        {

        }

        [ServerOnly]
        public virtual void OnServerIsReadyChanged(int clientId, bool isReady)
        {
            
        }
        #endregion

        #region Session Management
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

        [ServerOnly]
        public IEnumerable<T> GetAllSessionData<T>() where T : SessionData
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return _sessionData.Values.OfType<T>();
        }

        [ServerOnly]
        public IEnumerable<T> GetAllDisconnectedSessionData<T>() where T : SessionData
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            return _disconnectedSessionData.OfType<T>();
        }

        [ServerOnly]
        public void ClearAllDisconnectedSessionData()
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            _disconnectedSessionData.Clear();
        }

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

        #region State Snapshots
        [ServerOnly]
        public byte[] GetSnapshot()
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            var packet = _GetPreExistingInfoPacket(true);
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(versionMagicNumber);
            packet.Serialize(writer);
            return stream.ToArray();
        }

        [ServerOnly]
        public bool LoadSnapshot(byte[] snapshot)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            //_StartBusy();
            var packet = new NetworkPreExistingInfoPacket();
            var stream = new MemoryStream(snapshot);
            var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != versionMagicNumber)
            {
                Debug.LogError("[NetworkManager] Invalid snapshot version");
                return false;
            }
            packet.Deserialize(reader);
            
            //Sanitize Ids
            var validIds = GetConnectedClients().ToArray();
            foreach (var o in packet.PreExistingObjects)
                if (o.OwnerId != -1 && Array.IndexOf(validIds, o.OwnerId) == -1)
                    o.OwnerId = -1;
            
            foreach (var o in packet.SpawnedObjects)
                if(o.OwnerId != -1 &&  Array.IndexOf(validIds, o.OwnerId) == -1)
                    o.OwnerId = -1;
   
            BroadcastServerPacket(packet, true);
            return true;
        }
        
        public string SaveSnapshot()
        {
            var data = GetSnapshot();
            var fileName = $"snapshot_{DateTime.Now:dd_MM_yyyy HH_mm_ss}.dat";
            File.WriteAllBytes(fileName, data);
            Debug.Log($"[NetworkManager] Snapshot saved to snapshot.dat with {data.Length} bytes");
            return fileName;
        }
        #endregion
    }
}