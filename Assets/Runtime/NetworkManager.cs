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

        //ClientId
        private static readonly FieldInfo _ClientIdField =
            typeof(SessionData).GetField("clientId", BindingFlags.NonPublic | BindingFlags.Instance);
        
        #region Public Fields
        [Header("SETTINGS")]
        [SerializeField]
        protected int versionMagicNumber;
        [SerializeField]
        protected int defaultTickRate = 50;
        [SerializeField]
        protected bool spawnsPlayer = true;
        [SerializeField]
        protected bool supportSessionRestoration = true;
        
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
        
        [SerializeField, HideInInspector] private List<string> loadedScenes = new();

        [SerializeField, HideInInspector] private string sourceScene;

        private readonly Dictionary<Type, PacketListener> _packetListeners = new();

        [SerializeField, HideInInspector]
        private SerializedDictionary<NetworkId, NetworkIdentity> networkObjects = new();

        [SerializeField, HideInInspector] private List<NetworkId> removedPreExistingObjects = new();

        [SerializeField, HideInInspector] private List<NetworkBehaviour> dirtyBehaviours = new();
        
#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private NetworkTransport.EndType endTypeAfterReload = NetworkTransport.EndType.None;
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
        #endregion

        #region Helper Properties
        public static NetworkManager Instance { get; private set; }
        
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
        
        public bool SupportSessionRestoration
        {
            get => supportSessionRestoration;
            set => supportSessionRestoration = value;
        }
        
        public NetworkTransport Transport
        {
            get => transport;
            set
            {
                if(EndType != NetworkTransport.EndType.None)
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

        public bool IsClientRunning { get; private set; }

        public bool IsServerRunning { get; private set; }

        public IList<NetworkBehaviour> DirtyBehaviours => dirtyBehaviours;

        public NetworkTransport.EndType EndType => transport.Type;

        [ClientOnly]
        public IConnectionInfo ClientConnectionInfo => transport.ClientConnectionInfo;

        [ClientOnly]
        public ReadOnlySpan<int> LocalClientIds => _localClientIds;

        public string SourceScene => sourceScene;

        public IEnumerable<string> LoadedScenes => loadedScenes;

        public int LoadedSceneCount => loadedScenes.Count;

        public string LastLoadedScene => loadedScenes.Count == 0 ? SourceScene : loadedScenes.LastOrDefault();

        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
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

#if UNITY_EDITOR
            switch (endTypeAfterReload)
            {
                case NetworkTransport.EndType.Host:
                    StartHost();
                    isClientReloaded = true;
                    break;
                case NetworkTransport.EndType.Server:
                    StartServer();
                    isClientReloaded = false;
                    break;
                
                case NetworkTransport.EndType.None:
                    isClientReloaded = false;
                    networkObjects.Clear();
                    foreach (var identity in FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (networkObjects.TryGetValue(identity.Id, out var i))
                            throw new Exception("Duplicate NetworkObject found: " + identity.gameObject.name + " " + i.gameObject.name);
                        networkObjects.Add(identity.Id, identity);
                    }
                    break;
            }
            
            endTypeAfterReload = NetworkTransport.EndType.None;
            
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

            sourceScene = gameObject.scene.name;
            loadedScenes.Add(sourceScene);
            
            if(SupportSessionRestoration)
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
            endTypeAfterReload = EndType switch
            {
                NetworkTransport.EndType.Host => NetworkTransport.EndType.Host,
                NetworkTransport.EndType.Server => NetworkTransport.EndType.Server,
                _ => NetworkTransport.EndType.None
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

        #region Listeners

        public PacketListener<T> GetPacketListener<T>() where T : IPacket
        {
            if (_packetListeners.TryGetValue(typeof(T), out var listener))
                return (PacketListener<T>)listener;

            listener = new PacketListener<T>();
            _packetListeners.Add(typeof(T), listener);

            return (PacketListener<T>)listener;
        }

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

        [ServerOnly]
        public void SpawnNetworkObjectForClients(NetworkId prefabId, Vector3 position, Quaternion rotation,
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
                IsRetroactive = false,
                IsActive = prefabRegistry.GetPrefab(prefabId).activeSelf,
                SceneId = scene
            };

            BroadcastServerPacket(packet, true);
        }

        [ServerOnly]
        public void SetNetworkObjectOwnerForClients(NetworkId id, int owner)
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
        public void SetNetworkObjectActiveForClients(NetworkId id, bool active)
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
        public void DespawnNetworkObjectForClients(NetworkId id)
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

        #region Virtual Methods

        protected virtual void OnServerStart()
        {
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSpawned(false);

            IsServerRunning = true;
        }

        protected virtual void OnServerStop()
        {
            IsServerRunning = false;
            if (transport.Type is NetworkTransport.EndType.Server)
                OnClearEnvironment();
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

        protected virtual void OnNetworkObjectDespawned(NetworkIdentity identity)
        {
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnDespawned();
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
            if(SupportSessionRestoration)
                _disconnectedSessionData.Add(data);
            _sessionData.Remove(clientId);
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

            var packet = OnCreateSessionEstablishPacket();
            ClientSendPacket(packet);
        }

        [ClientOnly]
        protected virtual void OnDisconnect(string reason)
        {
            IsClientRunning = false;

#if UNITY_EDITOR
            if(endTypeAfterReload == NetworkTransport.EndType.None)
#endif
            {
                foreach (var identity in networkObjects.Values)
                    foreach (var behaviour in identity.Behaviours)
                        behaviour.OnActiveChanged(false);

                OnClearEnvironment();
            }
        }

        [ServerOnly]
        protected virtual void OnServerReceivePacket(int clientId, IPacket packet)
        {
            switch (packet)
            {
                case NetworkSessionEstablishPacket establishPacket:
                {
                    var response = OnSessionEstablishingRequest(establishPacket);
                    if (response.Type == SessionEstablishingResponse.SessionEstablishingResponseType.Reject)
                    {
                        transport.ServerDisconnect(clientId, response.Reason);
                        return;
                    }
                    
                    var data = (SupportSessionRestoration ? OnRestoreSessionData(clientId, establishPacket) : null) ?? OnCreateNewSessionData(clientId, establishPacket);
                    _ClientIdField.SetValue(data, clientId);

                    if(data == null)
                        throw new Exception("Session data is null");
                    
                    _sessionData[clientId] = data;
                    _disconnectedSessionData.Remove(data);
                    
                    SendSessionDataToClient(clientId);
                    _SendClientPreExistingInfo(clientId);
                    return;
                }
                
                case NetworkPreExistingResponsePacket _:
                {
#if UNITY_EDITOR
                    if (!isClientReloaded)
                    {
                        if (spawnsPlayer)
                        {
                            OnSpawnPlayer(clientId);
                        }
                    }
#else
                    if (spawnsPlayer)
                    {
                        OnSpawnPlayer(clientId);
                    }
#endif
                    foreach (var identity in networkObjects.Values)
                        foreach (var behaviour in identity.Behaviours)
                            behaviour.OnClientConnected(clientId);
                    return;
                }

                case NetworkValuesPacket valuesPacket:
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
                        Debug.LogWarning($"Client {clientId} tried to destroy object {destroyPacket.Id} which it does not own");
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
                        Debug.LogWarning($"Client {clientId} tried to change active state of object {activePacket.Id} which it does not own");
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
                
                case NetworkValuesPacket valuesPacket:
                    _HandleNetworkValuesPacket(valuesPacket);
                    return;

                case NetworkObjectSpawnPacket spawnPacket:
                    _HandleSpawnPacket(spawnPacket);
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

        protected virtual void OnClearEnvironment()
        {
            SceneManager.LoadScene(SourceScene);
        }

        protected virtual GameObject OnSpawnObject(NetworkId id, GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, ref bool active, ref int owner, ref int sceneId)
        {
            var obj = Instantiate(prefab, position, rotation);
            obj.transform.localScale = scale;
            return obj;
        }

        protected virtual void OnDespawnObject(GameObject o)
        {
            Destroy(o);
        }
        
        public virtual SessionData OnCreateEmptySessionData()
        {
            return new SessionData();
        }

        [ClientOnly]
        public virtual NetworkSessionEstablishPacket OnCreateSessionEstablishPacket()
        {
            return new NetworkSessionEstablishPacket();
        }

        [ClientOnly]
        public virtual void OnLocalSessionDataChanged(SessionData data)
        {
        }
        
        [ServerOnly]
        protected virtual SessionData OnRestoreSessionData(int clientId, NetworkSessionEstablishPacket packet)
        {
            return GetAllDisconnectedSessionData<SessionData>().FirstOrDefault(data => data.ClientId == clientId);
        }
        
        [ServerOnly]
        protected virtual SessionData OnCreateNewSessionData(int clientId, NetworkSessionEstablishPacket packet)
        {
            return new SessionData();
        }
        
        [ServerOnly]
        public virtual SessionEstablishingResponse OnSessionEstablishingRequest(NetworkSessionEstablishPacket packet)
        {
            return new SessionEstablishingResponse
            {
                Type = SessionEstablishingResponse.SessionEstablishingResponseType.Accept
            };
        }
        #endregion

        #region Pre Existing Packet
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
                    IsRetroactive = true,
                    SceneId = GetSceneId(o.scene.name)
                });
            }

            prePacket.SpawnedObjects = spawns.ToArray();

            var values = new List<NetworkValuesPacket>();

            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                {
                    var packet = behaviour.GetPreExistingValuesPacket();
                    if (packet == null)
                        continue;
                    values.Add(packet);
                }

            prePacket.NetworkValues = values.ToArray();

            ServerSendPacket(prePacket, clientId, true);
        }
        #endregion

        #region Packet Handling
        private void _HandleNetworkValuesPacket(NetworkValuesPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;
            foreach (var behaviour in identity.Behaviours)
                if (behaviour.BehaviourId == packet.BehaviourId)
                {
                    behaviour.ApplyDirtyValues(packet.Payload);
                }
        }

        private void _HandleActivePacket(NetworkObjectActivePacket activePacket)
        {
            if (!networkObjects.TryGetValue(activePacket.Id, out var identity)) return;

            if (identity.gameObject.activeSelf == activePacket.IsActive)
                return;

            identity.gameObject.SetActive(activePacket.IsActive);

            foreach (var behaviour in identity.Behaviours)
                behaviour.OnActiveChanged(activePacket.IsActive);
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
                {
                    SceneManager.MoveGameObjectToScene(obj, SceneManager.GetSceneByName(scene));
                }

                OnNetworkObjectSpawned(identity, true);
            }

            foreach (var removedObject in preExistingInfoPacket.RemovedObjects)
            {
                if (networkObjects.TryGetValue(removedObject, out var identity))
                {
                    networkObjects.Remove(removedObject);
                    OnNetworkObjectDespawned(identity);
                    Destroy(identity.gameObject);
                }
            }

            foreach (var spawnedObject in preExistingInfoPacket.SpawnedObjects)
                _HandleSpawnPacket(spawnedObject);

            foreach (var valuesPacket in preExistingInfoPacket.NetworkValues)
                _HandleNetworkValuesPacket(valuesPacket);

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
            
            if(_localSessionData.TryGetValue(packet.ClientId, out var data))
                data.Deserialize(reader, false);
            else
            {
                data = OnCreateEmptySessionData();
                _ClientIdField.SetValue(data, packet.ClientId);
                _localSessionData[packet.ClientId] = data;
                data.Deserialize(reader, false);
            }
            
            OnLocalSessionDataChanged(data);
        }

        private void _HandleSpawnPacket(NetworkObjectSpawnPacket packet)
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
            
            var obj = OnSpawnObject(packet.Id, prefab, packet.Position, packet.Rotation, packet.Scale, ref active, ref ownerId, ref sceneId);
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
                OnNetworkObjectSpawned(identity, packet.IsRetroactive);
            }
            else
                obj.SetActive(active);
        }

        private void _HandleOwnerPacket(NetworkObjectOwnerPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity))
                return;

            var oldOwner = identity.OwnerId;
            _OwnerIdField.SetValue(identity, packet.OwnerId);

            foreach (var behaviour in identity.Behaviours)
                behaviour.OnOwnershipChanged(oldOwner, packet.OwnerId);
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

            if (EndType == NetworkTransport.EndType.Server)
                OnClientReceivePacket(packet);
        }

        [ServerOnly]
        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            transport.BroadcastServerPacket(packet, reliable);

            if (EndType == NetworkTransport.EndType.Server)
                OnClientReceivePacket(packet);
        }

        [ServerOnly]
        public void BroadcastServerPacketExceptFor(IPacket packet, int except, bool reliable = false)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            foreach (var client in transport.GetClients())
            {
                if (client.Id != except)
                    ServerSendPacket(packet, client.Id, reliable);
            }

            if (EndType == NetworkTransport.EndType.Server)
                OnClientReceivePacket(packet);
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
        public void LoadScene(string sceneName)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            var packet = new NetworkLoadScenePacket
            {
                SceneName = sceneName
            };
            BroadcastServerPacket(packet, true);
        }

        [ServerOnly]
        public void UnloadScene(string sceneName)
        {
            if (!IsServerRunning)
                throw new Exception("This method can only be called on the server");

            if (sceneName == sourceScene)
                throw new Exception("Cannot unload the source scene");

            var packet = new NetworkUnloadScenePacket
            {
                SceneName = sceneName
            };

            BroadcastServerPacket(packet, true);
        }

        public bool IsSceneLoaded(string sceneName)
        {
            return loadedScenes.Contains(sceneName);
        }

        public void MoveObjectToScene(NetworkId id, int sceneId)
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

        private async Awaitable _LoadSceneLocally(string sceneName, bool needToCall)
        {
            if (loadedScenes.Contains(sceneName))
                return;

            loadedScenes.Add(sceneName);

            var async = SceneManager.LoadSceneAsync(sceneName, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!async.isDone)
            {
                await Awaitable.NextFrameAsync();
            }
            await Awaitable.NextFrameAsync();

            var scene = SceneManager.GetSceneByName(sceneName);

            foreach (var root in scene.GetRootGameObjects())
            {
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
            }
            
            var sceneId = GetSceneId(sceneName);
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSceneLoaded(sceneId);
        }

        private async void _UnloadSceneLocally(string sceneName)
        {
            if (!loadedScenes.Contains(sceneName))
                return;

            loadedScenes.Remove(sceneName);

            var scene = SceneManager.GetSceneByName(sceneName);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var identity in root.GetComponentsInChildren<NetworkIdentity>())
                {
                    if (!networkObjects.ContainsKey(identity.Id))
                        continue;
                    networkObjects.Remove(identity.Id);
                    OnNetworkObjectDespawned(identity);
                }
            }
            
            var sceneId = GetSceneId(sceneName);

            await SceneManager.UnloadSceneAsync(scene);
            await Awaitable.NextFrameAsync();
            
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSceneUnloaded(sceneId);
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
            return transport.GetClientCount();
        }
        #endregion
        
        #region Session Management
        [ServerOnly]
        public bool TryGetSessionData<T>(int clientId, out T data) where T : SessionData
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            if(_sessionData.TryGetValue(clientId, out var s))
                return (data = s as T) != null;
            
            data = null;
            return false;
        }
        
        [ServerOnly]
        public IEnumerable<T> GetAllSessionData<T>() where T : SessionData
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            return _sessionData.Values.OfType<T>();
        }
        
        [ServerOnly]
        public IEnumerable<T> GetAllDisconnectedSessionData<T>() where T : SessionData
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            return _disconnectedSessionData.OfType<T>();
        }
        
        [ServerOnly]
        public void SendSessionDataToClient(int clientId)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            if(!_sessionData.TryGetValue(clientId, out var data))
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
            if(!IsServerRunning)
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
            if(!IsClientRunning)
                throw new Exception("This method can only be called on the client");
            
            if(clientId == -1)
                clientId = _localClientIds[0];
            
            if(!_localSessionData.TryGetValue(clientId, out var data))
                throw new Exception("Session data not found");
            
            return (T) data;
        }
        #endregion
    }
}