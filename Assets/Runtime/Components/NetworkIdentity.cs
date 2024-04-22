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
    [Icon("Assets/Editor/Icons/NetworkIdentity.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-identity")]
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
        public NetworkId Id => id;
        
        public int OwnerId => ownerId;
        
        public NetworkId PrefabId => prefabId;
        
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
        
        public bool IsOwnedByClient => ownerId != -1;
        
        public int SceneId => GetSceneId(gameObject.scene.name);
        
        public bool IsServer => NetworkManager.Instance != null && NetworkManager.Instance.IsServerRunning;
        
        public int LoadedSceneCount => NetworkManager.Instance.LoadedSceneCount;

        public string SourceScene => NetworkManager.Instance.SourceScene;

        public string LastLoadedScene => NetworkManager.Instance.LastLoadedScene;
        
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
        [ServerOnly]
        public void ServerBroadcastPacket(IPacket packet, bool reliable = false) => NetworkManager.Instance.BroadcastServerPacket(packet, reliable);
        
        [ServerOnly]
        public void ServerBroadcastPacketExceptFor(IPacket packet, int except, bool reliable = false) => NetworkManager.Instance.BroadcastServerPacketExceptFor(packet, except, reliable);
        
        [ServerOnly]
        public void ServerSendPacket(IPacket packet, int clientId, bool reliable = false) => NetworkManager.Instance.SendServerPacket(packet, clientId, reliable);
        
        [ClientOnly]
        public void ClientSendPacket(IPacket packet, bool reliable = false) => NetworkManager.Instance.SendClientPacket(packet, reliable);

        public void SendPacket(IPacket packet, bool reliable = false)
        {
            if (IsOwnedByClient)
                ClientSendPacket(packet, reliable);
            else
                ServerBroadcastPacket(packet, reliable);
        }
        
        public PacketListener<T> GetPacketListener<T>() where T : IPacket
        {
            return NetworkManager.Instance.GetPacketListener<T>();
        }
        #endregion

        #region Object Methods
        [RequiresAuthority]
        public void Despawn()
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can despawn it");
                
            if(OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectDespawnPacket{Id = Id});
            else
                ClientSendPacket(new NetworkObjectDespawnPacket{Id = Id});
        }
        
        [RequiresAuthority]
        public void SetActive(bool active)
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can set its active state");
            
            if(OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectActivePacket{Id = Id, IsActive = active});
            else
                ClientSendPacket(new NetworkObjectActivePacket{Id = Id, IsActive = active});
        }
        
        [RequiresAuthority]
        public void SetOwner(int clientId)
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can change its owner");
        
            if(OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectOwnerPacket{Id = Id, OwnerId = clientId});
            else
                ClientSendPacket(new NetworkObjectOwnerPacket{Id = Id, OwnerId = clientId});
        }

        [ServerOnly]
        public void ForceSetOwner(int clientId)
        {
            if(clientId == OwnerId)
                return;
            
            if(!IsServer)
                throw new InvalidOperationException("Only the server can force set the owner of an object");
            
            ownerId = clientId;
            ServerBroadcastPacket(new NetworkObjectOwnerPacket{Id = Id, OwnerId = clientId});
        }
        
        public static NetworkIdentity GetNetworkObject(NetworkId objectId)
        {
            return NetworkManager.Instance.GetNetworkObject(objectId);
        }
        
        public static IEnumerable<NetworkIdentity> GetNetworkObjects()
        {
            return NetworkManager.Instance.GetNetworkObjects();
        }
        
        public static int GetNetworkObjectCount()
        {
            return NetworkManager.Instance.GetNetworkObjectCount();
        }
        
        public static IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int clientId)
        {
            return NetworkManager.Instance.GetNetworkObjectsOwnedBy(clientId);
        }
        #endregion
        
        #region Client Methods
        [ClientOnly]
        public int GetLocalClientIndex(int clientId)
        {
            return NetworkManager.Instance.LocalClientIds.IndexOf(clientId);
        }
        
        [ClientOnly]
        public int GetLocalClientCount()
        {
            return NetworkManager.Instance.LocalClientIds.Length;
        }
        
        [ClientOnly]
        public ReadOnlySpan<int> GetLocalClientIds()
        {
            return NetworkManager.Instance.LocalClientIds;
        }
        #endregion
        
        #region Prefabs
        public GameObject GetPrefabById(NetworkId prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.GetPrefab(prefab);
        }
        
        public NetworkId GetIdForPrefab(GameObject prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.GetPrefabId(prefab);
        }
        
        public bool IsPrefabValid(NetworkId prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.IsPrefabValid(prefab);
        }
        #endregion
        
        #region Scene Moving
        [RequiresAuthority]
        public void MoveToScene(int sceneId)
        {
            if(!HasAuthority)
                throw new InvalidOperationException("Only the object owner can move it to a different scene");

            SendPacket(new NetworkObjectMoveScenePacket{Id = Id, SceneId = sceneId}, true);
        }

        [RequiresAuthority]
        public void MoveToScene(string sceneName)
        {
            if(!HasAuthority)
                throw new InvalidOperationException("Only the object owner can move it to a different scene");

            SendPacket(new NetworkObjectMoveScenePacket{Id = Id, SceneId = NetworkManager.Instance.GetSceneId(sceneName)}, true);
        }
        #endregion
        
        #region Scene Utils
        public IEnumerable<string> GetLoadedScenes()
        {
            return NetworkManager.Instance.LoadedScenes;
        }

        public int GetSceneId(string sceneName)
        {
            return NetworkManager.Instance.GetSceneId(sceneName);
        }

        public string GetSceneName(int sceneId)
        {
            return NetworkManager.Instance.GetSceneName(sceneId);
        }
        #endregion
        
        #region Spawning
        public static NetworkId Spawn(GameObject prefab)
        {
            return Spawn(prefab, Vector3.zero, Quaternion.identity, Vector3.one, true);
        }

        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool active)
        {
            return Spawn(prefab, position, rotation, Vector3.one, active);
        }

        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, int owner)
        {
            return Spawn(prefab, position, rotation, Vector3.one, true, owner);
        }
        
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Spawn(prefab, position, rotation, Vector3.one, true);
        }

        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, bool active, int owner = -1, int scene = -1)
        {
            var id = NetworkManager.Instance.PrefabRegistry.GetPrefabId(prefab);
            if (id == NetworkId.Empty)
                throw new InvalidOperationException("The prefab is not registered");
            
            return _InternalSpawn(id, position, rotation, scale, active, owner, scene);
        }

        public static NetworkId Spawn(NetworkId prefabId, Vector3 position, Quaternion rotation, Vector3 scale, bool active, int owner = -1, int scene = -1)
        {
            if (!NetworkManager.Instance.PrefabRegistry.IsPrefabValid(prefabId))
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