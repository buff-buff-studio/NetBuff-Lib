using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff
{
    /// <summary>
    /// Registry of prefabs that can be spawned by the network.
    /// All the objects that are spawned at runtime by the network must be registered here.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkPrefabRegistry", menuName = "NetBuff/NetworkPrefabRegistry", order = 0)]
    [Icon("Assets/Editor/Icons/NetworkPrefabRegistry.png")]
    public class NetworkPrefabRegistry : ScriptableObject
    {
        [SerializeField]
        private SerializedDictionary<NetworkId, GameObject> prefabs = new();
        
        /// <summary>
        /// Registry of prefabs that can be spawned by the network.
        /// </summary>
        public SerializedDictionary<NetworkId, GameObject> Prefabs => prefabs;

        /// <summary>
        /// Check if a prefab id is valid.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsPrefabValid(NetworkId id)
        {
            return prefabs.ContainsKey(id);
        }
        
        /// <summary>
        /// Check if a prefab is valid.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public bool IsPrefabValid(GameObject prefab)
        {
            return prefab != null && prefabs.ContainsValue(prefab);
        }
        
        /// <summary>
        /// Returns the prefab id of a prefab.
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        public NetworkId GetPrefabId(GameObject prefab)
        {
            var v = prefabs.FirstOrDefault(pair => pair.Value == prefab);
            return v.Value != null ? v.Key : NetworkId.Empty;
        }
        
        /// <summary>
        /// Returns the prefab of a prefab id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public GameObject GetPrefab(NetworkId id)
        {
            return prefabs.TryGetValue(id, out var prefab) ? prefab : null;
        }
        
        /// <summary>
        /// Returns all the prefabs.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<GameObject> GetAllPrefabs()
        {
            return prefabs.Values;
        }
    }
}