using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NetBuff.Interface;
using NetBuff.Misc;
using NetBuff.Packets;
using UnityEngine;

namespace NetBuff.Components
{
    [RequireComponent(typeof(NetworkIdentity))]
    [Icon("Assets/Editor/Icons/NetworkBehaviour.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-behaviour")]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        private static readonly List<NetworkValue> _TempListValues = new();

        
        #region Internal Fields
        private NetworkIdentity _identity;
        private NetworkValue[] _values;
        private readonly Queue<byte> _dirtyValues = new();
        private bool _serializerDirty;
        
        #if NETBUFF_ADVANCED_DEBUG
        private float _debugLastUpdateTime = -5f;
        #endif
        #endregion

        #region Helper Properties
        public byte BehaviourId => (byte)Array.IndexOf(Identity.Behaviours, this);

        public bool IsDirty => NetworkManager.Instance.DirtyBehaviours.Contains(this);

        
        public ReadOnlySpan<NetworkValue> Values => new(_values);

        public NetworkIdentity Identity => _identity ??= GetComponent<NetworkIdentity>();

        public NetworkId Id
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.Id;
        }

        public int OwnerId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.OwnerId;
        }

        public NetworkId PrefabId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.PrefabId;
        }

        public bool HasAuthority
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.HasAuthority;
        }

        public bool IsOwnedByClient
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.IsOwnedByClient;
        }

        public int SceneId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.SceneId;
        }

        public static bool IsServer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.IsServer;
        }

        public static int LoadedSceneCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.LoadedSceneCount;
        }

        public static string MainScene
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.MainScene;
        }

        public static string LastLoadedScene
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.LastLoadedScene;
        }

        public static bool IsReady
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.IsReady;
        }
        
        #if NETBUFF_ADVANCED_DEBUG
        public float DebugLastUpdateTime => _debugLastUpdateTime;
        #endif
        #endregion

        #region Listeners
        [ServerOnly]
        public virtual void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
        }

        public virtual void OnReceivePacket(IOwnedPacket packet)
        {
        }

        public virtual void OnSpawned(bool isRetroactive)
        {
        }

        public virtual void OnSceneChanged(int fromScene, int toScene)
        {
        }

        [ServerOnly]
        public virtual void OnClientConnected(int clientId)
        {
        }

        [ServerOnly]
        public virtual void OnClientDisconnected(int clientId)
        {
        }

        public virtual void OnDespawned(bool isRetroactive)
        {
        }

        public virtual void OnActiveChanged(bool active)
        {
        }

        public virtual void OnOwnershipChanged(int oldOwner, int newOwner)
        {
        }

        public virtual void OnSceneLoaded(int sceneId)
        {
        }

        public virtual void OnSceneUnloaded(int sceneId)
        {
        }

        public virtual void OnAnyObjectSpawned(NetworkIdentity identity, bool isRetroactive)
        {
        }
        #endregion

        #region Value Methods
        public void TrackValues(params NetworkValue[] values)
        {
            foreach (var value in values)
                value.AttachedTo = this;

            _values = values;
            #if NETBUFF_ADVANCED_DEBUG
            _debugLastUpdateTime = -5f;
            #endif
        }

        public void TrackValues()
        {
            _TempListValues.Clear();
            var fields = GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType.IsAbstract)
                    continue;
                
                if (!typeof(NetworkValue).IsAssignableFrom(field.FieldType))
                    continue;
                
                if (field.GetCustomAttributes(typeof(NonSerializedAttribute), true).Length > 0)
                    continue;

                if (!field.IsPublic && field.GetCustomAttributes(typeof(SerializeField), true).Length == 0)
                    continue;
                
                var value = (NetworkValue) field.GetValue(this);
                if (value == null)
                    continue;
                
                _AddSorting(_TempListValues, value, field.Name);
            }
            
            TrackValues(_TempListValues.ToArray());
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void _AddSorting(List<NetworkValue> values, NetworkValue value, string name)
        {
            if (values.Count == 0)
            {
                values.Add(value);
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (string.Compare(values[i].GetType().Name, name, StringComparison.Ordinal) <=
                    0) continue;
                
                values.Insert(i, value);
                return;
            }
            
            values.Add(value);
        }
        
        // ReSharper disable once UnusedMember.Local
        private void _MarkValueDirty(NetworkValue value)
        {
            if (_values == null)
                TrackValues();

            var index = Array.IndexOf(_values!, value);
            if (index == -1)
                throw new InvalidOperationException("The value is not attached to this behaviour");

            _dirtyValues.Enqueue((byte)index);

            if (IsDirty)
                return;
            
            NetworkManager.Instance.DirtyBehaviours.Add(this);
        }

        public void MarkSerializerDirty()
        {
            if (IsDirty)
                return;

            if (this is not INetworkBehaviourSerializer)
                throw new InvalidOperationException("The behaviour does not implement INetworkBehaviourSerializer");
            
            _serializerDirty = true;
            NetworkManager.Instance.DirtyBehaviours.Add(this);
        }

        public void SendDirtyValues()
        {
            #if NETBUFF_ADVANCED_DEBUG
            _debugLastUpdateTime = Time.unscaledTime;
            #endif
            
            var writer = new BinaryWriter(new MemoryStream());
            writer.Write((byte)_dirtyValues.Count);
            while (_dirtyValues.Count > 0)
            {
                var index = _dirtyValues.Dequeue();
                writer.Write(index);
                _values[index].Serialize(writer);
            }

            if (_serializerDirty)
            {
                if (this is INetworkBehaviourSerializer nbs)
                    nbs.OnSerialize(writer, false, false);
                _serializerDirty = false;
            }

            var packet = new NetworkBehaviourDataPacket
            {
                Id = Id,
                BehaviourId = BehaviourId,
                Payload = ((MemoryStream)writer.BaseStream).ToArray()
            };
            
            if (NetworkManager.Instance.IsServerRunning)
            {
                if (NetworkManager.Instance.IsClientRunning)
                    ServerBroadcastPacketExceptFor(packet, NetworkManager.Instance.LocalClientIds[0], true);
                else
                    ServerBroadcastPacket(packet, true);
            }
            else
            {
                ClientSendPacket(packet, true);
            }
        }

        [ServerOnly]
        public void SendBehaviourDataToClient(int clientId)
        {
            if (!IsServer)
                throw new Exception("This method can only be called on the server");

            var packet = GetBehaviourDataPacket(false);
            if (packet != null)
                ServerSendPacket(packet, clientId, true);
        }

        [ServerOnly]
        public NetworkBehaviourDataPacket GetBehaviourDataPacket(bool isSnapshot)
        {
            if (!IsServer)
                throw new Exception("This method can only be called on the server");

            if (_values == null || _values.Length == 0)
            {
                var writer = new BinaryWriter(new MemoryStream());
                writer.Write((byte)0);

                if (this is INetworkBehaviourSerializer nbs)
                {
                    nbs.OnSerialize(writer, true, isSnapshot);

                    return new NetworkBehaviourDataPacket
                    {
                        Id = Id,
                        BehaviourId = BehaviourId,
                        Payload = ((MemoryStream)writer.BaseStream).ToArray()
                    };
                }

                return null;
            }
            else
            {
                var writer = new BinaryWriter(new MemoryStream());
                writer.Write((byte)_values.Length);

                for (var i = 0; i < _values.Length; i++)
                {
                    writer.Write((byte)i);
                    _values[i].Serialize(writer);
                }

                if (this is INetworkBehaviourSerializer nbs) 
                    nbs.OnSerialize(writer, true, isSnapshot);

                return new NetworkBehaviourDataPacket
                {
                    Id = Id,
                    BehaviourId = BehaviourId,
                    Payload = ((MemoryStream)writer.BaseStream).ToArray()
                };
            }
        }

        public void ApplyDirtyValues(byte[] payload, bool callCallback, bool isSnapshot)
        {
            #if NETBUFF_ADVANCED_DEBUG
            _debugLastUpdateTime = Time.unscaledTime;
            #endif
            
            if (_values == null)
                TrackValues();
            
            var reader = new BinaryReader(new MemoryStream(payload));
            var count = reader.ReadByte();
            for (var i = 0; i < count; i++)
            {
                var index = reader.ReadByte();
                _values![index].Deserialize(reader, callCallback);
            }

            if (reader.BaseStream.Position != reader.BaseStream.Length && this is INetworkBehaviourSerializer nbs)
                nbs.OnDeserialize(reader, isSnapshot);
        }
        #endregion

        #region Packet Methods
        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ServerBroadcastPacket(IPacket packet, bool reliable = false)
        {
            NetworkIdentity.ServerBroadcastPacket(packet, reliable);
        }

        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ServerBroadcastPacketExceptFor(IPacket packet, int except, bool reliable = false)
        {
            NetworkIdentity.ServerBroadcastPacketExceptFor(packet, except, reliable);
        }

        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ServerSendPacket(IPacket packet, int clientId, bool reliable = false)
        {
            NetworkIdentity.ServerSendPacket(packet, clientId, reliable);
        }

        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            NetworkIdentity.ClientSendPacket(packet, reliable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendPacket(IPacket packet, bool reliable = false)
        {
            Identity.SendPacket(packet, reliable);
        }
        #endregion

        #region Object Methods
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RequiresAuthority]
        public NetworkEvent<NetworkIdentity> Despawn()
        {
            return Identity.Despawn();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RequiresAuthority]
        public NetworkEvent<NetworkIdentity> SetActive(bool active)
        {
            return Identity.SetActive(active);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RequiresAuthority]
        public NetworkEvent<NetworkIdentity> SetOwner(int clientId)
        {
            return Identity.SetOwner(clientId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ServerOnly]
        public void ForceSetOwner(int clientId)
        {
            Identity.ForceSetOwner(clientId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkIdentity GetNetworkObject(NetworkId objectId)
        {
            return NetworkIdentity.GetNetworkObject(objectId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<NetworkIdentity> GetNetworkObjects()
        {
            return NetworkIdentity.GetNetworkObjects();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetNetworkObjectCount()
        {
            return NetworkIdentity.GetNetworkObjectCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int clientId)
        {
            return NetworkIdentity.GetNetworkObjectsOwnedBy(clientId);
        }
        #endregion

        #region Client Methods
        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLocalClientIndex(int clientId)
        {
            return Identity.GetLocalClientIndex(clientId);
        }

        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLocalClientCount()
        {
            return Identity.GetLocalClientCount();
        }

        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<int> GetLocalClientIds()
        {
            return Identity.GetLocalClientIds();
        }
        #endregion

        #region Prefabs
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameObject GetPrefabById(NetworkId prefab)
        {
            return NetworkIdentity.GetPrefabById(prefab);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkId GetIdForPrefab(GameObject prefab)
        {
            return NetworkIdentity.GetIdForPrefab(prefab);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPrefabValid(NetworkId prefab)
        {
            return NetworkIdentity.IsPrefabValid(prefab);
        }
        #endregion

        #region Scene Utils
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<string> GetLoadedScenes()
        {
            return NetworkIdentity.GetLoadedScenes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSceneId(string sceneName)
        {
            return NetworkIdentity.GetSceneId(sceneName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetSceneName(int sceneId)
        {
            return NetworkIdentity.GetSceneName(sceneId);
        }
        #endregion

        #region Spawning
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab)
        {
            return NetworkIdentity.Spawn(prefab);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab, Vector3 position,
            Quaternion rotation, bool active)
        {
            return NetworkIdentity.Spawn(prefab, position, rotation, active);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab, Vector3 position,
            Quaternion rotation, int owner)
        {
            return NetworkIdentity.Spawn(prefab, position, rotation, owner);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab, Vector3 position,
            Quaternion rotation)
        {
            return NetworkIdentity.Spawn(prefab, position, rotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkEvent<NetworkIdentity> Spawn(GameObject prefab, Vector3 position,
            Quaternion rotation, Vector3 scale,
            bool active, int owner = -1, int scene = -1)
        {
            return NetworkIdentity.Spawn(prefab, position, rotation, scale, active, owner, scene);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkEvent<NetworkIdentity> Spawn(NetworkId prefabId, Vector3 position,
            Quaternion rotation, Vector3 scale,
            bool active, int owner = -1, int scene = -1)
        {
            return NetworkIdentity.Spawn(prefabId, position, rotation, scale, active, owner, scene);
        }
        #endregion
    }
}