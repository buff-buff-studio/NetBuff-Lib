using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff
{
    [CreateAssetMenu(fileName = "NetworkPrefabRegistry", menuName = "NetBuff/NetworkPrefabRegistry", order = 0)]
    [Icon("Assets/Editor/Icons/NetworkPrefabRegistry.png")]
    public class NetworkPrefabRegistry : ScriptableObject
    {
        [SerializeField]
        private SerializedDictionary<NetworkId, GameObject> prefabs = new SerializedDictionary<NetworkId, GameObject>();
        
        public SerializedDictionary<NetworkId, GameObject> Prefabs => prefabs;

        public bool IsPrefabValid(NetworkId id)
        {
            return prefabs.ContainsKey(id);
        }
        
        public bool IsPrefabValid(GameObject prefab)
        {
            return prefab != null && prefabs.ContainsValue(prefab);
        }
        
        public NetworkId GetPrefabId(GameObject prefab)
        {
            var v = prefabs.FirstOrDefault(pair => pair.Value == prefab);
            return v.Value != null ? v.Key : NetworkId.Empty;
        }

        public GameObject GetPrefab(NetworkId id)
        {
            return prefabs.TryGetValue(id, out var prefab) ? prefab : null;
        }

        public IEnumerable<GameObject> GetAllPrefabs()
        {
            return prefabs.Values;
        }
    }
}