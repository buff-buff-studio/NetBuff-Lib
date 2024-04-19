using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff
{
    /// <summary>
    /// Used to store prefabs for networked objects, to reference them across the network
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkPrefabRegistry", menuName = "BuffBuffNetcode/NetworkPrefabRegistry", order = 0)]
    public class NetworkPrefabRegistry : ScriptableObject
    {
        [SerializeField]
        private SerializedDictionary<NetworkId, GameObject> prefabs = new SerializedDictionary<NetworkId, GameObject>();
        public SerializedDictionary<NetworkId, GameObject> Prefabs => prefabs;

        /// <summary>
        /// Returns if a given prefab id is registered
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsPrefabValid(NetworkId id)
        {
            return prefabs.ContainsKey(id);
        }
        
        /// <summary>
        /// Returns if a given prefab is registered
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public bool IsPrefabValid(GameObject prefab)
        {
            return prefab != null && prefabs.ContainsValue(prefab);
        }
        
        /// <summary>
        /// Returns the prefab id for a given prefab
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public NetworkId GetPrefabId(GameObject prefab)
        {
            var v = prefabs.FirstOrDefault(pair => pair.Value == prefab);
            return v.Value != null ? v.Key : NetworkId.Empty;
        }

        /// <summary>
        /// Returns the prefab for a given prefab id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public GameObject GetPrefab(NetworkId id)
        {
            return prefabs.TryGetValue(id, out var prefab) ? prefab : null;
        }

        /// <summary>
        /// Return all prefabs
        /// </summary>
        /// <returns></returns>
        public IEnumerable<GameObject> GetAllPrefabs()
        {
            return prefabs.Values;
        }
    }
}