using System.Linq;
using System.Reflection;
using NetBuff.Components;
using NetBuff.Misc;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NetBuff.Editor
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(NetworkManager), true)]
    public class NetworkManagerEditor : UnityEditor.Editor
    {
        private static readonly FieldInfo _IDField = typeof(NetworkIdentity).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Regenerate Ids"))
            {
                foreach (var identity in FindObjectsByType<NetworkIdentity>(FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    _IDField.SetValue(identity, NetworkId.New());
                    EditorUtility.SetDirty(identity);
                }
            }

            if (GUILayout.Button("Dump Ids"))
            {
                var path = EditorUtility.SaveFilePanel("Save Ids", "", "Ids", "txt");
                if (path.Length != 0)
                {
                    var ids = NetworkManager.Instance.GetNetworkObjects();

                    System.IO.File.WriteAllText(path, string.Join("\n", ids.Select(x => $"{x.gameObject.name}: {x.Id}")));
                    System.Diagnostics.Process.Start(path);
                }
            }
        }
    }
#endif
}