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
    /// Base class for all network object components
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
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
        /// Represents the NetworkId of the behaviour. This is only unique NetworkIdentity-wise
        /// </summary>
        public byte BehaviourId => (byte) Array.IndexOf(Identity.Behaviours, this);
        
        /// <summary>
        /// Returns if the behaviour is dirty
        /// </summary>
        public bool IsDirty => NetworkManager.Instance.DirtyBehaviours.Contains(this);

        /// <summary>
        /// Returns all values being tracked by this behaviour
        /// </summary>
        public ReadOnlySpan<NetworkValue> Values => new(_values);
        
        /// <summary>
        /// Returns the NetworkIdentity attached to this object
        /// </summary>
        public NetworkIdentity Identity => _identity ??= GetComponent<NetworkIdentity>();
        
        /// <summary>
        /// Returns the NetworkId of this object
        /// </summary>
        public NetworkId Id
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.Id; }
        }

        /// <summary>
        /// Returns the id of the owner of this object (If the object is owned by the server, this will return -1)
        /// </summary>
        public int OwnerId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.OwnerId; }
        }

        /// <summary>
        /// Returns the prefab used to spawn this object (Will be empty for pre-spawned objects)
        /// </summary>
        public NetworkId PrefabId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.PrefabId; }
        }

        /// <summary>
        /// Returns if the local client has authority over this object
        /// If the object is not owned by the client, the server/host has authority over it
        /// </summary>
        public bool HasAuthority
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.HasAuthority; }
        }

        /// <summary>
        /// Returns if the object is owned by some client
        /// If the object is owned by the server/host, this will return false
        /// </summary>
        public bool IsOwnedByClient
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.IsOwnedByClient; }
        }

        /// <summary>
        /// Returns the id of the scene the object is in
        /// </summary>
        public int SceneId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.SceneId; }
        }

        /// <summary>
        /// Returns if local environment is a server
        /// </summary>
        public bool IsServer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.IsServer; }
        }

        /// <summary>
        /// Returns the number of scenes loaded
        /// </summary>
        public int LoadedSceneCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.LoadedSceneCount; }
        }

        /// <summary>
        /// Returns the name of the source scene
        /// </summary>
        /// <returns></returns>
        public string SourceScene
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.SourceScene; }
        }

        /// <summary>
        /// Returns the name of the last loaded scene
        /// </summary>
        public string LastLoadedScene
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Identity.LastLoadedScene; }
        }

        #endregion
        
         #region Listeners
        /// <summary>
        /// Called when the object receives a packet (Server side)
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="clientId"></param>
        [ServerOnly]
        public virtual void OnServerReceivePacket(IOwnedPacket packet, int clientId) { }
        
        /// <summary>
        /// Called when the object receives a packet (Client side / Server-render side)
        /// </summary>
        /// <param name="packet"></param>
        [ClientOnly]
        public virtual void OnClientReceivePacket(IOwnedPacket packet) { }

        /// <summary>
        /// Called when the object is spawned. If the object is spawned retroactively, isRetroactive will be true
        /// </summary>
        /// <param name="isRetroactive"></param>
        public virtual void OnSpawned(bool isRetroactive){}
        
        /// <summary>
        /// Called when the object is moved to a different scene
        /// </summary>
        /// <param name="fromScene"></param>
        /// <param name="toScene"></param>
        public virtual void OnSceneChanged(int fromScene, int toScene) {}
        
        /// <summary>
        /// Called when a client connects to the server and the object is already spawned
        ///
        /// If the object has special data, you may want to send it to new clients here
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        public virtual void OnClientConnected(int clientId){}
        
        /// <summary>
        /// Called when the 
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        public virtual void OnClientDisconnected(int clientId){}
        
        /// <summary>
        /// Called when the object is despawned
        /// </summary>
        public virtual void OnDespawned(){}
        
        /// <summary>
        /// Called when the object's active state is changed
        /// </summary>
        /// <param name="active"></param>
        public virtual void OnActiveChanged(bool active){}
        
        /// <summary>
        /// Called when the object's owner is changed
        /// </summary>
        /// <param name="newOwner"></param>
        public virtual void OnOwnerChanged(int newOwner){}
        #endregion
        
        #region Value Methods

        /// <summary>
        /// Set all the values being tracked by this behaviour
        /// </summary>
        /// <param name="values"></param>
        public void WithValues(params NetworkValue[] values)
        {
            foreach (var value in values)
                value.AttachedTo = this;

            _values = values;
        }

        /// <summary>
        /// Mark a value as dirty to be updated across the network
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void MarkValueDirty(NetworkValue value)
        {
            if (_values == null)
                return;
            var index = Array.IndexOf(_values, value);
            if (index == -1)
                throw new InvalidOperationException("The value is not attached to this behaviour");
            MarkValueDirty((byte) index);
        }


        /// <summary>
        /// Mark a value as dirty to be updated across the network
        /// </summary>
        /// <param name="index"></param>
        public void MarkValueDirty(byte index)
        {
            _dirtyValues.Enqueue(index);

            if(IsDirty)
                return;            
            NetworkManager.Instance.DirtyBehaviours.Add(this);
        }
        
        
        /// <summary>
        /// Mark this behaviour as dirty to be updated across the network
        /// </summary>
        public void MarkSerializerDirty()
        {
            if(IsDirty)
                return;
            
            if(this is not INetworkBehaviourSerializer)
                throw new InvalidOperationException("The behaviour does not implement INetworkBehaviourSerializer");

            _serializerDirty = true;
            NetworkManager.Instance.DirtyBehaviours.Add(this);
        }
        
        /// <summary>
        /// Update all dirty values, generating a packet to sync them across the network
        /// </summary>
        public void UpdateDirtyValues()
        {
            var writer = new BinaryWriter(new MemoryStream());
            writer.Write((byte) _dirtyValues.Count);
            while (_dirtyValues.Count > 0)
            {
                var index = _dirtyValues.Dequeue();
                writer.Write(index);
                _values[index].Serialize(writer);
            }
            
            if (_serializerDirty)
            {
                if(this is INetworkBehaviourSerializer nbs)
                    nbs.OnSerialize(writer, false);
                _serializerDirty = false;
            }
            
            var packet = new NetworkValuesPacket
            {
                Id = Id,
                BehaviourId = BehaviourId,
                Payload = ((MemoryStream) writer.BaseStream).ToArray()
            };

            if (NetworkManager.Instance.IsServerRunning)
            {
                if(NetworkManager.Instance.IsClientRunning)
                    ServerBroadcastPacketExceptFor(packet, NetworkManager.Instance.LocalClientIds[0], true);
                else
                    ServerBroadcastPacket(packet, true);
            }
            else
                ClientSendPacket(packet, true);
        }

        /// <summary>
        /// Send a packet to a specific client to sync all the NetworkValues of this behaviour
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        public void SendNetworkValuesToClient(int clientId)
        {
            if(!IsServer)
                throw new Exception("This method can only be called on the server");
            
            var packet = GetPreExistingValuesPacket();
            if(packet != null)
                ServerSendPacket(packet, clientId, true);
        }
        
        /// <summary>
        /// Creates a packet containing all the NetworkValues of this behaviour
        /// </summary>
        /// <returns></returns>
        [ServerOnly]
        public NetworkValuesPacket GetPreExistingValuesPacket()
        {
            if(!IsServer)
                throw new Exception("This method can only be called on the server");
            
            if(_values == null || _values.Length == 0)
            {
                var writer = new BinaryWriter(new MemoryStream());
                writer.Write((byte) 0);
                
                if (this is INetworkBehaviourSerializer nbs)
                {
                    nbs.OnSerialize(writer, true);
                    
                    return new NetworkValuesPacket
                    {
                        Id = Id,
                        BehaviourId = BehaviourId,
                        Payload = ((MemoryStream) writer.BaseStream).ToArray()
                    };
                }
                return null;
            }
            else
            {
                var writer = new BinaryWriter(new MemoryStream());
                writer.Write((byte) _values.Length);
    
                //Write all values
                for(var i = 0; i < _values.Length; i++)
                {
                    writer.Write((byte) i);
                    _values[i].Serialize(writer);
                }
                
                if (this is INetworkBehaviourSerializer nbs)
                {
                    nbs.OnSerialize(writer, true);
                }
                
                return new NetworkValuesPacket
                {
                    Id = Id,
                    BehaviourId = BehaviourId,
                    Payload = ((MemoryStream) writer.BaseStream).ToArray()
                };
            }
        }

        /// <summary>
        /// Apply all values coming from a payload
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
            
            if(reader.BaseStream.Position != reader.BaseStream.Length && this is INetworkBehaviourSerializer nbs )
            {
                nbs.OnDeserialize(reader);
            }
        }
        #endregion

        #region Packet Methods
        /// <summary>
        /// Broadcasts a packet to all clients
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ServerBroadcastPacket(IPacket packet, bool reliable = false) => Identity.ServerBroadcastPacket(packet, reliable);
        
        /// <summary>
        /// Broadcasts a packet to all clients except for the specified one
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="except"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ServerBroadcastPacketExceptFor(IPacket packet, int except, bool reliable = false) => Identity.ServerBroadcastPacketExceptFor(packet, except, reliable);
        
        /// <summary>
        /// Sends a packet to a specific client
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="clientId"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ServerSendPacket(IPacket packet, int clientId, bool reliable = false) => Identity.ServerSendPacket(packet, clientId, reliable);
        
        /// <summary>
        /// Sends a packet to the server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClientSendPacket(IPacket packet, bool reliable = false) => Identity.ClientSendPacket(packet, reliable);

        /// <summary>
        /// Sends a packet to the server / all clients depending on the object's ownership
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendPacket(IPacket packet, bool reliable = false) => Identity.SendPacket(packet, reliable);
 
        /// <summary>
        /// Returns the packet listener for the specified packet type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PacketListener<T> GetPacketListener<T>() where T : IPacket => Identity.GetPacketListener<T>();
        #endregion

        #region Object Methods
        /// <summary>
        /// Tries to despawn the object across all clients (If you have authority)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Despawn() => Identity.Despawn();
        
        /// <summary>
        /// Try to set the active state of the object across all clients (If you have authority)
        /// </summary>
        /// <param name="active"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetActive(bool active) => Identity.SetActive(active);
        
        /// <summary>
        /// Try to set the owner of the object across all clients (If you have authority)
        /// </summary>
        /// <param name="clientId"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetOwner(int clientId) => Identity.SetOwner(clientId);
        
        /// <summary>
        /// Returns a network object by its id
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkIdentity GetNetworkObject(NetworkId objectId) => Identity.GetNetworkObject(objectId);
        
        /// <summary>
        /// Returns all network objects
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<NetworkIdentity> GetNetworkObjects() => Identity.GetNetworkObjects();
        
        /// <summary>
        /// Returns the count of network objects
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNetworkObjectCount() => Identity.GetNetworkObjectCount();
        
        /// <summary>
        /// Returns all network objects owned by a specific client (Use -1 to get all objects owned by the server)
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<NetworkIdentity> GetNetworkObjectsOwnedBy(int clientId) => Identity.GetNetworkObjectsOwnedBy(clientId);
        #endregion

        #region Client Methods
        /// <summary>
        /// Returns the local client index of the specified client id
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [ClientOnly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLocalClientIndex(int clientId) => Identity.GetLocalClientIndex(clientId);
        #endregion

        #region Prefabs
        /// <summary>
        /// Returns the registered prefab by its id
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameObject GetPrefabById(NetworkId prefab) => Identity.GetPrefabById(prefab);
        
        /// <summary>
        /// Returns the id of a registered prefab
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkId GetIdForPrefab(GameObject prefab) => Identity.GetIdForPrefab(prefab);
        
        /// <summary>
        /// Returns if a given prefab id is registered
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPrefabValid(NetworkId prefab) => Identity.IsPrefabValid(prefab);
        #endregion

        #region Scene Moving
        /// <summary>
        /// Moves the object to a different scene
        /// </summary>
        /// <param name="sceneId"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveToScene(int sceneId) => Identity.MoveToScene(sceneId);

        /// <summary>
        /// Moves the object to a different scene
        /// </summary>
        /// <param name="sceneName"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveToScene(string sceneName) => Identity.MoveToScene(sceneName);
        #endregion

        #region Scene Utils
        /// <summary>
        /// Returns all loaded scenes
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<string> GetLoadedScenes() => Identity.GetLoadedScenes();

        /// <summary>
        /// Returns the id of a scene by its name
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public int GetSceneId(string sceneName) => Identity.GetSceneId(sceneName);

        /// <summary>
        /// Returns the name of a scene by its id
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetSceneName(int sceneId) => Identity.GetSceneName(sceneId);
        #endregion

        #region Spawning
        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkId Spawn(GameObject prefab) => NetworkIdentity.Spawn(prefab);

        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="active"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool active) => NetworkIdentity.Spawn(prefab, position, rotation, active);

        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="owner"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, int owner) => NetworkIdentity.Spawn(prefab, position, rotation, owner);

        /// <summary>
        /// Spawns a new object across the network
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation) => NetworkIdentity.Spawn(prefab, position, rotation);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkId Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, bool active, int owner = -1, int scene = -1) => NetworkIdentity.Spawn(prefab, position, rotation, scale, active, owner, scene);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetworkId Spawn(NetworkId prefabId, Vector3 position, Quaternion rotation, Vector3 scale, bool active, int owner = -1, int scene = -1) => NetworkIdentity.Spawn(prefabId, position, rotation, scale, active, owner, scene);
        #endregion
    }
}