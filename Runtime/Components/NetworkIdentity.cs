using System;
using System.Collections.Generic;
using NetBuff.Interface;
using NetBuff.Misc;
using NetBuff.Packets;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NetBuff.Components
{
    [Icon("Assets/Editor/Icons/NetworkIdentity.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-identity")]
    [DisallowMultipleComponent]
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
                if (man == null)
                    return false;

                return man.EnvironmentType switch
                {
                    NetworkTransport.EnvironmentType.Host => (ownerId == -1 && man.IsServerRunning) ||
                                                             (man.LocalClientIds.IndexOf(ownerId) != -1 &&
                                                              man.IsClientRunning),
                    NetworkTransport.EnvironmentType.Client => ownerId != -1 &&
                                                               man.LocalClientIds.IndexOf(ownerId) != -1,
                    NetworkTransport.EnvironmentType.Server => ownerId == -1,
                    _ => false
                };
            }
        }

        public bool IsOwnedByClient => ownerId != -1;

        public int SceneId => GetSceneId(gameObject.scene.name);

        public static bool IsServer => NetworkManager.Instance != null && NetworkManager.Instance.IsServerRunning;

        public static int LoadedSceneCount => NetworkManager.Instance.LoadedSceneCount;

        public static string MainScene => NetworkManager.Instance.MainScene;

        public static string LastLoadedScene => NetworkManager.Instance.LastLoadedScene;

        public static bool IsReady => NetworkManager.Instance.IsReady;

        public NetworkBehaviour[] Behaviours
        {
            get
            {
                if (_behaviours != null) 
                    return _behaviours;
   
                _behaviours = GetComponents<NetworkBehaviour>();
                foreach (var behaviour in _behaviours)
                    behaviour.TrackValues();
        
                Array.Sort(_behaviours,
                    (x, y) => string.Compare(x.GetType().Name, y.GetType().Name, StringComparison.Ordinal));
                return _behaviours;
            }
        }
        #endregion

        #region Unity Callbacks
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            var identities = FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var identity in identities)
            {
                if (identity.id == id && identity.gameObject != this.gameObject)
                {
                    var isThisPrefab = PrefabUtility.IsPartOfPrefabInstance(this.gameObject);
                    var isOtherPrefab = PrefabUtility.IsPartOfPrefabInstance(identity.gameObject);

                    if (isOtherPrefab)
                        if (PrefabUtility.GetCorrespondingObjectFromSource(identity.gameObject) == this.gameObject)
                            return;

                    if (isThisPrefab)
                        if (PrefabUtility.GetCorrespondingObjectFromSource(this.gameObject) == identity.gameObject)
                            return;

                    id = NetworkId.New();
                    EditorUtility.SetDirty(this);
                    break;
                }
            } 
#endif
        }

        #if NETBUFF_ADVANCED_DEBUG
        private void OnGUI()
        {
            if (!DebugUtilities.EnableAdvancedDebugging)
                return;
            
            if (!DebugUtilities.NetworkIdentityDraw)
                return;
            
            var screenPos = Camera.allCameras[0].WorldToScreenPoint(transform.position);
            var pos = new Vector2(screenPos.x, Screen.height - screenPos.y);

            foreach (var (line, color) in GetDebugLines())
            {
                var rect = new Rect(pos.x, pos.y, 400, 20);
                var oldColor = GUI.color;
                GUI.color = color;
                GUI.Label(rect, line);
                GUI.color = oldColor;
                pos.y += 20;
            }
        }

        private IEnumerable<(string, Color)> GetDebugLines()
        {
            var unscaledTime = Time.unscaledTime;
            
            if (DebugUtilities.NetworkIdentityDrawNames)
                yield return ($"{id} {gameObject.name}", Color.white);
            else
                yield return ($"{id}", Color.white);

            if (_behaviours == null || !DebugUtilities.NetworkIdentityDrawBehaviourNames)
                yield break;

            foreach(var behaviour in Behaviours)
            {
                if (unscaledTime - behaviour.DebugLastUpdateTime > 0.25f)
                {
                    if (DebugUtilities.NetworkIdentityDrawBehaviourNamesSleep)
                        yield return ($"- {behaviour.GetType().Name}", Color.white);
                }
                else
                    yield return ($"- {behaviour.GetType().Name}", Color.cyan);
            }
        }
        #endif
        #endregion

        #region Packet Methods
        [ServerOnly]
        public static void ServerBroadcastPacket(IPacket packet, bool reliable = false)
        {
            NetworkManager.Instance.BroadcastServerPacket(packet, reliable);
        }

        [ServerOnly]
        public static void ServerBroadcastPacketExceptFor(IPacket packet, int except, bool reliable = false)
        {
            NetworkManager.Instance.BroadcastServerPacketExceptFor(packet, except, reliable);
        }

        [ServerOnly]
        public static void ServerSendPacket(IPacket packet, int clientId, bool reliable = false)
        {
            NetworkManager.Instance.ServerSendPacket(packet, clientId, reliable);
        }

        [ClientOnly]
        public static void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            NetworkManager.Instance.ClientSendPacket(packet, reliable);
        }

        public void SendPacket(IPacket packet, bool reliable = false)
        {
            if (IsOwnedByClient)
                ClientSendPacket(packet, reliable);
            else
                ServerBroadcastPacket(packet, reliable);
        }
        #endregion

        #region Object Methods
        [RequiresAuthority]
        public NetworkEvent<NetworkIdentity> Despawn()
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can despawn it");

            var @event = new NetworkEvent<NetworkIdentity>();
            var eventId = NetworkEvent.Register(@event);

            if (OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectDespawnPacket { Id = Id, EventId = eventId }, true);
            else
                ClientSendPacket(new NetworkObjectDespawnPacket { Id = Id, EventId = eventId }, true);

            return @event;
        }
        
        [ServerOnly]
        public NetworkEvent<NetworkIdentity> ForceDespawn()
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the server can force despawn an object");
            
            var @event = new NetworkEvent<NetworkIdentity>();
            var eventId = NetworkEvent.Register(@event);
            
            ServerBroadcastPacket(new NetworkObjectDespawnPacket { Id = Id, EventId = eventId }, true);
            return @event;
        }

        [RequiresAuthority]
        public NetworkEvent<NetworkIdentity> SetActive(bool active)
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can set its active state");

            var @event = new NetworkEvent<NetworkIdentity>();
            var eventId = NetworkEvent.Register(@event);

            if (OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectActivePacket { Id = Id, IsActive = active, EventId = eventId }, true);
            else
                ClientSendPacket(new NetworkObjectActivePacket { Id = Id, IsActive = active, EventId = eventId }, true);

            return @event;  
        }

        [ServerOnly]
        public NetworkEvent<NetworkIdentity> ForceSetActive(bool active)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the server can force set the activeness of an object");
            
            var @event = new NetworkEvent<NetworkIdentity>();
            var eventId = NetworkEvent.Register(@event);
            
            ServerBroadcastPacket(new NetworkObjectActivePacket { Id = Id, IsActive = active, EventId = eventId }, true);
            return @event;
        }

        [RequiresAuthority]
        public NetworkEvent<NetworkIdentity> SetOwner(int clientId)
        {
            if (!HasAuthority)
                throw new InvalidOperationException("Only the object owner can change its owner");

            var @event = new NetworkEvent<NetworkIdentity>();
            var eventId = NetworkEvent.Register(@event);

            if (OwnerId == -1)
                ServerBroadcastPacket(new NetworkObjectOwnerPacket { Id = Id, OwnerId = clientId, EventId = eventId }, true);
            else
                ClientSendPacket(new NetworkObjectOwnerPacket { Id = Id, OwnerId = clientId, EventId = eventId }, true);

            return @event;  
        }

        [ServerOnly]
        public void ForceSetOwner(int clientId)
        {
            if (clientId == OwnerId)
                return;

            if (!IsServer)
                throw new InvalidOperationException("Only the server can force set the owner of an object");
       
            ownerId = clientId;
            ServerBroadcastPacket(new NetworkObjectOwnerPacket { Id = Id, OwnerId = clientId }, true);
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
        public static GameObject GetPrefabById(NetworkId prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.GetPrefab(prefab);
        }

        public static NetworkId GetIdForPrefab(GameObject prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.GetPrefabId(prefab);
        }

        public static bool IsPrefabValid(NetworkId prefab)
        {
            return NetworkManager.Instance.PrefabRegistry.IsPrefabValid(prefab);
        }
        #endregion

        #region Scene Utils
        public static IEnumerable<string> GetLoadedScenes()
        {
            return NetworkManager.Instance.LoadedScenes;
        }

        public static int GetSceneId(string sceneName)
        {
            return NetworkManager.Instance.GetSceneId(sceneName);
        }

        public static string GetSceneName(int sceneId)
        {
            return NetworkManager.Instance.GetSceneName(sceneId);
        }
        #endregion

        #region Spawning
        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab)
        {
            return Spawn(prefab, Vector3.zero, Quaternion.identity, Vector3.one, true);
        }

        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab, Vector3 position,
            Quaternion rotation, bool active)
        {
            return Spawn(prefab, position, rotation, Vector3.one, active);
        }

        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab, Vector3 position,
            Quaternion rotation, int owner)
        {
            return Spawn(prefab, position, rotation, Vector3.one, true, owner);
        }

        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab, Vector3 position,
            Quaternion rotation)
        {
            return Spawn(prefab, position, rotation, Vector3.one, true);
        }

        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab, Vector3 position,
            Quaternion rotation, Vector3 scale,
            bool active, int owner = -1, int scene = -1)
        {
            var id = NetworkManager.Instance.PrefabRegistry.GetPrefabId(prefab);
            if (id == NetworkId.Empty)
                throw new InvalidOperationException("The prefab is not registered");

            return _InternalSpawn(id, position, rotation, scale, active, owner, scene);
        }

        public static NetworkEvent<NetworkIdentity> Spawn(NetworkId prefabId, Vector3 position,
            Quaternion rotation, Vector3 scale,
            bool active, int owner = -1, int scene = -1)
        {
            if (!NetworkManager.Instance.PrefabRegistry.IsPrefabValid(prefabId))
                throw new InvalidOperationException("The prefab is not registered");

            return _InternalSpawn(prefabId, position, rotation, scale, active, owner, scene);
        }

        private static NetworkEvent<NetworkIdentity> _InternalSpawn(NetworkId prefabId, Vector3 position,
            Quaternion rotation,
            Vector3 scale, bool active, int owner, int scene)
        {
            var @event = new NetworkEvent<NetworkIdentity>();
            var eventId = NetworkEvent.Register(@event);

            var packet = new NetworkObjectSpawnPacket
            {
                Id = NetworkId.New(),
                PrefabId = prefabId,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                IsActive = active,
                OwnerId = owner,
                SceneId = scene,
                EventId = eventId
            };

            if (NetworkManager.Instance.IsServerRunning)
                NetworkManager.Instance.BroadcastServerPacket(packet, true);
            else
                NetworkManager.Instance.ClientSendPacket(packet, true);

            return @event;
        }
        #endregion
    }
}