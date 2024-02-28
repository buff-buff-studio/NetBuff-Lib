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
        [SerializeField]
        private NetworkId id;
        [SerializeField]
        private int ownerId = -1;
        [SerializeField]
        private NetworkId prefabId = NetworkId.Empty;
        
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
        /// Returns if the object is owned by some client
        /// If the object is owned by the server/host, this will return false
        /// </summary>
        public bool IsOwnedByClient => ownerId != -1;
        
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
                    NetworkTransport.EndType.Host => (ownerId == -1 && man.IsServerRunning) || (ownerId == man.ClientId && man.IsClientRunning),
                    NetworkTransport.EndType.Client => ownerId != -1 && ownerId == man.ClientId,
                    NetworkTransport.EndType.Server => ownerId == -1,
                    _ => false
                };
            }
        }
        
        private NetworkBehaviour[] _behaviours;
        
        /// <summary>
        /// Returns all NetworkBehaviours attached to this object
        /// </summary>
        public NetworkBehaviour[] Behaviours => _behaviours ??= GetComponents<NetworkBehaviour>();
        
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
        
        private void OnValidate()
        {
            #if UNITY_EDITOR
            var identities = FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (identities.Where(identity => identity != this).All(identity => identity.id != id)) return;
            id = NetworkId.New();
            EditorUtility.SetDirty(this);
            #endif
        }
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(NetworkIdentity))]
    public class NetworkIdentityEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
            EditorGUI.EndDisabledGroup();
            DrawPropertiesExcluding(serializedObject, "id", "m_Script", "ownerId", "prefabId");
            serializedObject.ApplyModifiedProperties();
        }
    }
    #endif
}