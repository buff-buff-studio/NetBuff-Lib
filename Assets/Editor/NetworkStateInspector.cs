
using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using NetBuff.Session;
using UnityEditor;
#endif

namespace NetBuff.Editor
{
    #if UNITY_EDITOR
    public class NetworkStateInspector : EditorWindow
    {
        [Serializable]
        public class ClientFoldout
        {
            public bool main = false;
            public bool data = false;
            public bool objects = false;
        }
        
        public SerializedDictionary<int, ClientFoldout> foldoutsClientsRemote = new();
        public SerializedDictionary<int, ClientFoldout> foldoutsClientsLocal = new();
        
        public SerializedDictionary<string, bool> foldoutsScenes = new();
        public SerializedDictionary<NetworkId, bool> foldoutsObjects = new();
        public Vector2 scrollPosition;
       
        [MenuItem("Window/Network/State Inspector")]
        public static void ShowWindow()
        {
            GetWindow<NetworkStateInspector>("Network State Inspector");
        }
        
        //Show the object info bellow
        
        EditorGUISplitView verticalSplitView = new EditorGUISplitView (EditorGUISplitView.Direction.Vertical);
        
        private void OnGUI()
        {
            verticalSplitView.BeginSplitView ();
            GUILayout.Label("Network State Inspector", EditorStyles.boldLabel);
            verticalSplitView.Split ();
            GUILayout.Label("Network State Inspector", EditorStyles.boldLabel);
            verticalSplitView.EndSplitView ();
            Repaint();
            
            /*
            var manager = NetworkManager.Instance;
            if (manager == null)
            {
                //Create warning message
                EditorGUILayout.HelpBox("No active NetworkManager found!", MessageType.Warning);
                return;
            }
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            #region Clients
            GUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Network Clients", EditorStyles.boldLabel);
            
            if(manager.Transport == null)
                EditorGUILayout.HelpBox("No active transport found!", MessageType.Warning);
            else if(manager.EnvironmentType is NetworkTransport.EnvironmentType.None) 
                EditorGUILayout.HelpBox("No active environment found!", MessageType.Warning);
            else
            {
                if(manager.IsClientRunning)
                    _DrawConnectionInfo(manager, manager.ClientConnectionInfo, true);
                
                if(manager.IsServerRunning)
                {
                    foreach (var client in manager.Transport.GetClients())
                        if (!_DrawConnectionInfo(manager, client, false))
                            break;
                }
            }

            GUILayout.EndVertical();
            #endregion
            
            //Network Objects box
            GUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Network Objects", EditorStyles.boldLabel);
            
            //filter objects by name, id or components
            var objects = manager.GetNetworkObjects();
            
            foreach (var (scene, sceneObjects) in _SeparateByScenes(manager, objects))
            {
                var open = foldoutsScenes[scene] = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutsScenes.GetValueOrDefault(scene, false), scene);
                EditorGUILayout.EndFoldoutHeaderGroup();
                
                if (open)
                {
                    EditorGUILayout.BeginVertical("Box");
                    foreach (var identity in sceneObjects)
                        _DrawNetworkIdentityInfo(manager, identity, true);
                    EditorGUILayout.EndVertical();
                }
            }
            
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            */
        }

        private IEnumerable<(string id, IEnumerable<NetworkIdentity> objects)> _SeparateByScenes(NetworkManager manager, IEnumerable<NetworkIdentity> objects)
        {
            var filtered = objects.ToList();
            
            var loadedScenes = manager.LoadedScenes.ToArray();
            for(var i = 0; i < loadedScenes.Length; i++)
            {
                var sceneObjects = filtered.Where(o => o.SceneId == i).ToList();
                filtered.RemoveAll(o => o.SceneId == i);
                
                yield return (loadedScenes[i], sceneObjects);
            }
        }
        
        #region Utils
        private void _DrawNetworkIdentityInfo(NetworkManager manager, NetworkIdentity identity, bool sortedByScenes)
        {
            var label = $"{identity.gameObject.name}";

            var open = foldoutsObjects[identity.Id] = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutsObjects.GetValueOrDefault(identity.Id, false), label);
            
            var idLabel = identity.Id.ToString();
            var needWidth = GUI.skin.label.CalcSize(new GUIContent(idLabel)).x;
            var rect = GUILayoutUtility.GetLastRect();
            EditorGUI.LabelField(new Rect(rect.x + rect.width - needWidth, rect.y, needWidth, rect.height), identity.Id.ToString());
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (open)
            {
                EditorGUI.BeginDisabledGroup(true);
                
                //Draw basic info
                EditorGUILayout.TextField("Owner", identity.OwnerId == -1 ? "Server" : identity.OwnerId.ToString());
                EditorGUILayout.Toggle("Authority", identity.HasAuthority);

                if (!sortedByScenes)
                {
                    var safeName = identity.SceneId < manager.LoadedSceneCount ? manager.GetSceneName(identity.SceneId) : "Unknown";
                    EditorGUILayout.TextField("Scene", safeName);
                }
                
                EditorGUILayout.ObjectField("Prefab", _GetPrefab(manager, identity), typeof(GameObject), false);
                
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Ping Object"))
                {
                    EditorGUIUtility.PingObject(identity.gameObject);
                }

                if (identity.HasAuthority)
                {
                    if (GUILayout.Button("Despawn"))
                    {
                        identity.Despawn();
                    }

                    if (GUILayout.Button("Toggle Active"))
                    {
                        identity.SetActive(!identity.gameObject.activeSelf);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        
        private GameObject _GetPrefab(NetworkManager manager, NetworkIdentity identity)
        {
            if (manager.PrefabRegistry == null)
                return null;
            
            return manager.PrefabRegistry.GetPrefab(identity.PrefabId);
        }

        private void _DrawObjectReadOnly(object o)
        {
            EditorGUI.BeginDisabledGroup(true);
            foreach (var field in o.GetType().GetFields())
            {
                _DrawObjectField(field.Name, field.GetValue(o));
            }
            EditorGUI.EndDisabledGroup();
        }

        private void _DrawObjectField(string label, object value)
        {
            switch (value)
            {
                case string s:
                    EditorGUILayout.TextField(label, s);
                    break;
                case int i:
                    EditorGUILayout.IntField(label, i);
                    break;
                case float f:
                    EditorGUILayout.FloatField(label, f);
                    break;
                case double d:
                    EditorGUILayout.DoubleField(label, d);
                    break;
                case bool b:
                    EditorGUILayout.Toggle(label, b);
                    break;
                case Enum e:
                    EditorGUILayout.EnumPopup(label, e);
                    break;
                case null:
                    EditorGUILayout.TextField(label, "null");
                    break;
                case Vector2 v2:
                    EditorGUILayout.Vector2Field(label, v2);
                    break;
                case Vector3 v3:
                    EditorGUILayout.Vector3Field(label, v3);
                    break;
                case Color c:
                    EditorGUILayout.ColorField(label, c);
                    break;
                case Color32 c32:
                    EditorGUILayout.ColorField(label, c32);
                    break;
                default:
                    EditorGUILayout.LabelField(label, value?.ToString() ?? "null");
                    break;
            }
        }

        private void _DrawConnectionInfoSessionData(NetworkManager manager, int id, bool local)
        {
            if (!manager.TryGetSessionData(id, out SessionData data))
            {
                EditorGUILayout.HelpBox("No session data found!", MessageType.Warning);
                return;
            }
            
            var foldout = _GetClientFoldout(id, local);
            var open = foldout.data = EditorGUILayout.BeginFoldoutHeaderGroup(foldout.data, "Session Data");
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            if (open)
                _DrawObjectReadOnly(data);
        }
        
        private void _DrawConnectionInfoObjects(NetworkManager manager, int id, bool local)
        {
            var objects = manager.GetNetworkObjectsOwnedBy(id);
            
            var foldout = _GetClientFoldout(id, local);
            var open = foldout.objects = EditorGUILayout.BeginFoldoutHeaderGroup(foldout.objects, "Owned Objects");
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            if (open)
            {
                EditorGUILayout.BeginVertical("Box");
                foreach (var identity in objects)
                    _DrawNetworkIdentityInfo(manager, identity, false);
                EditorGUILayout.EndVertical();
            }
        }
        
        private ClientFoldout _GetClientFoldout(int id, bool local)
        {
            if (local)
            {
                foldoutsClientsLocal.TryAdd(id, new ClientFoldout());
                return foldoutsClientsLocal[id];
            }
            
            foldoutsClientsRemote.TryAdd(id, new ClientFoldout());
            return foldoutsClientsRemote[id];
        }

        private bool _DrawConnectionInfo(NetworkManager manager, IConnectionInfo info, bool local)
        {
            GUILayout.BeginVertical("Box");

            var id = local ? manager.LocalClientIds[0] : ((IClientConnectionInfo)info).Id;
            var idLabel = local ? "Local" : $"#{id}";
            
            var foldout = _GetClientFoldout(id, local);
            var open = foldout.main = EditorGUILayout.BeginFoldoutHeaderGroup(foldout.main, idLabel);
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            if (open)
            {
                EditorGUILayout.LabelField("Latency", info.Latency.ToString());
                EditorGUILayout.LabelField("Packets", $"R {info.PacketReceived} | S {info.PacketSent} | L {info.PacketLoss} ({info.PacketLossPercentage:0.00}%)");
                
                if (!local)
                {
                    if (GUILayout.Button("Disconnect"))
                    {
                        GUILayout.EndVertical();
                        manager.Transport.ServerDisconnect(id, "disconnect");
                        return false;
                    }
                }
                
                _DrawConnectionInfoSessionData(manager, id, local);
                _DrawConnectionInfoObjects(manager, id, local);
            }
            
            GUILayout.EndVertical();
            return true;
        }
        #endregion
    }
    #endif
}