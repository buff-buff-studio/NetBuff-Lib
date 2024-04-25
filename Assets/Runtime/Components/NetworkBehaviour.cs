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
    /// <summary>
    ///     Base class for all network behaviours.
    ///     NetworkBehaviours cannot be added / removed at runtime.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [Icon("Assets/Editor/Icons/NetworkBehaviour.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-behaviour")]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        #region Internal Fields
        private NetworkIdentity _identity;
        private NetworkValue[] _values;
        private readonly Queue<byte> _dirtyValues = new();
        private bool _serializerDirty;
        #endregion

        #region Helper Properties
        /// <summary>
        ///     The behaviour id of this behaviour (Relative to the identity).
        /// </summary>
        public byte BehaviourId => (byte)Array.IndexOf(Identity.Behaviours, this);

        /// <summary>
        ///     Whether this behaviour has any dirty values that needs to be synchronized.
        /// </summary>
        public bool IsDirty => NetworkManager.Instance.DirtyBehaviours.Contains(this);

        /// <summary>
        ///     The values attached to this behaviour.
        /// </summary>
        public ReadOnlySpan<NetworkValue> Values => new(_values);

        /// <summary>
        ///     The network identity which this behaviour is attached to.
        /// </summary>
        public NetworkIdentity Identity => _identity ??= GetComponent<NetworkIdentity>();

        /// <summary>
        ///     The network id of this behaviour identity.
        /// </summary>
        public NetworkId Id
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.Id;
        }

        /// <summary>
        ///     The owner id of this behaviour identity.
        ///     If the owner id is -1, the object is owned by the server.
        /// </summary>
        public int OwnerId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.OwnerId;
        }

        /// <summary>
        ///     The id of the prefab used to spawn this behaviour identity object.
        ///     If the prefab id is empty, the object was not spawned from a prefab at runtime.
        /// </summary>
        public NetworkId PrefabId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.PrefabId;
        }

        /// <summary>
        ///     Checks if the local environment has authority over this behaviour.
        /// </summary>
        public bool HasAuthority
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.HasAuthority;
        }

        /// <summary>
        ///     Checks if this behaviour identity is owned by any client.
        /// </summary>
        public bool IsOwnedByClient
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.IsOwnedByClient;
        }

        /// <summary>
        ///     The if of the scene this behaviour identity is in.
        /// </summary>
        public int SceneId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Identity.SceneId;
        }

        /// <summary>
        ///     Checks if the local environment is the server.
        /// </summary>
        public static bool IsServer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.IsServer;
        }

        /// <summary>
        ///     The number of currently loaded scenes on the network.
        /// </summary>
        public static int LoadedSceneCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.LoadedSceneCount;
        }

        /// <summary>
        ///     The name of the scene where the NetworkManager is currently in.
        /// </summary>
        public static string MainScene
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.MainScene;
        }

        /// <summary>
        ///     The name of the currently last loaded scene.
        /// </summary>
        public static string LastLoadedScene
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => NetworkIdentity.LastLoadedScene;
        }
        #endregion

        #region Listeners
        /// <summary>
        ///     Called when the server receives a packet owned by this object from a client
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="clientId"></param>
        [ServerOnly]
        public virtual void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
        }

        /// <summary>
        ///     Called when the clients receive a packet owned by this object from the server.
        /// </summary>
        /// <param name="packet"></param>
        [ClientOnly]
        public virtual void OnClientReceivePacket(IOwnedPacket packet)
        {
        }

        /// <summary>
        ///     Called when this object is spawned on the network.
        ///     Called when the network environment initializes if the object already exists.
        ///     The isRetroactive parameter is true if the client is joining the server after the object is already spawned.
        /// </summary>
        /// <param name="isRetroactive"></param>
        public virtual void OnSpawned(bool isRetroactive)
        {
        }

        /// <summary>
        ///     Called when this object is moved to another scene.
        /// </summary>
        /// <param name="fromScene"></param>
        /// <param name="toScene"></param>
        public virtual void OnSceneChanged(int fromScene, int toScene)
        {
        }

        /// <summary>
        ///     Called when a new client connects to the server.
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        public virtual void OnClientConnected(int clientId)
        {
        }

        /// <summary>
        ///     Called when a client disconnects from the server.
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        public virtual void OnClientDisconnected(int clientId)
        {
        }

        /// <summary>
        ///     Called when this object is despawned from the network.
        /// </summary>
        public virtual void OnDespawned()
        {
        }

        /// <summary>
        ///     Called when the active state of this object changes.
        ///     Called when the object is spawned as well.
        /// </summary>
        /// <param name="active"></param>
        public virtual void OnActiveChanged(bool active)
        {
        }

        /// <summary>
        ///     Called when the owner of this object changes.
        /// </summary>
        /// <param name="oldOwner"></param>
        /// <param name="newOwner"></param>
        public virtual void OnOwnershipChanged(int oldOwner, int newOwner)
        {
        }

        /// <summary>
        ///     Called when a new scene is loaded.
        /// </summary>
        /// <param name="sceneId"></param>
        public virtual void OnSceneLoaded(int sceneId)
        {
        }

        /// <summary>
        ///     Called when a loaded scene is unloaded.
        /// </summary>
        /// <param name="sceneId"></param>
        public virtual void OnSceneUnloaded(int sceneId)
        {
        }

        /// <summary>
        ///     Called when a new object is spawned on the network.
        ///     The isRetroactive parameter is true if the client is joining the server after the object is already spawned.
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="isRetroactive"></param>
        public virtual void OnAnyObjectSpawned(NetworkIdentity identity, bool isRetroactive)
        {
        }
        #endregion

        #region Value Methods
        /// <summary>
        ///     Attaches the given values to this behaviour.
        ///     Shall not be called after the behaviour is spawned.
        /// </summary>
        /// <param name="values"></param>
        public void WithValues(params NetworkValue[] values)
        {
            foreach (var value in values)
                value.AttachedTo = this;

            _values = values;
        }

        private void _MarkValueDirty(NetworkValue value)
        {
            if (_values == null)
                return;

            var index = Array.IndexOf(_values, value);

            if (index == -1)
                throw new InvalidOperationException("The value is not attached to this behaviour");

            _dirtyValues.Enqueue((byte)index);

            if (IsDirty)
                return;

            NetworkManager.Instance.DirtyBehaviours.Add(this);
        }

        /// <summary>
        ///     Marks the serializer of this behaviour as dirty, so it will be synchronized through the network.
        ///     Only call this method if the behaviour implements INetworkBehaviourSerializer.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void MarkSerializerDirty()
        {
            if (IsDirty)
                return;

            if (this is not INetworkBehaviourSerializer)
                throw new InvalidOperationException("The behaviour does not implement INetworkBehaviourSerializer");

            _serializerDirty = true;
            NetworkManager.Instance.DirtyBehaviours.Add(this);
        }

        /// <summary>
        ///     Synchronizes the dirty values of this behaviour through the network.
        /// </summary>
        public void UpdateDirtyValues()
        {
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
                    nbs.OnSerialize(writer, false);
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

        /// <summary>
        ///     Synchronizes all data of this behaviour through the network.
        ///     Used to synchronize the current server state to a new client.
        /// </summary>
        /// <param name="clientId"></param>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public void SendBehaviourDataToClient(int clientId)
        {
            if (!IsServer)
                throw new Exception("This method can only be called on the server");

            var packet = GetBehaviourDataPacket();
            if (packet != null)
                ServerSendPacket(packet, clientId, true);
        }

        /// <summary>
        ///     Creates a packet containing all data of this behaviour.
        ///     Used to synchronize the current server state to a new client.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [ServerOnly]
        public NetworkBehaviourDataPacket GetBehaviourDataPacket()
        {
            if (!IsServer)
                throw new Exception("This method can only be called on the server");

            if (_values == null || _values.Length == 0)
            {
                var writer = new BinaryWriter(new MemoryStream());
                writer.Write((byte)0);

                if (this is INetworkBehaviourSerializer nbs)
                {
                    nbs.OnSerialize(writer, true);

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

                if (this is INetworkBehaviourSerializer nbs) nbs.OnSerialize(writer, true);

                return new NetworkBehaviourDataPacket
                {
                    Id = Id,
                    BehaviourId = BehaviourId,
                    Payload = ((MemoryStream)writer.BaseStream).ToArray()
                };
            }
        }

        /// <summary>
        ///     Applies the given payload to the values of this behaviour.
        ///     used to synchronize the values of this behaviour from the network.
        /// </summary>
        /// <param name="payload"></param>
        public void ApplyDirtyValues(byte[] payload)
        {
            var reader = new BinaryReader(new MemoryStream(payload));
            var count = reader.ReadByte();
            for (var i = 0; i < count; i++)
            {
                var index = reader.ReadByte();
                _values[index].Deserialize(reader);
            }

            if (reader.BaseStream.Position != reader.BaseStream.Length && this is INetworkBehaviourSerializer nbs)
                nbs.OnDeserialize(reader);
        }
        #endregion

        #region Packet Methods
        /// <summary>
        ///     Broadcasts a packet to all clients.
        ///     You can choose if the packet should be reliable or not.
        ///     Reliable packets are guaranteed to be delivered, but they are a little slower.
        ///     Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ServerBroadcastPacket(IPacket packet, bool reliable = false)
        {
            NetworkIdentity.ServerBroadcastPacket(packet, reliable);
        }

        /// <summary>
        ///     Broadcasts a packet to all clients except for the given client.
        ///     You can choose if the packet should be reliable or not.
        ///     Reliable packets are guaranteed to be delivered, but they are a little slower.
        ///     Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="except"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ServerBroadcastPacketExceptFor(IPacket packet, int except, bool reliable = false)
        {
            NetworkIdentity.ServerBroadcastPacketExceptFor(packet, except, reliable);
        }

        /// <summary>
        ///     Sends a packet to the given client.
        ///     You can choose if the packet should be reliable or not.
        ///     Reliable packets are guaranteed to be delivered, but they are a little slower.
        ///     Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="clientId"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ServerSendPacket(IPacket packet, int clientId, bool reliable = false)
        {
            NetworkIdentity.ServerSendPacket(packet, clientId, reliable);
        }

        /// <summary>
        ///     Sends a packet to the server.
        ///     You can choose if the packet should be reliable or not.
        ///     Reliable packets are guaranteed to be delivered, but they are a little slower.
        ///     Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClientSendPacket(IPacket packet, bool reliable = false)
        {
            NetworkIdentity.ClientSendPacket(packet, reliable);
        }

        /// <summary>
        ///     Sends a packet through the network, automatically choosing the correct method.
        ///     You can choose if the packet should be reliable or not.
        ///     Reliable packets are guaranteed to be delivered, but they are a little slower.
        ///     Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendPacket(IPacket packet, bool reliable = false)
        {
            Identity.SendPacket(packet, reliable);
        }

        /// <summary>
        ///     Gets the packet listener for the given packet type, so you can listen to packets of that type.
        ///     Does not work for IOwnedPacket types.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PacketListener<T> GetPacketListener<T>() where T : IPacket
        {
            return NetworkIdentity.GetPacketListener<T>();
        }
        #endregion

        #region Object Methods
        /// <summary>
        ///     Despawns the object from the network.
        ///     Requires authority.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RequiresAuthority]
        public NetworkAction<NetworkId, NetworkIdentity> Despawn()
        {
            return Identity.Despawn();
        }

        /// <summary>
        ///     Changes the active state of the object.
        ///     Requires authority.
        /// </summary>
        /// <returns></returns>
        /// <param name="active"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RequiresAuthority]
        public NetworkAction<NetworkId, NetworkIdentity> SetActive(bool active)
        {
            return Identity.SetActive(active);
        }

        /// <summary>
        ///     Sets the owner of the object.
        ///     Requires authority.
        /// </summary>
        /// <returns></returns>
        /// <param name="clientId"></param>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RequiresAuthority]
        public NetworkAction<NetworkId, NetworkIdentity> SetOwner(int clientId)
        {
            return Identity.SetOwner(clientId);
        }

        /// <summary>
        ///     Forces the transfer of ownership of the object.
        ///     Can be called by the server only.
        ///     Useful for preventing the despawn of object when the owner disconnects.
        /// </summary>
        /// <param name="clientId"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ServerOnly]
        public void ForceSetOwner(int clientId)
        {
            Identity.ForceSetOwner(clientId);
        }

        /// <summary>
        ///     Returns the network identity object with the given id.
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkIdentity GetNetworkObject(NetworkId objectId)
        {
            return NetworkIdentity.GetNetworkObject(objectId);
        }

        /// <summary>
        ///     Returns all the network identity objects.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<NetworkIdentity> GetNetworkObjects()
        {
            return NetworkIdentity.GetNetworkObjects();
        }

        /// <summary>
        ///     Returns the number of network identity objects.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetNetworkObjectCount()
        {
            return NetworkIdentity.GetNetworkObjectCount();
        }

        /// <summary>
        ///     Returns all the network identity objects owned by the given client.
        ///     If the client id is -1, it returns all the objects owned by the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int clientId)
        {
            return NetworkIdentity.GetNetworkObjectsOwnedBy(clientId);
        }
        #endregion

        #region Client Methods
        /// <summary>
        ///     Returns the index of the local client with the given id.
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLocalClientIndex(int clientId)
        {
            return Identity.GetLocalClientIndex(clientId);
        }

        /// <summary>
        ///     Returns the number of local clients.
        /// </summary>
        /// <returns></returns>
        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLocalClientCount()
        {
            return Identity.GetLocalClientCount();
        }

        /// <summary>
        ///     returns the client id of all local clients.
        /// </summary>
        /// <returns></returns>
        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<int> GetLocalClientIds()
        {
            return Identity.GetLocalClientIds();
        }
        #endregion

        #region Prefabs
        /// <summary>
        ///     Returns the prefab object registered with the given id.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameObject GetPrefabById(NetworkId prefab)
        {
            return NetworkIdentity.GetPrefabById(prefab);
        }

        /// <summary>
        ///     Returns the id of a registered prefab.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkId GetIdForPrefab(GameObject prefab)
        {
            return NetworkIdentity.GetIdForPrefab(prefab);
        }

        /// <summary>
        ///     Checks if the prefab is registered.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPrefabValid(NetworkId prefab)
        {
            return NetworkIdentity.IsPrefabValid(prefab);
        }
        #endregion

        #region Scene Moving
        /// <summary>
        ///     Moves this object to a different scene.
        ///     Requires authority.
        /// </summary>
        /// <returns></returns>
        /// <param name="sceneId"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RequiresAuthority]
        public NetworkAction<NetworkId, NetworkIdentity> MoveToScene(int sceneId)
        {
            return Identity.MoveToScene(sceneId);
        }

        /// <summary>
        ///     Moves this object to a different scene.
        ///     Requires authority.
        /// </summary>
        /// <param name="sceneName"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [RequiresAuthority]
        public void MoveToScene(string sceneName)
        {
            Identity.MoveToScene(sceneName);
        }
        #endregion

        #region Scene Utils
        /// <summary>
        ///     Returns the name of all the scenes loaded on the network.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<string> GetLoadedScenes()
        {
            return NetworkIdentity.GetLoadedScenes();
        }

        /// <summary>
        ///     Returns the id of the scene with the given name.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSceneId(string sceneName)
        {
            return NetworkIdentity.GetSceneId(sceneName);
        }

        /// <summary>
        ///     Returns the name of the scene with the given id.
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetSceneName(int sceneId)
        {
            return NetworkIdentity.GetSceneName(sceneId);
        }
        #endregion

        #region Spawning
        /// <summary>
        ///     Spawns a prefab object on the network.
        ///     If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkAction<NetworkId, NetworkIdentity> Spawn(GameObject prefab)
        {
            return NetworkIdentity.Spawn(prefab);
        }

        /// <summary>
        ///     Spawns a prefab object on the network.
        ///     If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="active"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkAction<NetworkId, NetworkIdentity> Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool active)
        {
            return NetworkIdentity.Spawn(prefab, position, rotation, active);
        }

        /// <summary>
        ///     Spawns a prefab object on the network.
        ///     If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="owner"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkAction<NetworkId, NetworkIdentity> Spawn(GameObject prefab, Vector3 position, Quaternion rotation, int owner)
        {
            return NetworkIdentity.Spawn(prefab, position, rotation, owner);
        }

        /// <summary>
        ///     Spawns a prefab object on the network.
        ///     If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkAction<NetworkId, NetworkIdentity> Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return NetworkIdentity.Spawn(prefab, position, rotation);
        }

        /// <summary>
        ///     Spawns a prefab object on the network.
        ///     If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        ///     If the scene id is 0, the object will be spawned in the main scene.
        ///     If the scene id is -1, the object will be spawned in the last loaded scene.
        ///     If the owner id is -1, the object will be owned by the server.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="active"></param>
        /// <param name="owner"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkAction<NetworkId, NetworkIdentity> Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale,
            bool active, int owner = -1, int scene = -1)
        {
            return NetworkIdentity.Spawn(prefab, position, rotation, scale, active, owner, scene);
        }

        /// <summary>
        ///     Spawns a prefab object on the network.
        ///     If the object does not have a NetworkIdentity component, the NetworkId will be discarded.
        ///     If the scene id is 0, the object will be spawned in the main scene.
        ///     If the scene id is -1, the object will be spawned in the last loaded scene.
        ///     If the owner id is -1, the object will be owned by the server.
        /// </summary>
        /// <param name="prefabId"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="active"></param>
        /// <param name="owner"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkAction<NetworkId, NetworkIdentity> Spawn(NetworkId prefabId, Vector3 position, Quaternion rotation, Vector3 scale,
            bool active, int owner = -1, int scene = -1)
        {
            return NetworkIdentity.Spawn(prefabId, position, rotation, scale, active, owner, scene);
        }
        #endregion
    }
}