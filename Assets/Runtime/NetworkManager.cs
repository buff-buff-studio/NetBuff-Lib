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
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        [Header("SETTINGS")]
        public int defaultTickRate = 50;
        public bool spawnsPlayer = true;
        
        [Header("REFERENCES")]
        public NetworkTransport transport;
        public NetworkPrefabRegistry prefabRegistry;
        public GameObject playerPrefab;
        
        #if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private NetworkTransport.EndType endTypeAfterReload = NetworkTransport.EndType.None;
        [SerializeField, HideInInspector]  
        private bool isClientReloaded;
        #endif

        #region Helper Properties
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
        private int[] _localClientIds = Array.Empty<int>();
        #endregion
        
        private readonly Dictionary<Type, PacketListener> _packetListeners = new();

        [SerializeField, HideInInspector]
        private List<string> loadedScenes = new List<string>();

        [SerializeField, HideInInspector]
        private SerializedDictionary<NetworkId, NetworkIdentity> networkObjects = new SerializedDictionary<NetworkId, NetworkIdentity>();
        
        [SerializeField, HideInInspector]
        private List<NetworkId> removedPreExistingObjects = new List<NetworkId>();

        [SerializeField, HideInInspector]
        public List<NetworkBehaviour> dirtyBehaviours = new List<NetworkBehaviour>();
        
        private void OnEnable()
        {
            Instance = this;
            PacketRegistry.Clear();
            
            var types = (from assembly in AppDomain.CurrentDomain.GetAssemblies() from type in assembly.GetTypes() where type.IsClass && !type.IsAbstract && typeof(IPacket).IsAssignableFrom(type) select type).ToList();
            types.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

            //Register all packets
            foreach (var type in types)
                PacketRegistry.RegisterPacket(type);
            
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
        }

        private void OnDisable()
        {
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

        #region Helper Methods
        public void StartClient()
        {
            transport.StartClient();
        }
        
        public void StartServer()
        {
            transport.StartServer();
        }
        
        public void StartHost()
        {
            transport.StartHost();
        }
        
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
            if (!_packetListeners.TryGetValue(typeof(T), out var listener))
            {
                listener = new PacketListener<T>();
                _packetListeners.Add(typeof(T), listener);
            }
            
            return (PacketListener<T>) listener;
        }
        
        /// <summary>
        /// Returns the packet listener for the specified packet type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public PacketListener GetPacketListener(Type type)
        {
            if (!_packetListeners.TryGetValue(type, out var listener))
            {
                listener = (PacketListener) Activator.CreateInstance(typeof(PacketListener<>).MakeGenericType(type));
                _packetListeners.Add(type, listener);
            }
            
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
        [ServerOnly]
        public void SpawnNetworkObjectForClients(NetworkId prefabId, Vector3 position, Quaternion rotation, Vector3 scale,int owner = -1)
        {
            var packet = new NetworkObjectSpawnPacket
            {
                Id = NetworkId.New(),
                PrefabId = prefabId,
                OwnerId = owner,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                IsRetroactive = false,
                IsActive = prefabRegistry.GetPrefab(prefabId).activeSelf
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
        public virtual void OnServerStart()
        {
            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnSpawned(false);
            
            IsServerRunning = true;
        }

        /// <summary>
        /// Called when the server stops
        /// </summary>
        public virtual void OnServerStop()
        {
            IsServerRunning = false;
            if(transport.Type is NetworkTransport.EndType.Server)
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        
        
        /// <summary>
        /// Called when a network object is spawned
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="retroactive"></param>

        public virtual void OnNetworkObjectSpawned(NetworkIdentity identity, bool retroactive)
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
        public virtual void OnNetworkObjectDespawned(NetworkIdentity identity)
        {
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnDespawned();
        }

        /// <summary>
        /// Called when a client connects to the server
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        public virtual void OnClientConnected(int clientId)
        {
            //Send client id
            var idPacket = new ClientIdPacket {ClientId = clientId};
            transport.SendServerPacket(idPacket, clientId, true);
            
            //load scenes
            foreach (var scene in loadedScenes)
                SendServerPacket(new NetworkLoadScenePacket {SceneName = scene}, clientId, true);

            var prePacket = new NetworkGetPreExistingInfoPacket
            {
                PreExistingObjects = networkObjects.Values.Where(identity => identity.PrefabId.IsEmpty).Select(identity =>
                {
                    var t = identity.transform;
                    return new NetworkGetPreExistingInfoPacket.PreExistingState
                    {
                        Id = identity.Id,
                        PrefabId = identity.PrefabId,
                        OwnerId = identity.OwnerId,
                        Position = t.position,
                        Rotation = t.rotation,
                        Scale = t.localScale,
                        IsActive = identity.gameObject.activeSelf
                    };
                }).ToArray(),
                RemovedObjects = removedPreExistingObjects.ToArray()
            };

            SendServerPacket(prePacket, clientId, true);
            
            foreach (var identity in networkObjects.Values)
            {
                if(identity.PrefabId.IsEmpty) continue;
                
                var t = identity.transform;
                var packet = new NetworkObjectSpawnPacket
                {
                    Id = identity.Id,
                    PrefabId = identity.PrefabId,
                    OwnerId = identity.OwnerId,
                    Position = t.position,
                    Rotation = t.rotation,
                    Scale = t.localScale,
                    IsActive = identity.gameObject.activeSelf,
                    IsRetroactive = true
                };
                SendServerPacket(packet, clientId, true);
            }

            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.SendNetworkValuesToClient(clientId);
            
            SpawnPlayer(clientId);

            foreach (var identity in networkObjects.Values)
                foreach (var behaviour in identity.Behaviours)
                    behaviour.OnClientConnected(clientId);
        }

        /// <summary>
        /// Spawns a player in the game world.
        /// </summary>
        /// <param name="clientId">The ID of the client for whom the player is being spawned.</param>
        protected virtual void SpawnPlayer(int clientId)
        {
            #if UNITY_EDITOR
            if (!isClientReloaded)
            {
                if (spawnsPlayer)
                {
                    Assert.IsTrue(prefabRegistry.IsPrefabValid(playerPrefab), "Player prefab is not valid");
                    SpawnNetworkObjectForClients(prefabRegistry.GetPrefabId(playerPrefab), Vector3.zero, Quaternion.identity, Vector3.one, clientId);
                }
            }
            #else
            if (spawnsPlayer)
            {
                Assert.IsTrue(prefabRegistry.IsPrefabValid(playerPrefab), "Player prefab is not valid");
                SpawnNetworkObjectForClients(prefabRegistry.GetPrefabId(playerPrefab), Vector3.zero, Quaternion.identity, Vector3.one, clientId);
            }
        #endif
        }

        /// <summary>
        /// Called when a client disconnects from the server
        /// </summary>
        /// <param name="clientId"></param>
        
        [ServerOnly]
        public virtual void OnClientDisconnected(int clientId)
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
        public virtual void OnConnect()
        {
            IsClientRunning = true;
        }
        
        
        /// <summary>
        /// Called when the client disconnects from the server
        /// </summary>
        [ClientOnly]
        public virtual void OnDisconnect()
        {
            IsClientRunning = false;
            
            #if UNITY_EDITOR
            if(endTypeAfterReload == NetworkTransport.EndType.None)
            #endif
            {
                foreach (var identity in networkObjects.Values)
                    foreach (var behaviour in identity.Behaviours)
                        behaviour.OnActiveChanged(false);
                
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
        
        /// <summary>
        /// Called when the server receives a packet
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="packet"></param>
        [ServerOnly]
        public virtual void OnServerReceivePacket(int clientId, IPacket packet)
        {
            switch (packet)
            {
                case NetworkValuesPacket valuesPacket:
                {
                    if (!networkObjects.TryGetValue(valuesPacket.IdentityId, out _)) return;
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
        public virtual void OnClientReceivePacket(IPacket packet)
        {
            switch (packet)
            {
                case ClientIdPacket clientPacket:
                    var list = new List<int>(_localClientIds);
                    list.Add(clientPacket.ClientId);
                    _localClientIds = list.ToArray();
                    return;
                
                case NetworkLoadScenePacket loadScenePacket:
                    if (loadedScenes.Contains(loadScenePacket.SceneName))
                        return;
                    loadedScenes.Add(loadScenePacket.SceneName);
                    SceneManager.LoadScene(loadScenePacket.SceneName, LoadSceneMode.Additive);
                    return;
                
                case NetworkUnloadScenePacket unloadScenePacket:
                    if (!loadedScenes.Contains(unloadScenePacket.SceneName))
                        return;
                    loadedScenes.Remove(unloadScenePacket.SceneName);
                    SceneManager.UnloadSceneAsync(unloadScenePacket.SceneName);
                    return;

                case NetworkValuesPacket valuesPacket:
                {
                    if (!networkObjects.TryGetValue(valuesPacket.IdentityId, out var identity)) return;
                    foreach (var behaviour in identity.Behaviours)
                        if (behaviour.BehaviourId == valuesPacket.BehaviourId)
                        {
                            behaviour.ApplyDirtyValues(valuesPacket.Payload);
                        }
                    return;
                }
                
                case NetworkObjectSpawnPacket spawnPacket:
                {
                    HandleSpawnPacket(spawnPacket);
                    return;
                }
                
                case NetworkObjectDespawnPacket destroyPacket:
                {
                    HandleDespawnPacket(destroyPacket);
                    return;
                }
                
                case NetworkObjectActivePacket activePacket:
                {
                    HandleActivePacket(activePacket);
                    return;
                }
                
                case NetworkObjectOwnerPacket authorityPacket:
                {
                    HandleOwnerPacket(authorityPacket);
                    return;
                }
                
                case NetworkGetPreExistingInfoPacket preExistingInfoPacket:
                    HandlePreExistingInfoPacket(preExistingInfoPacket);
                    return;

                case IOwnedPacket ownedPacket:
                {
                    if (!networkObjects.TryGetValue(ownedPacket.Id, out var identity)) return;
                    
                    foreach (var behaviour in identity.Behaviours)
                        behaviour.OnClientReceivePacket(ownedPacket);
                    return;
                }
            }
            
            GetPacketListener(packet.GetType()).CallOnClientReceive(packet);
        }

        private void HandleActivePacket(NetworkObjectActivePacket activePacket)
        {
            if (!networkObjects.TryGetValue(activePacket.Id, out var identity)) return;
            
            if(identity.gameObject.activeSelf == activePacket.IsActive)
                return;
            
            identity.gameObject.SetActive(activePacket.IsActive);
            
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnActiveChanged(activePacket.IsActive);
        }

        private void HandlePreExistingInfoPacket(NetworkGetPreExistingInfoPacket preExistingInfoPacket)
        {
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
        }
        #endregion
        
        private void HandleSpawnPacket(NetworkObjectSpawnPacket packet)
        {
            if (networkObjects.ContainsKey(packet.Id))
                return;
            var prefab = prefabRegistry.GetPrefab(packet.PrefabId);
            var obj = Instantiate(prefab, packet.Position, packet.Rotation);
            obj.transform.localScale = packet.Scale;
            var identity = obj.GetComponent<NetworkIdentity>();
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
        
        private void HandleOwnerPacket(NetworkObjectOwnerPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;
            _OwnerIdField.SetValue(identity, packet.OwnerId);
            
            foreach (var behaviour in identity.Behaviours)
                behaviour.OnOwnerChanged(packet.OwnerId);
        }
        
        private void HandleDespawnPacket(NetworkObjectDespawnPacket packet)
        {
            if (!networkObjects.TryGetValue(packet.Id, out var identity)) return;
            if(identity.PrefabId.IsEmpty)
                removedPreExistingObjects.Add(packet.Id);
            networkObjects.Remove(packet.Id);
            OnNetworkObjectDespawned(identity);
            Destroy(identity.gameObject);
        }

        #region Send Utils
        /// <summary>
        /// Send a packet to the server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        public void SendClientPacket(IPacket packet, bool reliable = false)
        {
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
        /// Loads a scene for all the players. Useful for multi-level games
        /// </summary>
        /// <param name="sceneName"></param>
        [ServerOnly]
        public void LoadScene(string sceneName)
        {
            if (loadedScenes.Contains(sceneName))
                return;

            if (!IsServerRunning)
                return;
                
            loadedScenes.Add(sceneName);
            SendServerPacket(new NetworkLoadScenePacket()
            {
                SceneName = sceneName
            }, reliable: true);
        }
        
        /// <summary>
        /// Unloads a scene for all the players. Useful for multi-level games
        /// </summary>
        /// <param name="sceneName"></param>
        [ServerOnly]
        public void UnloadScene(string sceneName)
        {
            if (!loadedScenes.Contains(sceneName))
                return;

            if (!IsServerRunning)
                return;
            
            loadedScenes.Remove(sceneName);
            SendServerPacket(new NetworkUnloadScenePacket()
            {
                SceneName = sceneName
            }, reliable: true);
        }
        #endregion

        private void Update()
        {
            //Update dirty behaviours
            foreach (var behaviour in dirtyBehaviours)
                behaviour.UpdateDirtyValues();

            dirtyBehaviours.Clear();
        }
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(NetworkManager))]
    public class NetworkManagerEditor : Editor
    {
        private static readonly FieldInfo _IDField = typeof(NetworkIdentity).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!GUILayout.Button("Regenerate Ids")) return;
            foreach (var identity in FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                _IDField.SetValue(identity, NetworkId.New());
                EditorUtility.SetDirty(identity);
            }
        }
    }
    #endif
}
