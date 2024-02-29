using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        [SerializeField, HideInInspector]
        private NetworkId behaviourId;

        /// <summary>
        /// Represents the NetworkId of the behaviour. This is only unique NetworkIdentity-wise
        /// </summary>
        public NetworkId BehaviourId => behaviourId;

        /// <summary>
        /// Returns the NetworkIdentity attached to this object
        /// </summary>
        public NetworkIdentity Identity => _identity ??= GetComponent<NetworkIdentity>();
        private NetworkIdentity _identity;
        
        /// <summary>
        /// Returns if the local client has authority over this object
        /// If the object is not owned by the client, the server/host has authority over it
        /// </summary>
        public bool HasAuthority => Identity.HasAuthority;
        
        /// <summary>
        /// Returns if the object is owned by some client
        /// If the object is owned by the server/host, this will return false
        /// </summary>
        public bool IsOwnedByClient => Identity.IsOwnedByClient;
        
        /// <summary>
        /// Returns the NetworkId of this object
        /// </summary>
        public NetworkId Id => Identity.Id;
        
        /// <summary>
        /// Returns the id of the owner of this object (If the object is owned by the server, this will return -1)
        /// </summary>
        public int OwnerId => Identity.OwnerId;
        
        /// <summary>
        /// Returns the prefab used to spawn this object (Will be empty for pre-spawned objects)
        /// </summary>
        public NetworkId PrefabId => Identity.PrefabId;

        /// <summary>
        /// Returns if the behaviour is dirty
        /// </summary>
        public bool IsDirty => NetworkManager.Instance.dirtyBehaviours.Contains(this);

        /// <summary>
        /// Returns all values being tracked by this behaviour
        /// </summary>
        public ReadOnlySpan<NetworkValue> Values => new ReadOnlySpan<NetworkValue>(_values);
        private NetworkValue[] _values;
        private Queue<byte> _dirtyValues = new Queue<byte>();

        #region Values

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
            NetworkManager.Instance.dirtyBehaviours.Add(this);
        }

        /// <summary>
        /// Update all dirty values, generating a packet to sync them across the network
        /// </summary>
        public void UpdateDirtyValues()
        {
            BinaryWriter writer = new BinaryWriter(new MemoryStream());
            writer.Write((byte) _dirtyValues.Count);
            while (_dirtyValues.Count > 0)
            {
                var index = _dirtyValues.Dequeue();
                writer.Write(index);
                _values[index].Serialize(writer);
            }

            NetworkValuesPacket packet = new NetworkValuesPacket
            {
                IdentityId = Id,
                BehaviourId = BehaviourId,
                Payload = ((MemoryStream) writer.BaseStream).ToArray()
            };

            if(NetworkManager.Instance.IsServerRunning)
                ServerBroadcastPacket(packet, true);
            else
                ClientSendPacket(packet, true);
        }

        /// <summary>
        /// Send a packet to a specific client to sync all the NetowrkValues of this behaviour
        /// </summary>
        /// <param name="clientId"></param>
        [ServerOnly]
        public void SendNetworkValuesToClient(int clientId)
        {
            if(_values == null || _values.Length == 0)
                return;

            BinaryWriter writer = new BinaryWriter(new MemoryStream());
            writer.Write((byte) _values.Length);

            //Write all values
            for(var i = 0; i < _values.Length; i++)
            {
                writer.Write((byte) i);
                _values[i].Serialize(writer);
            }

            NetworkValuesPacket packet = new NetworkValuesPacket
            {
                IdentityId = Id,
                BehaviourId = BehaviourId,
                Payload = ((MemoryStream) writer.BaseStream).ToArray()
            };

            ServerSendPacket(packet, clientId, true);
        }

        /// <summary>
        /// Apply all values coming from a payload
        /// </summary>
        /// <param name="payload"></param>
        public void ApplyDirtyValues(byte[] payload)
        {
            BinaryReader reader = new BinaryReader(new MemoryStream(payload));
            var count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var index = reader.ReadByte();
                _values[index].Deserialize(reader);
            }
        }
        #endregion
        
        /// <summary>
        /// Broadcasts a packet to all clients
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void ServerBroadcastPacket(IPacket packet, bool reliable = false) => Identity.ServerBroadcastPacket(packet, reliable);
        
        /// <summary>
        /// Broadcasts a packet to all clients except for the specified one
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="except"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void ServerBroadcastPacketExceptFor(IPacket packet, int except, bool reliable = false) => Identity.ServerBroadcastPacketExceptFor(packet, except, reliable);
        
        /// <summary>
        /// Sends a packet to a specific client
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="clientId"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void ServerSendPacket(IPacket packet, int clientId, bool reliable = false) => Identity.ServerSendPacket(packet, clientId, reliable);
        
        /// <summary>
        /// Sends a packet to the server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        public void ClientSendPacket(IPacket packet, bool reliable = false) => Identity.ClientSendPacket(packet, reliable);

        /// <summary>
        /// Sends a packet to the server / all clients depending on the object's ownership
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        public void SendPacket(IPacket packet, bool reliable = false)
        {
            if (!HasAuthority)
                return;
            
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
            return Identity.GetPacketListener<T>();
        }
        
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
        /// <param name="id"></param>
        /// <returns></returns>
        public NetworkIdentity GetNetworkObject(NetworkId id)
        {
            return NetworkManager.Instance.GetNetworkObject(id);
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
        /// Returns all network objects owned by a specific client (Use -1 to get all objects owned by the server)
        /// </summary>
        /// <param name="clientId"></param>
        public void GetNetworkObjectsOwnedBy(int clientId)
        {
            NetworkManager.Instance.GetNetworkObjectsOwnedBy(clientId);
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
        /// Returns the local client index of the specified client id
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        [ClientOnly]
        public int GetLocalClientIndex(int clientId)
        {
            return Identity.GetLocalClientIndex(clientId);
        }


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

        #region Prefabs
        /// <summary>
        /// Returns the registered prefab by its id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public GameObject GetPrefabById(NetworkId id)
        {
            return NetworkManager.Instance.prefabRegistry.GetPrefab(id);
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
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsPrefabValid(NetworkId id)
        {
            return NetworkManager.Instance.prefabRegistry.IsPrefabValid(id);
        }
        #endregion

        private void OnValidate()
        {
            #if UNITY_EDITOR
            if (Identity.Behaviours.FirstOrDefault(b => b.behaviourId == behaviourId) == null)
                return;
            behaviourId = NetworkId.New();
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
    }
}