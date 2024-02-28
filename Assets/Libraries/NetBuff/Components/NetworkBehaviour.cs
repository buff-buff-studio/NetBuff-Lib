using System.Collections.Generic;
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
        private NetworkIdentity _identity;
        
        /// <summary>
        /// Returns the NetworkIdentity attached to this object
        /// </summary>
        public NetworkIdentity Identity => _identity ??= GetComponent<NetworkIdentity>();
        
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
    }
}