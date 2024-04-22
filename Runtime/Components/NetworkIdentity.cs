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
    /// Used to keep the reference of an object throughout the network.
    /// All NetworkIdentity are registered in the NetworkManager.
    /// </summary>
    [Icon("Assets/Editor/Icons/NetworkIdentity.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-identity")]
    public sealed class NetworkIdentity : MonoBehaviour
    {
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
        /// The network id of this identity.
        /// </summary>
        public NetworkId Id => id;

        /// <summary>
        /// The owner id of this identity.
        /// If the owner id is -1, the object is owned by the server.
        /// </summary>
        public int OwnerId => ownerId;

        /// <summary>
        /// The id of the prefab used to spawn this identity object.
        /// If the prefab id is empty, the object was not spawned from a prefab at runtime.
        /// </summary>
        public NetworkId PrefabId => prefabId;

        /// <summary>
        /// Checks if the local environment has authority over this identity.
        /// </summary>
        public bool HasAuthority
        {
            get
            {
                var man = NetworkManager.Instance;
                if (man == null)
                    return false;

                return man.EnvironmentType switch
                {
                    NetworkTransport.EnvironmentType.Host => (ownerId == -1 && man.IsServerRunning) ||
                                                     (man.LocalClientIds.IndexOf(ownerId) != -1 && man.IsClientRunning),
                    NetworkTransport.EnvironmentType.Client => ownerId != -1 && man.LocalClientIds.IndexOf(ownerId) != -1,
                    NetworkTransport.EnvironmentType.Server => ownerId == -1,
                    _ => false
                };
            }
        }
        
        /// <summary>
        /// Checks if this identity is owned by any client.
        /// </summary>
        public bool IsOwnedByClient => ownerId != -1;

        /// <summary>
        /// The if of the scene this identity is in.
        /// </summary>
        public int SceneId => GetSceneId(gameObject.scene.name);

        /// <summary>
        /// Checks if the local environment is the server.
        /// </summary>
        public static bool IsServer => NetworkManager.Instance != null && NetworkManager.Instance.IsServerRunning;

        /// <summary>
        /// The number of currently loaded scenes on the network.
        /// </summary>
        public static int LoadedSceneCount => NetworkManager.Instance.LoadedSceneCount;

        /// <summary>
        /// The name of the scene where the NetworkManager is currently in.
        /// </summary>
        public static string MainScene => NetworkManager.Instance.MainScene;

        /// <summary>
        /// The name of the currently last loaded scene.
        /// </summary>
        public static string LastLoadedScene => NetworkManager.Instance.LastLoadedScene;

        /// <summary>
        /// All the network behaviours attached to this identity.
        /// </summary>
        public NetworkBehaviour[] Behaviours
        {
            get
            {
                if (_behaviours != null) return _behaviours;
                _behaviours = GetComponents<NetworkBehaviour>();
                Array.Sort(_behaviours,
                    (x, y) => string.Compare(x.GetType().Name, y.GetType().Name, StringComparison.Ordinal));
                return _behaviours;
            }
        }
        #endregion

        #region Packet Methods
        /// <summary>
        /// Broadcasts a packet to all clients.
        /// You can choose if the packet should be reliable or not.
        /// Reliable packets are guaranteed to be delivered, but they are a little slower.
        /// Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public static void ServerBroadcastPacket(IPacket packet, bool reliable = false)
        {
            NetworkManager.Instance.BroadcastServerPacket(packet, reliable);
        }
        
        /// <summary>
        /// Broadcasts a packet to all clients except for the given client.
        /// You can choose if the packet should be reliable or not.
        /// Reliable packets are guaranteed to be delivered, but they are a little slower.
        /// Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="except"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public static void ServerBroadcastPacketExceptFor(IPacket packet, int except, bool reliable = false)
        {
            NetworkManager.Instance.BroadcastServerPacketExceptFor(packet, except, reliable);
        }
        
        /// <summary>
        /// Sends a packet to the given client.
        /// You can choose if the packet should be reliable or not.
        /// Reliable packets are guaranteed to be delivered, but they are a little slower.
        /// Non-reliable packets are faster, but they are not guaranteed to be delivered. 
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="clientId"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public static void ServerSendPacket(IPacket packet, int clientId, bool reliable = false)
        {
            NetworkManager.Instance.ServerSendPacket(packet, clientId, reliable);
        }
        
        /// <summary>
        /// Sends a packet to the server.
        /// You can choose if the packet should be reliable or not.
        /// Reliable packets are guaranteed to be delivered, but they are a little slower.
        /// Non-reliable packets are faster, but they are not guaranteed to be delivered. 
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        public static void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            NetworkManager.Instance.ClientSendPacket(packet, reliable);
        }
        
        /// <summary>
        /// Sends a packet through the network, automatically choosing the correct method.
        /// You can choose if the packet should be reliable or not.
        /// Reliable packets are guaranteed to be delivered, but they are a little slower.
        /// Non-reliable packets are faster, but they are not guaranteed to be delivered. 
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
        /// Gets the packet listener for the given packet type, so you can listen to packets of that type.
        /// Does not work for IOwnedPacket types.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static PacketListener<T> GetPacketListener<T>() where T : IPacket
        {
            return NetworkManager.Instance.GetPacketListener<T>();
        }
        #endregion

        #region Object Methods
        /// <summary>
        /// Despawns the object from the network.
        /// Requires authority.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        [RequiresAuthority]
        public void Despawn()
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can despawn it");

            if (OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectDespawnPacket { Id = Id });
            else
                ClientSendPacket(new NetworkObjectDespawnPacket { Id = Id });
        }

        /// <summary>
        /// Changes the active state of the object.
        /// Requires authority.
        /// </summary>
        /// <param name="active"></param>
        [RequiresAuthority]
        public void SetActive(bool active)
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can set its active state");

            if (OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectActivePacket { Id = Id, IsActive = active });
            else
                ClientSendPacket(new NetworkObjectActivePacket { Id = Id, IsActive = active });
        }

        /// <summary>
        /// Sets the owner of the object.
        /// Requires authority.
        /// </summary>
        /// <param name="clientId"></param>
        /// <exception cref="InvalidOperationException"></exception>
        [RequiresAuthority]
        public void SetOwner(int clientId)
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can change its owner");

            if (OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectOwnerPacket { Id = Id, OwnerId = clientId });
            else
                ClientSendPacket(new NetworkObjectOwnerPacket { Id = Id, OwnerId = clientId });
        }

        /// <summary>
        /// Forces the transfer of ownership of the object.
        /// Can be called by the server only.
        /// Useful for preventing the despawn of object when the owner disconnects.
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        public void ForceSetOwner(int clientId)
        {
            if (clientId == OwnerId)
                return;

            if (!IsServer)
                throw new InvalidOperationException("Only the server can force set the owner of an object");

            ownerId = clientId;
            ServerBroadcastPacket(new NetworkObjectOwnerPacket { Id = Id, OwnerId = clientId });
        }

        /// <summary>
        /// Returns the network identity object with the given id.
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        public static NetworkIdentity GetNetworkObject(NetworkId objectId)
        {
            return NetworkManager.Instance.GetNetworkObject(objectId);
        }

        /// <summary>
        /// Returns all the network identity objects.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<NetworkIdentity> GetNetworkObjects()
        {
            return NetworkManager.Instance.GetNetworkObjects();
        }

        /// <summary>
        /// Returns the number of network identity objects.
        /// </summary>
        /// <returns></returns>
        public static int GetNetworkObjectCount()
        {
            return NetworkManager.Instance.GetNetworkObjectCount();
        }

        /// <summary>
        /// Returns all the network identity objects owned by the given client.
        /// If the client id is -1, it returns all the objects owned by the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public static IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int clientId)
        {
            return NetworkManager.Instance.GetNetworkObjectsOwnedBy(clientId);
        }
        #endregion

        #region Client Methods
        /// <summary>
        /// Returns the index of the local client with the given id.
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [ClientOnly]
        public int GetLocalClientIndex(int clientId)
        {
            return NetworkManager.Instance.LocalClientIds.IndexOf(clientId);
        }
        
        /// <summary>
        /// Returns the number of local clients.
        /// </summary>
        /// <returns></returns>
        [ClientOnly]
        public int GetLocalClientCount()
        {
            return NetworkManager.Instance.LocalClientIds.Length;
        }

        /// <summary>
        /// returns the client id of all local clients.
        /// </summary>
        /// <returns></returns>
        [ClientOnly]
        public ReadOnlySpan<int> GetLocalClientIds()
        {
            return NetworkManager.Instance.LocalClientIds;
        }
        #endregion

        #region Prefabs
        /// <summary>
        /// Returns the prefab object registered with the given id.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public static GameObject GetPrefabById(NetworkId prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.GetPrefab(prefab);
        }
        
        /// <summary>
        /// Returns the id of a registered prefab.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public static NetworkId GetIdForPrefab(GameObject prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.GetPrefabId(prefab);
        }
    
        /// <summary>
        /// Checks if the prefab is registered.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public static bool IsPrefabValid(NetworkId prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.IsPrefabValid(prefab);
        }
        #endregion

        #region Scene Moving
        /// <summary>
        /// Moves this object to a different scene.
        /// Requires authority.
        /// </summary>
        /// <param name="sceneId"></param>
        [RequiresAuthority]
        public void MoveToScene(int sceneId)
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can move it to a different scene");

            SendPacket(new NetworkObjectMoveScenePacket { Id = Id, SceneId = sceneId }, true);
        }

        /// <summary>
        /// Moves this object to a different scene.
        /// Requires authority.
        /// </summary>
        /// <param name="sceneName"></param>
        [RequiresAuthority]
        public void MoveToScene(string sceneName)
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can move it to a different scene");

            SendPacket(
                new NetworkObjectMoveScenePacket { Id = Id, SceneId = NetworkManager.Instance.GetSceneId(sceneName) },
                true);
        }
        #endregion

        #region Scene Utils
        /// <summary>
        /// Returns the name of all the scenes loaded on the network.
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetLoadedScenes()
        {
            return NetworkManager.Instance.LoadedScenes;
        }
        
        /// <summary>
        /// Returns the id of the scene with the given name.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public static int GetSceneId(string sceneName)
        {
            return NetworkManager.Instance.GetSceneId(sceneName);
        }

        /// <summary>
        /// Returns the name of the scene with the given id.
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        public static string GetSceneName(int sceneId)
        {
            return NetworkManager.Instance.GetSceneName(sceneId);
        }
        #endregion

        #region Spawning
        /// <summary>
        /// Spawns a prefab object on the network.
        /// If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public static NetworkId Spawn(GameObject prefab)
        {
            return Spawn(prefab, Vector3.zero, Quaternion.identity, Vector3.one, true);
        }

        /// <summary>
        /// Spawns a prefab object on the network.
        /// If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
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
        /// Spawns a prefab object on the network.
        /// If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
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
        /// Spawns a prefab object on the network.
        /// If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
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
        /// Spawns a prefab object on the network.
        /// If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        /// If the scene id is 0, the object will be spawned in the main scene.
        /// If the scene id is -1, the object will be spawned in the last loaded scene.
        /// If the owner id is -1, the object will be owned by the server.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="active"></param>
        /// <param name="owner"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale,
            bool active, int owner = -1, int scene = -1)
        {
            var id = NetworkManager.Instance.PrefabRegistry.GetPrefabId(prefab);
            if (id == NetworkId.Empty)
                throw new InvalidOperationException("The prefab is not registered");

            return _InternalSpawn(id, position, rotation, scale, active, owner, scene);
        }

        /// <summary>
        /// Spawns a prefab object on the network.
        /// If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        /// If the scene id is 0, the object will be spawned in the main scene.
        /// If the scene id is -1, the object will be spawned in the last loaded scene.
        /// If the owner id is -1, the object will be owned by the server.
        /// </summary>
        /// <param name="prefabId"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="active"></param>
        /// <param name="owner"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static NetworkId Spawn(NetworkId prefabId, Vector3 position, Quaternion rotation, Vector3 scale,
            bool active, int owner = -1, int scene = -1)
        {
            if (!NetworkManager.Instance.PrefabRegistry.IsPrefabValid(prefabId))
                throw new InvalidOperationException("The prefab is not registered");

            return _InternalSpawn(prefabId, position, rotation, scale, active, owner, scene);
        }

        private static NetworkId _InternalSpawn(NetworkId prefabId, Vector3 position, Quaternion rotation,
            Vector3 scale, bool active, int owner, int scene)
        {
            var packet = new NetworkObjectSpawnPacket
            {
                Id = NetworkId.New(),
                PrefabId = prefabId,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                IsActive = active,
                OwnerId = owner,
                SceneId = scene
            };

            if (NetworkManager.Instance.IsServerRunning)
                NetworkManager.Instance.BroadcastServerPacket(packet, true);
            else
                NetworkManager.Instance.ClientSendPacket(packet, true);

            return packet.Id;
        }
        #endregion
    }
}