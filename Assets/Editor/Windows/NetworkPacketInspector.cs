using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AYellowpaper.SerializedCollections;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEditor;
using UnityEngine;

namespace NetBuff.Editor.Windows
{
    #if UNITY_EDITOR
    public class NetworkPacketInspector : EditorWindow
    {
        [Serializable]
        public class Filter
        {
            public bool recordClientSide = true;
            public bool recordServerSide = true;
            public int recordServerSideFilter = -1;
            public List<string> types = new List<string>();
        }

        [Serializable]
        public class PacketData
        {
            public int client;
            [SerializeReference]
            public IPacket packet;
            public bool foldout;
        }

        public enum PacketTabs
        {
            Client,
            Server
        }
        
        public Filter filter = new();
        public Vector2 scrollFilter;
        public Vector2 scrollPackets;
        public int limit = 100;
        public PacketTabs tab = PacketTabs.Client;
        
        public List<PacketData> serverReceivedPackets = new();
        public List<PacketData> clientReceivedPackets = new();
        public bool recording;
        public SerializedDictionary<int, bool> foldouts = new();
        private int _currentFoldout = -1;


        [MenuItem("NetBuff/Network Packet Inspector")]
        [MenuItem("Window/Network/Packet Inspector")]
        public static void ShowWindow()
        {
            GetWindow<NetworkPacketInspector>("Network Packet Inspector");
        }

        private void OnGUI()
        {
            _Update();
            
            _DrawFilter();
            _DrawControls();
            _DrawPackets();
        }

        private void _DrawFilter()
        {
            EditorGUI.BeginDisabledGroup(recording);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Filter", EditorStyles.boldLabel);
            
            limit = EditorGUILayout.IntField("Limit", limit);
            bool DrawBadge(string typeName)
            {
                var type = Type.GetType(typeName);
                if (type == null)
                    return true;
                var needWidth = EditorStyles.helpBox.CalcSize(new GUIContent(type.Name)).x + 20;
                EditorGUILayout.LabelField(type.Name, EditorStyles.helpBox, GUILayout.Width(needWidth));
                var lastRect = GUILayoutUtility.GetLastRect();
                var rect = new Rect(lastRect.x + needWidth - 20, lastRect.y, 20, lastRect.height);
                
                if (GUI.Button(rect, "X"))
                {
                    Undo.RecordObject(this, "Remove Type");
                    filter.types.Remove(typeName);
                    Repaint();
                    return false;
                }
                
                return true;
            }
            
            filter.recordClientSide = EditorGUILayout.Toggle("Record Client Side", filter.recordClientSide);
            EditorGUILayout.BeginHorizontal();
            filter.recordServerSide = EditorGUILayout.Toggle("Record Server Side", filter.recordServerSide);
            filter.recordServerSideFilter = EditorGUILayout.IntField("Server Client Filter", filter.recordServerSideFilter);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Packet Types");
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                var menu = new GenericMenu();
                var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => t.GetInterfaces().Contains(typeof(IPacket)) && !t.IsAbstract));
                
                foreach (var type in types)
                {
                    if (filter.types.Contains(type.AssemblyQualifiedName))
                        continue;
                    
                    var type1 = type;
                    menu.AddItem(new GUIContent(type.Name), false, () =>
                    {
                        Undo.RecordObject(this, "Add Type");
                        filter.types.Add(type1.AssemblyQualifiedName);
                        Repaint();
                    });
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            if (filter.types.Count > 0)
            {
                scrollFilter = EditorGUILayout.BeginScrollView(scrollFilter,true, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.scrollView, GUILayout.Height(40));
                scrollFilter.y = 0;
                
                EditorGUILayout.BeginHorizontal();
                foreach (var type in filter.types)
                    if (!DrawBadge(type))
                        break;

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
            EditorGUI.EndDisabledGroup();
        }

        private void _DrawControls()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            var recordingColor = recording ? Color.red : Color.white;
            GUI.color = recordingColor;
            if (GUILayout.Button(EditorGUIUtility.IconContent("Animation.Record@2x"), GUILayout.Width(30), GUILayout.Height(30)))
            {
                if (recording)
                    StopRecording();
                else
                    StartRecording();
            }
            GUI.color = Color.white;
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
            };
            EditorGUILayout.LabelField(recording ? "Stop Recording" : "Start Recording", style, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            if(GUILayout.Button(new GUIContent("Clear"), GUILayout.Height(30)))
            {
                serverReceivedPackets.Clear();
                clientReceivedPackets.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void _DrawPackets()
        {
            EditorGUILayout.BeginVertical("box");
            var centerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            EditorGUILayout.LabelField("Current Tab", centerStyle);
            EditorGUILayout.BeginHorizontal();
            tab = (PacketTabs)GUILayout.Toolbar((int)tab, Enum.GetNames(typeof(PacketTabs)));
            EditorGUILayout.EndHorizontal();

            _currentFoldout = 0;
            if (tab == PacketTabs.Client)
                _DrawPacketList(clientReceivedPackets, true);
            else
                _DrawPacketList(serverReceivedPackets, false);
            
            EditorGUILayout.EndVertical();
        }

        private void _DrawPacketList(List<PacketData> packets, bool hideOrigin)
        {
            EditorGUILayout.LabelField("Packets", EditorStyles.boldLabel);
            scrollPackets = EditorGUILayout.BeginScrollView(scrollPackets);  
            EditorGUILayout.BeginVertical();
            
            if (packets.Count == 0)
            {
                EditorGUILayout.LabelField("No Packets", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
                return;
            }
            
            foreach (var t in packets)
                _DrawPacket(t, hideOrigin);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void _DrawPacket(PacketData data, bool hideOrigin)
        {
            if (!hideOrigin && filter.recordServerSideFilter != -1 && data.client != filter.recordServerSideFilter)
                return;
            
            if(!ApplyTypeFilterForPacket(data.packet))
                return;

            if (data.packet == null)
            {
                EditorGUILayout.LabelField("Null Packet", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            
            var label = hideOrigin ? data.packet.GetType().Name : $"{data.packet.GetType().Name} (From: {data.client})";
            
            EditorGUILayout.BeginHorizontal();
            data.foldout = EditorGUILayout.BeginFoldoutHeaderGroup(data.foldout, label);
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndHorizontal();

            if (data.foldout)
            {
                EditorGUILayout.BeginVertical("box");
                _DrawObjectReadOnly(data.packet);
                EditorGUILayout.EndVertical();
            }
        }

        private void _Update()
        {
            var manager = NetworkManager.Instance;
            if (manager == null || manager.Transport == null)
            {
                StopRecording();
                return;
            }
            
            if (!recording)
                return;
            
            manager.Transport.OnClientPacketReceived -= OnClientPacketReceived;
            manager.Transport.OnServerPacketReceived -= OnServerPacketReceived;
            
            if (filter.recordClientSide)
                manager.Transport.OnClientPacketReceived += OnClientPacketReceived;
            if (filter.recordServerSide)
                manager.Transport.OnServerPacketReceived += OnServerPacketReceived;
        }
        
        public void StartRecording()
        {
            if (recording)
                return;

            var manager = NetworkManager.Instance;
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Error", "Network Manager not found", "Ok");
                return;
            }
            
            if (manager.Transport == null)
            {
                EditorUtility.DisplayDialog("Error", "Transport not found", "Ok");
                return;
            }
            
            recording = true;
            Repaint();
        }
        
        public void StopRecording()
        {
            if (!recording)
                return;

            recording = false;
            
            var manager = NetworkManager.Instance;

            if (manager != null && manager.Transport != null)
            {
                manager.Transport.OnClientPacketReceived -= OnClientPacketReceived;
                manager.Transport.OnServerPacketReceived -= OnServerPacketReceived;
            }
            Repaint();
        }

        private void OnServerPacketReceived(int arg1, IPacket arg2)
        {
            if(serverReceivedPackets.Count() >= limit)
                serverReceivedPackets.RemoveAt(0);
            
            if (filter.recordServerSideFilter != -1 && arg1 != filter.recordServerSideFilter)
                return;
            
            if (!ApplyTypeFilterForPacket(arg2))
                return;
            
            serverReceivedPackets.Add(new PacketData
            {
                client = arg1,
                packet = arg2
            });
            
            Repaint();
        }

        private void OnClientPacketReceived(IPacket obj)
        {
            if(clientReceivedPackets.Count() >= limit)
                clientReceivedPackets.RemoveAt(0);
            
            if (!ApplyTypeFilterForPacket(obj))
                return;
            
            clientReceivedPackets.Add(new PacketData
            {
                client = -1,
                packet = obj
            });
            
            Repaint();
        }

        private bool ApplyTypeFilterForPacket(IPacket packet)
        {
            return filter.types.Count == 0 || filter.types.Contains(packet.GetType().AssemblyQualifiedName);
        }
        
        private void _DrawObjectReadOnly(object o)
        {
            EditorGUI.BeginDisabledGroup(true);
            var properties = o.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (properties.Length == 0)
            {
                EditorGUILayout.LabelField("No Properties", EditorStyles.centeredGreyMiniLabel);
                EditorGUI.EndDisabledGroup();
                return;
            }
            
            foreach (var property in properties)
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;
               
                _DrawObjectField(property.Name, property.GetValue(o));
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
                case short s:
                    EditorGUILayout.IntField(label, s);
                    break;
                case byte b:
                    EditorGUILayout.IntField(label, b);
                    break;
                case long l:
                    EditorGUILayout.LongField(label, l);
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
                case Vector4 v4:
                    EditorGUILayout.Vector4Field(label, v4);
                    break;
                case Quaternion q:
                    EditorGUILayout.Vector4Field(label, new Vector4(q.x, q.y, q.z, q.w));
                    break;
                case Color c:
                    EditorGUILayout.ColorField(label, c);
                    break;
                case Color32 c32:
                    EditorGUILayout.ColorField(label, c32);
                    break;
                
                case NetworkId id:
                    if (label.ToLower().Contains("prefab"))
                    {
                        var obj = GetNetworkPrefab(id);
                        EditorGUILayout.ObjectField(label, obj, typeof(GameObject), true);
                    }
                    else
                    {
                        var obj =  GetNetworkObject(id);
                        EditorGUILayout.ObjectField(label, obj, typeof(NetworkIdentity), true);
                    }
                    
                    break;
                
                case Array a:
                    {
                        var fold = foldouts[_currentFoldout] = EditorGUILayout.Foldout(foldouts.GetValueOrDefault(_currentFoldout, false), $"{label} [{a.Length}]");
                        _currentFoldout++;
                        if (fold)
                        {
                            if (a.Length == 0)
                            {
                                EditorGUILayout.LabelField("Empty", EditorStyles.centeredGreyMiniLabel);
                                break;
                            }
                            
                            EditorGUILayout.BeginVertical("box");
                            for (var i = 0; i < a.Length; i++)
                            {
                                _DrawObjectField(i.ToString(), a.GetValue(i));
                            }
                            EditorGUILayout.EndVertical();
                        }
                    }

                    break;

                default:
                    {
                        var fold = foldouts[_currentFoldout] = EditorGUILayout.Foldout(foldouts.GetValueOrDefault(_currentFoldout, false), $"{label} [{value.GetType().Name}]");
                        _currentFoldout++;
                        
                        if (fold)
                        {
                            EditorGUILayout.BeginVertical("box");
                            _DrawObjectReadOnly(value);
                            EditorGUILayout.EndVertical();
                        }
                    }
                    break;
            }
        }

        private static GameObject GetNetworkPrefab(NetworkId id)
        {
            var manager = NetworkManager.Instance;
            if (manager == null)
                return null;
            
            if (manager.PrefabRegistry == null)
                return null;
            
            return manager.PrefabRegistry.GetPrefab(id);
        }

        private static NetworkIdentity GetNetworkObject(NetworkId id)
        {
            #if UNITY_EDITOR
            if (NetworkManager.Instance == null)
            {
                foreach (var obj in UnityEngine.Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None))
                    if (obj.Id == id)
                        return obj;
                
                return null;
            }
            #endif
            
            return id == NetworkId.Empty ? null : NetworkManager.Instance.GetNetworkObject(id);
        }
    }
    #endif
}