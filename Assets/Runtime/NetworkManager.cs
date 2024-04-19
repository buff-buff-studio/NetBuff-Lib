using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AYellowpaper.SerializedCollections;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using NetBuff.Packets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NetBuff
{
    /// <summary>
    /// Main network system class. Holds the network state and manages the network objects.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        private static readonly FieldInfo _IDField = typeof(NetworkIdentity).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _OwnerIdField = typeof(NetworkIdentity).GetField("ownerId", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _PrefabIdField = typeof(NetworkIdentity).GetField("prefabId", BindingFlags.NonPublic | BindingFlags.Instance);

        #region Public Fields
        [Header("SETTINGS")] 
        //Used to check if the communication is being done on the same system
        public int magicNumber = _GenerateMagicNumber();
        public int defaultTickRate = 50;
        public bool spawnsPlayer = true;
 
        [Header("REFERENCES")]
        public NetworkTransport transport;
        public NetworkPrefabRegistry prefabRegistry;
        public GameObject playerPrefab;
        #endregion
        
        #region Internal Fields
        private int[] _localClientIds = Array.Empty<int>();
        
        [SerializeField, HideInInspector]
        private List<string> loadedScenes = new();
        
        [SerializeField, HideInInspector]
        private string sourceScene;
        
        private readonly Dictionary<Type, PacketListener> _packetListeners = new();
        
        [SerializeField, HideInInspector]
        private SerializedDictionary<NetworkId, NetworkIdentity> networkObjects = new();
       
        [SerializeField, HideInInspector]
        private List<NetworkId> removedPreExistingObjects = new();
       
        [SerializeField, HideInInspector]
        private List<NetworkBehaviour> dirtyBehaviours = new();

        #if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private NetworkTransport.EndType endTypeAfterReload = NetworkTransport.EndType.None;
        [SerializeField, HideInInspector]  
        protected bool isClientReloaded;
        #endif
        #endregion

        #region Helper Properties
        /// <summary>
        /// Current instance of the NetworkManager
        /// </summary>
        public static NetworkManager Instance { get; private set; }

        /// <summary>
        /// Returns if the network is running (Client or Server)
        /// </summary>
        public bool IsClientRunning { get; private set; }
        
        /// <summary>
        /// Returns if the network is running (Server or Host)
        /// </summary>
        public bool IsServerRunning { get; private set; }
        
        /// <summary>
        /// Returns current dirty behaviours list
        /// </summary>
        public IList<NetworkBehaviour> DirtyBehaviours => dirtyBehaviours;

        /// <summary>
        /// Returns current networking end type
        /// </summary>
        public NetworkTransport.EndType EndType => transport.Type;
        
        /// <summary>
        /// Returns the connection info of local client connection
        /// </summary>
        [ClientOnly]
        public IConnectionInfo ClientConnectionInfo => transport.ClientConnectionInfo;
        
        /// <summary>
        /// Returns all local client ids
        /// </summary>
        [ClientOnly]
        public ReadOnlySpan<int> LocalClientIds => _localClientIds;
        
        /// <summary>
        /// Returns the current session entrypoint scene
        /// </summary>R
        public string SourceScene => sourceScene;

        /// <summary>
        /// Returns all loaded scenes
        /// </summary>
        public IEnumerable<string> LoadedScenes => loadedScenes;

        /// <summary>
        /// Returns the count of loaded scenes
        /// </summary>
        public int LoadedSceneCount => loadedScenes.Count;

        /// <summary>
        /// Returns the last loaded scene
        /// </summary>
        public string LastLoadedScene => loadedScenes.Count == 0 ? SourceScene : loadedScenes.LastOrDefault();
        #endregion
        
        #region Unity Callbacks
        private void OnEnable()
        {
            Instance = this;
            PacketRegistry.Clear();
            
            var types = (from assembly in AppDomain.CurrentDomain.GetAssemblies() from type in assembly.GetTypes() where type.IsClass && !type.IsAbstract && typeof(IPacket).IsAssignableFrom(type) select type).ToList();
            types.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

            //Register all packets
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
            #else
            networkObjects.Clear();
            foreach (var identity in FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (networkObjects.TryGetValue(identity.Id, out var i))
                    throw new Exception("Duplicate NetworkObject found: " + identity.gameObject.name + " " + i.gameObject.name);
                networkObjects.Add(identity.Id, identity);
            }
            #endif

            sourceScene = gameObject.scene.name;
            loadedScenes.Add(sourceScene);
        }
        
        private void OnDisable()
        {
            if(transport == null)
                return;
            
            #if UNITY_EDITOR
            endTypeAfterReload = EndType switch
            {
                NetworkTransport.EndType.Host => NetworkTransport.EndType.Host,
                NetworkTransport.EndType.Server => NetworkTransport.EndType.Server,
                _ => NetworkTransport.EndType.None
            };
            #endif
            transport.Close();
        }
        
        private void Update()
        {
            //Update dirty behaviours
            foreach (var behaviour in dirtyBehaviours)
                behaviour.UpdateDirtyValues();

            dirtyBehaviours.Clear();
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Starts the network client
        /// </summary>
        public void StartClient()
        {
            transport.StartClient(magicNumber);
        }
        
        /// <summary>
        /// Starts the network server
        /// </summary>
        public void StartServer()
        {
            transport.StartServer();
        }
        
        /// <summary>
        /// Starts the network host (Server and Client)
        /// </summary>
        public void StartHost()
        {
            transport.StartHost(magicNumber);
        }
        
        /// <summary>
        /// Close the running client or/and server
        /// </summary>
        public void Close()
        {
            transport.Close();
        }
        #endregion

        #region Listeners
        /// <summary>
        /// Returns the packet listener for the specified packet type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PacketListener<T> GetPacketListener<T>() where T : IPacket
        {
            if (_packetListeners.TryGetValue(typeof(T), out var listener)) 
                return (PacketListener<T>)listener;
            
            listener = new PacketListener<T>();
            _packetListeners.Add(typeof(T), listener);

            return (PacketListener<T>) listener;
        }
        
        /// <summary>
        /// Returns the packet listener for the specified packet type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public PacketListener GetPacketListener(Type type)
        {
            if (_packetListeners.TryGetValue(type, out var listener)) 
                return listener;
            
            listener = (PacketListener) Activator.CreateInstance(typeof(PacketListener<>).MakeGenericType(type));
            _packetListeners.Add(type, listener);

            return listener;
        }
        #endregion
        
        #region Network Object Methods
        /// <summary>
        /// Get a network object by its id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public NetworkIdentity GetNetworkObject(NetworkId id)
        {
            return networkObjects.GetValueOrDefault(id);
        }
        
        /// <summary>
        /// Get all network objects
        /// </summary>
        /// <returns></returns>
        public IEnumerable<NetworkIdentity> GetNetworkObjects()
        {
            return networkObjects.Values;
        }
        
        /// <summary>
        /// Get the count of network objects
        /// </summary>
        /// <returns></returns>
        public int GetNetworkObjectCount()
        {
            return networkObjects.Count;
        }
        
        /// <summary>
        /// Get all objects owned by a specific client (Use -1 to get unowned objects)
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        public IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int owner)
        {
            return networkObjects.Values.Where(identity => identity.OwnerId == owner);
        }
        
        /// <summary>
        /// Spawn a network object for all clients
        /// </summary>
        /// <param name="prefabId"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="owner"></param>
        /// <param name="scene"></param>
        [ServerOnly]
        public void SpawnNetworkObjectForClients(NetworkId prefabId, Vector3 position, Quaternion rotation, Vector3 scale,int owner = -1, int scene = -1)
        {
            if(!IsServerRunning)
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
        
        /// <summary>
        /// Set the owner of a network object for all clients
        /// </summary>
        /// <param name="id"></param>
        /// <param name="owner"></param>
        [ServerOnly]
        public void SetNetworkObjectOwnerForClients(NetworkId id, int owner)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            var packet = new NetworkObjectOwnerPacket
            {
                Id = id,
                OwnerId = owner
            };
            
            BroadcastServerPacket(packet, true);
        }
        
        /// <summary>
        /// Set the active state of a network object for all clients
        /// </summary>
        /// <param name="id"></param>
        /// <param name="active"></param>
        [ServerOnly]
        public void SetNetworkObjectActiveForClients(NetworkId id, bool active)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            var packet = new NetworkObjectActivePacket
            {
                Id = id,
                IsActive = active
            };
            
            BroadcastServerPacket(packet, true);
        }
        
        /// <summary>
        /// Despawn a network object for all clients
        /// </summary>
        /// <param name="id"></param>
        [ServerOnly]
        public void DespawnNetworkObjectForClients(NetworkId id)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            var packet = new NetworkObjectDespawnPacket
            {
                Id = id
            };
            
            BroadcastServerPacket(packet, true);
        }
        #endregion
        
        #region Virtual Methods
        /// <summary>
        /// Called when the server starts
        /// </summary>
        protected virtual void OnServerStart()
        {
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSpawned(false);
            
            IsServerRunning = true;
        }

        /// <summary>
        /// Called when the server stops
        /// </summary>
        protected virtual void OnServerStop()
        {
            IsServerRunning = false;
            if(transport.Type is NetworkTransport.EndType.Server)
                OnClearEnvironment();
        }
        
        
        /// <summary>
        /// Called when a network object is spawned
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="retroactive"></param>

        protected virtual void OnNetworkObjectSpawned(NetworkIdentity identity, bool retroactive)
        {
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnSpawned(retroactive);
            
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnActiveChanged(identity.gameObject.activeInHierarchy);
        }
        
        /// <summary>
        /// Called when a network object is despawned
        /// </summary>
        /// <param name="identity"></param>
        protected virtual void OnNetworkObjectDespawned(NetworkIdentity identity)
        {
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnDespawned();
        }

        /// <summary>
        /// Called when a client connects to the server
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        protected virtual void OnClientConnected(int clientId)
        {
            //Send client id
            var idPacket = new ClientIdPacket {ClientId = clientId};
            transport.SendServerPacket(idPacket, clientId, true);
        
            var prePacket = new NetworkPreExistingInfoPacket
            {
                PreExistingObjects = networkObjects.Values.Where(identity => identity.PrefabId.IsEmpty).Select(identity =>
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
                if(identity.PrefabId.IsEmpty) continue;
                
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
            
            //Network variables
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                {
                    var packet = behaviour.GetPreExistingValuesPacket();
                    if(packet == null)
                        continue;
                    values.Add(packet);
                }

            prePacket.NetworkValues = values.ToArray();

            SendServerPacket(prePacket, clientId, true);
        }

        /// <summary>
        /// Spawns a player in the game world.
        /// </summary>
        /// <param name="clientId">The ID of the client for whom the player is being spawned.</param>
        protected virtual void OnSpawnPlayer(int clientId)
        {
            if (!prefabRegistry.IsPrefabValid(playerPrefab))
                throw new Exception("Player prefab is not valid");
            SpawnNetworkObjectForClients(prefabRegistry.GetPrefabId(playerPrefab), Vector3.zero, Quaternion.identity, Vector3.one, clientId, 0);
        }

        /// <summary>
        /// Called when a client disconnects from the server
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="reason"></param>
        [ServerOnly]
        protected virtual void OnClientDisconnected(int clientId, string reason)
        {
            //Destroy all objects owned by client
            var toDestroy = GetNetworkObjectsOwnedBy(clientId).ToList();

            foreach (var id in toDestroy)
                DespawnNetworkObjectForClients(id.Id);
            
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnClientDisconnected(clientId);
        }

        /// <summary>
        /// Called when the client connects to the server
        /// </summary>
        [ClientOnly]
        protected virtual void OnConnect()
        {
            IsClientRunning = true;
        }
        
        /// <summary>
        /// Called when the client disconnects from the server
        /// </summary>
        /// <param name="reason"></param>
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
        
        /// <summary>
        /// Called when the server receives a packet
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="packet"></param>
        [ServerOnly]
        protected virtual void OnServerReceivePacket(int clientId, IPacket packet)
        {
            switch (packet)
            {
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
                    if (identity.OwnerId != clientId) return;
                    
                    //Apply the despawn
                    DespawnNetworkObjectForClients(destroyPacket.Id);
                    return;
                }

                case NetworkObjectActivePacket activePacket:
                {
                    if (!networkObjects.TryGetValue(activePacket.Id, out var identity)) return;
                    if (identity.OwnerId != clientId) return;
                    
                    //Apply the active state
                    SetNetworkObjectActiveForClients(activePacket.Id, activePacket.IsActive);
                    return;
                }
                
                case NetworkObjectOwnerPacket authorityPacket:
                {
                    if (!networkObjects.TryGetValue(authorityPacket.Id, out var identity)) return;
                    if (identity.OwnerId != clientId) return;
                    
                    //Apply the authority
                    SetNetworkObjectOwnerForClients(authorityPacket.Id, authorityPacket.OwnerId);
                    return;
                }
                
                case NetworkObjectMoveScenePacket moveObjectScenePacket:
                {
                    if (!networkObjects.TryGetValue(moveObjectScenePacket.Id, out var identity)) return;
                    if (identity.OwnerId != clientId) return;
                    
                    //Apply the scene move
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
        /// Called when the client (or a server local render) receives a packet
        /// </summary>
        /// <param name="packet"></param>
        protected virtual void OnClientReceivePacket(IPacket packet)
        {
            switch (packet)
            {
                case ClientIdPacket clientPacket:
                    _HandleClientIdPacket(clientPacket);
                    return;
            
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
        
        /// <summary>
        /// Called to reset the entire network environment
        /// </summary>
        protected virtual void OnClearEnvironment()
        {
            SceneManager.LoadScene(SourceScene);
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
            
            if(identity.gameObject.activeSelf == activePacket.IsActive)
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
            
            SendClientPacket(new NetworkPreExistingResponsePacket(), true);
        }
        
        private void _HandleClientIdPacket(ClientIdPacket packet)
        {
            var list = new List<int>(_localClientIds) { packet.ClientId };
            _localClientIds = list.ToArray();    
        }
        
        private void _HandleSpawnPacket(NetworkObjectSpawnPacket packet)
        {
            if (networkObjects.ContainsKey(packet.Id))
                return;
            
            var prefab = prefabRegistry.GetPrefab(packet.PrefabId);
            if (!prefab.TryGetComponent<NetworkIdentity>(out _))
                throw new Exception($"Prefab {prefab.name} ({packet.PrefabId}) does not have a NetworkIdentity component");
            
            var obj = Instantiate(prefab, packet.Position, packet.Rotation);
            obj.transform.localScale = packet.Scale;
            var identity = obj.GetComponent<NetworkIdentity>();
            var scene = GetSceneName(packet.SceneId);
            if (scene != obj.scene.name && loadedScenes.Contains(scene))
                SceneManager.MoveGameObjectToScene(obj, SceneManager.GetSceneByName(scene));

            if (identity != null)
            {
                _IDField.SetValue(identity, packet.Id);
                _OwnerIdField.SetValue(identity, packet.OwnerId);
                _PrefabIdField.SetValue(identity, packet.PrefabId);
                networkObjects.Add(identity.Id, identity);
                identity.gameObject.SetActive(packet.IsActive);
                OnNetworkObjectSpawned(identity, packet.IsRetroactive);
            }
            else
                obj.SetActive(packet.IsActive);
        }
        
        private void _HandleOwnerPacket(NetworkObjectOwnerPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;
            _OwnerIdField.SetValue(identity, packet.OwnerId);
            
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnOwnerChanged(packet.OwnerId);
        }
        
        private void _HandleDespawnPacket(NetworkObjectDespawnPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;
            if(identity.PrefabId.IsEmpty)
                removedPreExistingObjects.Add(packet.Id);
            networkObjects.Remove(packet.Id);
            OnNetworkObjectDespawned(identity);
            Destroy(identity.gameObject);
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
        /// <summary>
        /// Send a packet to the server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        public void SendClientPacket(IPacket packet, bool reliable = false)
        {
            if(!IsClientRunning)
                throw new Exception("This method can only be called on the client");
            
            transport.SendClientPacket(packet, reliable);
        }
        
        /// <summary>
        /// Send a packet to a specific client (Use -1 to broadcast to all clients)
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="target"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void SendServerPacket(IPacket packet, int target = -1, bool reliable = false)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            transport.SendServerPacket(packet, target, reliable);
            
            if(EndType == NetworkTransport.EndType.Server)
                OnClientReceivePacket(packet);
        }
    
        /// <summary>
        /// Send a packet to all clients
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            transport.BroadcastServerPacket(packet, reliable);
            
            if(EndType == NetworkTransport.EndType.Server)
                OnClientReceivePacket(packet);
        }
            
        /// <summary>
        /// Send a packet to all clients except for a specific one
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="except"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void BroadcastServerPacketExceptFor(IPacket packet, int except, bool reliable = false)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            foreach (var client in transport.GetClients())
            {
                if (client.Id != except)
                    SendServerPacket(packet, client.Id, reliable);
            }
            
            if(EndType == NetworkTransport.EndType.Server)
                OnClientReceivePacket(packet);
        }
        #endregion
        #region Scene Management
        /// <summary>
        /// Returns the network id of a scene
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public int GetSceneId(string sceneName)
        {
            return loadedScenes.IndexOf(sceneName);
        }

        /// <summary>
        /// Returns the name of a scene
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        public string GetSceneName(int sceneId)
        {
            if(sceneId == -1)
                return LastLoadedScene;
            
            if(sceneId < 0 || sceneId >= loadedScenes.Count)
                throw new Exception("Invalid scene id");
            
            return loadedScenes[sceneId];
        }
        
        /// <summary>
        /// Load a scene for all clients
        /// </summary>
        /// <param name="sceneName"></param>
        [ServerOnly]
        public void LoadScene(string sceneName)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            var packet = new NetworkLoadScenePacket
            {
                SceneName = sceneName
            };
            BroadcastServerPacket(packet, true);
        }
        
        /// <summary>
        /// Unload a scene for all clients
        /// </summary>
        /// <param name="sceneName"></param>
        [ServerOnly]
        public void UnloadScene(string sceneName)
        {
            if(!IsServerRunning)
                throw new Exception("This method can only be called on the server");
            
            if(sceneName == sourceScene)
                throw new Exception("Cannot unload the source scene");

            var packet = new NetworkUnloadScenePacket
            {
                SceneName = sceneName
            };

            BroadcastServerPacket(packet, true);
        }
        
        /// <summary>
        /// Returns if a scene is loaded
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public bool IsSceneLoaded(string sceneName)
        {
            return loadedScenes.Contains(sceneName);
        }

        /// <summary>
        /// Moves an object to a specific scene
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sceneId"></param>
        public void MoveObjectToScene(NetworkId id, int sceneId)
        {
            if(!IsServerRunning)
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
        
            //Load the scene itself
            var async = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
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

                    if(removedPreExistingObjects.Contains(identity.Id))
                        removedPreExistingObjects.Remove(identity.Id);
                    
                    networkObjects.Add(identity.Id, identity);

                    if(needToCall)
                        OnNetworkObjectSpawned(identity, false);
                }
            }
        }
        
        private void _UnloadSceneLocally(string sceneName)
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
            SceneManager.UnloadSceneAsync(scene);
        }
        #endregion

        private static int _GenerateMagicNumber()
        {
            var rnd = new System.Random();
            return rnd.Next(1_000_000, 9_999_999);
        }
    }
}
