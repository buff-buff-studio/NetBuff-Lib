using System;
using System.Collections.Generic;
using NetBuff.Interface;
using NetBuff.Misc;
using NetBuff.Packets;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

namespace NetBuff.Components
{
    /// <summary>
    /// Main class for networked objects. Used to identify and manage networked objects.
    /// </summary>
    public sealed class NetworkIdentity : MonoBehaviour
    {
        #region Internal Fields
        [SerializeField]
        private NetworkId id;
        
        [SerializeField]
        private int ownerId = -1;
        
        [SerializeField]
        private NetworkId prefabId = NetworkId.Empty;
        
        private NetworkBehaviour[] _behaviours;
        #endregion

        #region Helper Properties
        /// <summary>
        /// Returns the NetworkId of this object
        /// </summary>
        public NetworkId Id => id;
        
        /// <summary>
        /// Returns the id of the owner of this object (If the object is owned by the server, this will return -1)
        /// </summary>
        public int OwnerId => ownerId;
        
        /// <summary>
        /// Returns the prefab used to spawn this object (Will be empty for pre-spawned objects)
        /// </summary>
        public NetworkId PrefabId => prefabId;
        
        /// <summary>
        /// Returns if the local client has authority over this object
        /// If the object is not owned by the client, the server/host has authority over it
        /// </summary>
        public bool HasAuthority
        {
            get
            {
                var man = NetworkManager.Instance;
                if(man == null)
                    return false;

                return man.EndType switch
                {
                    NetworkTransport.EndType.Host => (ownerId == -1 && man.IsServerRunning) || (man.LocalClientIds.IndexOf(ownerId) != -1 && man.IsClientRunning),
                    NetworkTransport.EndType.Client => ownerId != -1 && man.LocalClientIds.IndexOf(ownerId) != -1,
                    NetworkTransport.EndType.Server => ownerId == -1,
                    _ => false
                };
            }
        }
        
        /// <summary>
        /// Returns if the object is owned by some client
        /// If the object is owned by the server/host, this will return false
        /// </summary>
        public bool IsOwnedByClient => ownerId != -1;
        
        /// <summary>
        /// Returns the id of the scene the object is in
        /// </summary>
        public int SceneId => GetSceneId(gameObject.scene.name);
        
        /// <summary>
        /// Returns if local environment is a server
        /// </summary>
        public bool IsServer => NetworkManager.Instance != null && NetworkManager.Instance.IsServerRunning;
        
        /// <summary>
        /// Returns the number of scenes loaded
        /// </summary>
        public int LoadedSceneCount => NetworkManager.Instance.LoadedSceneCount;

        /// <summary>
        /// Returns the name of the source scene
        /// </summary>
        /// <returns></returns>
        public string SourceScene => NetworkManager.Instance.SourceScene;

        /// <summary>
        /// Returns the name of the last loaded scene
        /// </summary>
        public string LastLoadedScene => NetworkManager.Instance.LastLoadedScene;

        
        /// <summary>
        /// Returns all NetworkBehaviours attached to this object
        /// </summary>
        public NetworkBehaviour[] Behaviours
        {
            get
            {
                if (_behaviours != null) return _behaviours;
                _behaviours = GetComponents<NetworkBehaviour>();
                Array.Sort(_behaviours, (x, y) => string.Compare(x.GetType().Name, y.GetType().Name, StringComparison.Ordinal));
                return _behaviours;
            }
        }
        #endregion

        #region Unity Callbacks
        private void OnValidate()
        {
            #if UNITY_EDITOR
            var identities = FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (identities.Where(identity => identity != this).All(identity => identity.id != id)) return;
            id = NetworkId.New();
            EditorUtility.SetDirty(this);
            #endif
        }
        #endregion

        #region Packet Methods
        /// <summary>
        /// Broadcasts a packet to all clients
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void ServerBroadcastPacket(IPacket packet, bool reliable = false) => NetworkManager.Instance.BroadcastServerPacket(packet, reliable);
        
        /// <summary>
        /// Broadcasts a packet to all clients except for the specified one
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="except"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void ServerBroadcastPacketExceptFor(IPacket packet, int except, bool reliable = false) => NetworkManager.Instance.BroadcastServerPacketExceptFor(packet, except, reliable);
        
        /// <summary>
        /// Sends a packet to a specific client
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="clientId"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void ServerSendPacket(IPacket packet, int clientId, bool reliable = false) => NetworkManager.Instance.SendServerPacket(packet, clientId, reliable);
        
        /// <summary>
        /// Sends a packet to the server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        public void ClientSendPacket(IPacket packet, bool reliable = false) => NetworkManager.Instance.SendClientPacket(packet, reliable);

        /// <summary>
        /// Sends a packet to the server / all clients depending on the object's ownership
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        public void SendPacket(IPacket packet, bool reliable = false)
        {
            if (IsOwnedByClient)
                ClientSendPacket(packet, reliable);
            else
                ServerBroadcastPacket(packet, reliable);
        }
        
        /// <summary>
        /// Returns the packet listener for the specified packet type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PacketListener<T> GetPacketListener<T>() where T : IPacket
        {
            return NetworkManager.Instance.GetPacketListener<T>();
        }
        #endregion

        #region Object Methods
        /// <summary>
        /// Tries to despawn the object across all clients (If you have authority)
        /// </summary>
        public void Despawn()
        {
            if (HasAuthority)
            {
                if(OwnerId == -1)
                    ServerBroadcastPacket(new NetworkObjectDespawnPacket{Id = Id});
                else
                    ClientSendPacket(new NetworkObjectDespawnPacket{Id = Id});
            }
        }
        
        /// <summary>
        /// Try to set the active state of the object across all clients (If you have authority)
        /// </summary>
        /// <param name="active"></param>
        public void SetActive(bool active)
        {
            if (HasAuthority)
            {
                if(OwnerId == -1)
                    ServerBroadcastPacket(new NetworkObjectActivePacket{Id = Id, IsActive = active});
                else
                    ClientSendPacket(new NetworkObjectActivePacket{Id = Id, IsActive = active});
            }
        }
        
        /// <summary>
        /// Try to set the owner of the object across all clients (If you have authority)
        /// </summary>
        /// <param name="clientId"></param>
        public void SetOwner(int clientId)
        {
            if (HasAuthority)
            {
                if(OwnerId == -1)
                    ServerBroadcastPacket(new NetworkObjectOwnerPacket{Id = Id, OwnerId = clientId});
                else
                    ClientSendPacket(new NetworkObjectOwnerPacket{Id = Id, OwnerId = clientId});
            }
        } 
        
        /// <summary>
        /// Returns a network object by its id
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        public NetworkIdentity GetNetworkObject(NetworkId objectId)
        {
            return NetworkManager.Instance.GetNetworkObject(objectId);
        }
        
        /// <summary>
        /// Returns all network objects
        /// </summary>
        /// <returns></returns>
        public IEnumerable<NetworkIdentity> GetNetworkObjects()
        {
            return NetworkManager.Instance.GetNetworkObjects();
        }
        
        /// <summary>
        /// Returns the count of network objects
        /// </summary>
        /// <returns></returns>
        public int GetNetworkObjectCount()
        {
            return NetworkManager.Instance.GetNetworkObjectCount();
        }
        
        /// <summary>
        /// Returns all network objects owned by a specific client (Use -1 to get all objects owned by the server)
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int clientId)
        {
            return NetworkManager.Instance.GetNetworkObjectsOwnedBy(clientId);
        }
        #endregion
        
        #region Client Methods
        /// <summary>
        /// Returns the local client index of the specified client id
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [ClientOnly]
        public int GetLocalClientIndex(int clientId)
        {
            return NetworkManager.Instance.LocalClientIds.IndexOf(clientId);
        }
        #endregion
        
        #region Prefabs
        /// <summary>
        /// Returns the registered prefab by its id
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public GameObject GetPrefabById(NetworkId prefab)
        {
            return NetworkManager.Instance.prefabRegistry.GetPrefab(prefab);
        }
        
        /// <summary>
        /// Returns the id of a registered prefab
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public NetworkId GetIdForPrefab(GameObject prefab)
        {
            return NetworkManager.Instance.prefabRegistry.GetPrefabId(prefab);
        }
        
        /// <summary>
        /// Returns if a given prefab id is registered
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public bool IsPrefabValid(NetworkId prefab)
        {
            return NetworkManager.Instance.prefabRegistry.IsPrefabValid(prefab);
        }
        #endregion
        
        #region Scene Moving
        /// <summary>
        /// Moves the object to a different scene
        /// </summary>
        /// <param name="sceneId"></param>
        public void MoveToScene(int sceneId)
        {
            if(!HasAuthority)
                throw new InvalidOperationException("Only the object owner can move it to a different scene");

            SendPacket(new NetworkObjectMoveScenePacket{Id = Id, SceneId = sceneId}, true);
        }

        /// <summary>
        /// Moves the object to a different scene
        /// </summary>
        /// <param name="sceneName"></param>
        public void MoveToScene(string sceneName)
        {
            if(!HasAuthority)
                throw new InvalidOperationException("Only the object owner can move it to a different scene");

            SendPacket(new NetworkObjectMoveScenePacket{Id = Id, SceneId = NetworkManager.Instance.GetSceneId(sceneName)}, true);
        }
        #endregion
        
        #region Scene Utils
        /// <summary>
        /// Returns all loaded scenes
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetLoadedScenes()
        {
            return NetworkManager.Instance.LoadedScenes;
        }

        /// <summary>
        /// Returns the id of a scene by its name
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public int GetSceneId(string sceneName)
        {
            return NetworkManager.Instance.GetSceneId(sceneName);
        }

        /// <summary>
        /// Returns the name of a scene by its id
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        public string GetSceneName(int sceneId)
        {
            return NetworkManager.Instance.GetSceneName(sceneId);
        }
        #endregion
        
        #region Spawning
        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public static NetworkId Spawn(GameObject prefab)
        {
            return Spawn(prefab, Vector3.zero, Quaternion.identity, Vector3.one, true);
        }

        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="active"></param>
        /// <returns></returns>
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool active)
        {
            return Spawn(prefab, position, rotation, Vector3.one, active);
        }

        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="owner"></param>
        /// <returns></returns>
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, int owner)
        {
            return Spawn(prefab, position, rotation, Vector3.one, true, owner);
        }
        
        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Spawn(prefab, position, rotation, Vector3.one, true);
        }

        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="active"></param>
        /// <param name="owner"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, bool active, int owner = -1, int scene = -1)
        {
            //var get it id
            var id = NetworkManager.Instance.prefabRegistry.GetPrefabId(prefab);
            if (id == NetworkId.Empty)
                throw new InvalidOperationException("The prefab is not registered");
            
            return _InternalSpawn(id, position, rotation, scale, active, owner, scene);
        }

        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefabId"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="active"></param>
        /// <param name="owner"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static NetworkId Spawn(NetworkId prefabId, Vector3 position, Quaternion rotation, Vector3 scale, bool active, int owner = -1, int scene = -1)
        {
            if (!NetworkManager.Instance.prefabRegistry.IsPrefabValid(prefabId))
                throw new InvalidOperationException("The prefab is not registered");
                
            return _InternalSpawn(prefabId, position, rotation, scale, active, owner, scene);
        }
        
        private static NetworkId _InternalSpawn(NetworkId prefabId, Vector3 position, Quaternion rotation, Vector3 scale, bool active, int owner, int scene)
        {
            var packet = new NetworkObjectSpawnPacket
            {
                Id = NetworkId.New(),
                PrefabId = prefabId,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                IsActive = active,
                IsRetroactive = false,
                OwnerId = owner,
                SceneId = scene
            };

            if (NetworkManager.Instance.IsServerRunning)
                NetworkManager.Instance.BroadcastServerPacket(packet, true);
            else
                NetworkManager.Instance.SendClientPacket(packet, true);
            
            return packet.Id;
        }
        #endregion
    }
}