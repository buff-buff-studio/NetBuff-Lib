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

            var manager = target as NetworkManager;
            if (!manager!.IsReady)
                EditorGUILayout.HelpBox("NetworkManager is not ready", MessageType.Warning);
            
            #if NETBUFF_ADVANCED_DEBUG
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Debug General Settings", EditorStyles.boldLabel);
            DebugUtilities.EnableAdvancedDebugging = EditorGUILayout.Toggle("Enable Advanced Debugging", DebugUtilities.EnableAdvancedDebugging);
            if (DebugUtilities.EnableAdvancedDebugging)
            {
                DebugUtilities.DefaultFillBounds =
                    EditorGUILayout.Toggle("Default Fill Bounds", DebugUtilities.DefaultFillBounds);

                EditorGUILayout.LabelField("Network Transform Debugging", EditorStyles.boldLabel);
                DebugUtilities.NetworkTransformDraw =
                    EditorGUILayout.Toggle("Network Transform Draw", DebugUtilities.NetworkTransformDraw);
                
                if(DebugUtilities.NetworkTransformDraw)
                    DebugUtilities.NetworkTransformDrawSleep = EditorGUILayout.Toggle("Network Transform Draw Sleep",
                        DebugUtilities.NetworkTransformDrawSleep);
                
                EditorGUILayout.LabelField("Network Identity Debugging", EditorStyles.boldLabel);
                DebugUtilities.NetworkIdentityDraw =
                    EditorGUILayout.Toggle("Network Identity Draw", DebugUtilities.NetworkIdentityDraw);
                
                if (DebugUtilities.NetworkIdentityDraw)
                {
                    DebugUtilities.NetworkIdentityDrawNames =
                        EditorGUILayout.Toggle("Network Identity Draw Names", DebugUtilities.NetworkIdentityDrawNames);
                    
                    EditorGUILayout.LabelField("Network Behaviour Debugging", EditorStyles.boldLabel);
                    DebugUtilities.NetworkIdentityDrawBehaviourNames =
                        EditorGUILayout.Toggle("Network Behaviour Names",
                            DebugUtilities.NetworkIdentityDrawBehaviourNames);
                    
                    if(DebugUtilities.NetworkIdentityDrawBehaviourNames)
                        DebugUtilities.NetworkIdentityDrawBehaviourNamesSleep = EditorGUILayout.Toggle(
                            "Network Behaviour Names Sleep",
                            DebugUtilities.NetworkIdentityDrawBehaviourNamesSleep);
                }

                EditorGUILayout.HelpBox("Advanced debugging is enabled. This may impact performance.",
                        MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
            #endif
        }
    }
#endif
}