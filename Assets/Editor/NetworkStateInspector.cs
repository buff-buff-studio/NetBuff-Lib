
using System;
using System.Linq;
using UnityEngine;
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
            public bool main;
            public bool data;
            public bool objects;
            
            public SerializedDictionary<NetworkId, ObjectFoldout> objectsFoldouts = new();
        }

        [Serializable]
        public class ObjectFoldout
        {
            public bool main;
            public bool behaviours;
            
            public SerializedDictionary<int, bool> behavioursFoldouts = new();
        }

        [Serializable]
        public class Filter
        {
            public string search;
            public List<Type> Components = new();
            
            public void Clear()
            {
                search = "";
                Components.Clear();
            }
        }

        public SerializedDictionary<int, ClientFoldout> foldoutsLocalClients = new();
        public SerializedDictionary<int, ClientFoldout> foldoutsRemoteClients = new();
        public SerializedDictionary<NetworkId, ObjectFoldout> foldoutsObjects = new();

        public Vector2 scrollMainPanel;
        public Vector2 scrollObjectsPanel;
        public EditorGUISplitView verticalSplitView = new(EditorGUISplitView.Direction.Vertical);
        public SerializedDictionary<string, bool> foldoutsScenes = new();
        public Filter filter = new();

        [MenuItem("NetBuff/Network State Inspector")]
        [MenuItem("Window/Network/State Inspector")]
        public static void ShowWindow()
        {
            GetWindow<NetworkStateInspector>("Network State Inspector");
        }

        private void OnGUI()
        {
            var manager = NetworkManager.Instance;
            if (manager == null)
            {
                EditorGUILayout.HelpBox("No active NetworkManager found!", MessageType.Warning);
                return;
            }

            verticalSplitView.BeginSplitView();

            #region Connections
            scrollMainPanel = EditorGUILayout.BeginScrollView(scrollMainPanel);
            GUILayout.BeginVertical("Box");

            if (manager.Transport == null)
            {
                GUILayout.BeginVertical("Box");
                EditorGUILayout.LabelField("Local Client", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("No active transport found!", MessageType.Warning);
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical("Box");
                EditorGUILayout.LabelField("Local Client", EditorStyles.boldLabel);

                if (manager.IsClientRunning)
                    _DrawConnectionInfo(manager, manager.ClientConnectionInfo, true, true);
                else
                    EditorGUILayout.HelpBox("No active local client found!", MessageType.Warning);

                GUILayout.EndVertical();

                GUILayout.BeginVertical("Box");
                EditorGUILayout.LabelField("Remote Clients", EditorStyles.boldLabel);

                if (manager.IsServerRunning)
                {
                    foreach (var client in manager.Transport.GetClients())
                        if (!_DrawConnectionInfo(manager, client, true))
                            break;
                }
                else
                    EditorGUILayout.HelpBox("No active server found!", MessageType.Warning);

                GUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            #endregion

            verticalSplitView.Split();

            #region Objects

            scrollObjectsPanel = EditorGUILayout.BeginScrollView(scrollObjectsPanel);
            
            GUILayout.BeginVertical("Box");
            
            EditorGUILayout.LabelField("Network Scenes / Objects", EditorStyles.boldLabel);
            
            _DrawFilter();
            
            var objects = _ApplyFilter(manager.GetNetworkObjects());
            
            foreach (var (scene, sceneObjects) in _SeparateByScenes(manager, objects))
            {
                var networkIdentities = sceneObjects as NetworkIdentity[] ?? sceneObjects.ToArray();
                
                if (networkIdentities.Length == 0)
                    continue;

                var open = foldoutsScenes[scene] = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutsScenes.GetValueOrDefault(scene, false), scene);
                EditorGUILayout.EndFoldoutHeaderGroup();
                
                EditorGUILayout.BeginVertical("Box");
                
                if (manager.IsServerRunning)
                {
                    if (manager.MainScene == scene)
                    {
                        EditorGUILayout.HelpBox("Main scene can't be unloaded!", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Unload Scene"))
                        {
                            manager.UnloadScene(scene);
                        }

                        if (GUILayout.Button("Reload Scene"))
                        {
                            manager.UnloadScene(scene);
                            manager.LoadScene(scene);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Only server can load/unload scenes!", MessageType.Warning);
                }
                
                if (open)
                {
                    foreach (var identity in networkIdentities)
                        _DrawNetworkIdentity(manager, identity, true, foldoutsObjects);
                }
                
                EditorGUILayout.EndVertical();
            }
            
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            #endregion

            verticalSplitView.EndSplitView();
            Repaint();
        }

        private void _DrawFilter()
        {
            EditorGUILayout.BeginHorizontal();
            filter.search = EditorGUILayout.TextField("Search", filter.search);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                Undo.RecordObject(this, "Clear Search");
                filter.search = "";
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private IEnumerable<NetworkIdentity> _ApplyFilter(IEnumerable<NetworkIdentity> objects)
        {
            var filterById = false;
            var filterId = NetworkId.Empty;

            if(NetworkId.TryParse(filter.search, out var id))
            {
                filterById = true;
                filterId = id;
            }
            
            return objects.Where(o =>
            {
                if (filterById)
                    return o.Id == filterId;
                
                if (filter.Components.Count > 0)
                {
                    var behaviours = o.Behaviours.Select(b => b.GetType()).ToList();
                    if (filter.Components.Any(c => !behaviours.Contains(c)))
                        return false;
                }
                
                if (string.IsNullOrEmpty(filter.search))
                    return true;

                if (o.gameObject.name.Contains(filter.search))
                    return true;


                return false;
            });
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
        
        private bool _DrawConnectionInfo(NetworkManager manager, IConnectionInfo info, bool simple, bool local = false)
        {
            GUILayout.BeginVertical("Box");

            var id = local ? manager.LocalClientIds[0] : ((IClientConnectionInfo)info).Id;
            var foldout = local
                ? foldoutsLocalClients.GetValueOrDefault(id, new ClientFoldout())
                : foldoutsRemoteClients.GetValueOrDefault(id, new ClientFoldout());

            if (local)
                foldoutsLocalClients[id] = foldout;
            else
                foldoutsRemoteClients[id] = foldout;

            if (_DrawHeader(info, local ? "Local Client" : $"Client #{id}", simple, () => foldout.main,
                    value => foldout.main = value))
            {
                EditorGUILayout.LabelField("Latency", info.Latency.ToString());
                EditorGUILayout.LabelField("Packets",
                    $"R {info.PacketReceived} | S {info.PacketSent} | L {info.PacketLoss} ({info.PacketLossPercentage:0.00}%)");

                _DrawConnectionInfoSessionData(manager, id, foldout, simple);
                _DrawConnectionInfoObjects(manager, id, foldout, simple);

                if (!local)
                {
                    if (GUILayout.Button("Disconnect"))
                    {
                        GUILayout.EndVertical();
                        manager.Transport.ServerDisconnect(id, "disconnect");
                        return false;
                    }
                }
            }

            GUILayout.EndVertical();
            return true;
        }

        private void _DrawConnectionInfoSessionData(NetworkManager manager, int id, ClientFoldout foldout, bool simple)
        {
            GUILayout.BeginVertical("Box");

            if (_DrawHeader(null, "Session Data", simple, () => foldout.data, value => foldout.data = value))
            {
                if (!manager.TryGetSessionData(id, out SessionData data))
                    EditorGUILayout.HelpBox("No session data found!", MessageType.Warning);
                else
                    _DrawObjectReadOnly(data);
            }

            GUILayout.EndVertical();
        }

        private void _DrawConnectionInfoObjects(NetworkManager manager, int id, ClientFoldout foldout, bool simple)
        {
            GUILayout.BeginVertical("Box");

            if (_DrawHeader(null, "Owned Objects", simple, () => foldout.objects,
                    value => foldout.objects = value))
            {
                var objects = manager.GetNetworkObjectsOwnedBy(id);
                EditorGUILayout.BeginVertical("Box");
                foreach (var identity in objects)
                    _DrawNetworkIdentity(manager, identity, simple, foldout.objectsFoldouts);
                EditorGUILayout.EndVertical();
            }

            GUILayout.EndVertical();
        }

        private GameObject _GetPrefab(NetworkManager manager, NetworkIdentity identity)
        {
            if (manager.PrefabRegistry == null)
                return null;

            return manager.PrefabRegistry.GetPrefab(identity.PrefabId);
        }
        
        private void _DrawNetworkIdentity(NetworkManager manager, NetworkIdentity identity, bool simple, SerializedDictionary<NetworkId, ObjectFoldout> dict)
        {
            var foldout = dict.GetValueOrDefault(identity.Id, new ObjectFoldout());
            dict[identity.Id] = foldout;
            
            GUILayout.BeginVertical("Box");
            var open = _DrawHeader(identity, identity.gameObject.name, simple, () => foldout.main,
                value => foldout.main = value);
            
            var idLabel = identity.Id.ToString();
            var needWidth = GUI.skin.label.CalcSize(new GUIContent(idLabel)).x;
            var rect = GUILayoutUtility.GetLastRect();
            EditorGUI.LabelField(new Rect(rect.x + rect.width - needWidth - 100, rect.y, needWidth, rect.height), identity.Id.ToString());

            if(open)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Owner", identity.OwnerId == -1 ? "Server" : identity.OwnerId.ToString());
                EditorGUILayout.Toggle("Authority", identity.HasAuthority);

                var safeName = identity.SceneId < manager.LoadedSceneCount ? manager.GetSceneName(identity.SceneId) : "Unknown";
                EditorGUILayout.TextField("Scene", safeName);

                EditorGUILayout.ObjectField("Prefab", _GetPrefab(manager, identity), typeof(GameObject), false);

                EditorGUI.EndDisabledGroup();

                EditorGUILayout.BeginVertical("Box");
                foldout.behaviours = _DrawHeader(null, "Behaviours", simple, () => foldout.behaviours,
                    value => foldout.behaviours = value);
                
                if (foldout.behaviours)
                {
                    var index = 0;
                    foreach (var behaviour in identity.Behaviours)
                    {
                        EditorGUILayout.BeginVertical("Box");
                        var i = index;
                        var index1 = index;
                        if (_DrawHeader(null, behaviour.GetType().Name, simple,
                                () => foldout.behavioursFoldouts.GetValueOrDefault(index1, false),
                                value => foldout.behavioursFoldouts[i] = value))
                        {
                            //draw behaviour
                            var so = new SerializedObject(behaviour);
                            so.UpdateIfRequiredOrScript();
                            
                            var found = false;
                            
                            var iterator = so.GetIterator();
                            for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
                            {
                                if (iterator.boxedValue is NetworkValue)
                                {
                                    found = true;
                                    EditorGUILayout.PropertyField(iterator, true);
                                }
                            }
                            
                            if (!found)
                                EditorGUILayout.HelpBox("No network values found!", MessageType.Warning);
                            
                            so.ApplyModifiedProperties();
                        }
                        
                        EditorGUILayout.EndVertical();
                        index++;
                    }
                }
                
                EditorGUILayout.EndVertical();
                
                if (identity.HasAuthority)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    if (GUILayout.Button("Despawn"))
                    {
                        identity.Despawn();
                    }

                    if (GUILayout.Button("Toggle Active"))
                    {
                        identity.SetActive(!identity.gameObject.activeSelf);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            GUILayout.EndVertical();
        }
        
        private bool _DrawHeader(object selectable, string header, bool simple, Func<bool> getState, Action<bool> setState)
        {
            if (!simple)
            {
                EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
                return true;
            }
            
            var rect = GUILayoutUtility.GetRect(new GUIContent(header), EditorStyles.foldoutHeader);
            var canSelect = selectable is NetworkIdentity;
            var headerRect = new Rect(rect.x, rect.y, canSelect ? rect.width - 100 : rect.width, EditorGUIUtility.singleLineHeight);
            
            var open = getState();
            var newOpen = EditorGUI.BeginFoldoutHeaderGroup(headerRect, open, header);
            EditorGUI.EndFoldoutHeaderGroup();
            if (newOpen != open)
                setState(newOpen);
            
            if (canSelect)
            {
                var selectRect = new Rect(rect.x + rect.width - 100, rect.y, 100, EditorGUIUtility.singleLineHeight);

                if (GUI.Button(selectRect, "Select"))
                {
                    var gameObject = ((NetworkIdentity)selectable).gameObject;
                    Selection.activeObject = gameObject;
                    EditorGUIUtility.PingObject(gameObject);
                    
                    Undo.RecordObject(this, "Select Object");
                    filter.Clear();
                    filter.search = ((NetworkIdentity)selectable).Id.ToString();
                    
                    Repaint();
                }
            }
            
            return newOpen;
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
                    EditorGUILayout.LabelField(label,value.ToString());
                    break;
            }
        }
        
        /*
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
        */
    }
    #endif
}