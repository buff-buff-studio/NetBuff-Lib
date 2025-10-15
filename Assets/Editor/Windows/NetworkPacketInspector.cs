using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            public List<string> types = new();
        }

        [Serializable]
        public class PacketData
        {
            public int client;
            
            [SerializeReference]
            public IPacket packet;
        }

        public enum PacketTabs
        {
            Client,
            Server
        }
        
        public EditorGUISplitView verticalSplitView = new(EditorGUISplitView.Direction.Vertical);
        
        public Filter filter = new();
        public Vector2 scrollPackets;
        public int limit = 250;
        public bool startRecordingAutomatically = true;
        public PacketTabs tab = PacketTabs.Server;
        public PacketInspectorPriority minPacketPriority = PacketInspectorPriority.Normal;
        
        public List<PacketData> serverReceivedPackets = new();
        public List<PacketData> clientReceivedPackets = new();
        public bool recording;
        public InspectorUtilities.FoldStateHolder foldouts = new();
        
        private Texture2D _icon;
    
        [MenuItem("NetBuff/Network Packet Inspector")]
        [MenuItem("Window/NetBuff/Network Packet Inspector")]
        public static void ShowWindow()
        {
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var window = GetWindow<NetworkPacketInspector>(null, false, inspectorType);
            window.Show();
        }

        private void OnEnable()
        {
            _icon = EditorGUIUtility.Load("Assets/NetBuff/Editor/Icons/NetworkPacketInspector.png") as Texture2D;
            EditorApplication.playModeStateChanged += _OnPlayModeStateChanged;
            
            var windowIcon = EditorGUIUtility.Load("Assets/NetBuff/Editor/Icons/WindowNetworkPacketInspector.png") as Texture2D;
            titleContent = new GUIContent("Network Packet Inspector", windowIcon);
        }
        
        private void OnGUI()
        {
            _Update();
            
            verticalSplitView.BeginSplitView();
            _DrawSettings();
            verticalSplitView.Split();
            _DrawControls();
            _DrawPacketList();
            verticalSplitView.EndSplitView();
        }

        private void _DrawSettings()
        {
            #region Header
            if (_icon != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(_icon, GUILayout.Width(50), GUILayout.Height(50));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Network Packet Inspector", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(20));
            GUILayout.EndHorizontal();
            #endregion
            
            EditorGUI.BeginDisabledGroup(recording);

            #region General Settings
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);
            minPacketPriority = (PacketInspectorPriority)EditorGUILayout.EnumPopup("Min Packet Priority", minPacketPriority);
            limit = EditorGUILayout.IntField("Limit", limit);
            startRecordingAutomatically = EditorGUILayout.Toggle("Auto Start Recording", startRecordingAutomatically);
            EditorGUILayout.EndVertical();
            #endregion
            
            #region Filter Settings
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Filter Settings", EditorStyles.boldLabel);
            filter.recordClientSide = EditorGUILayout.Toggle("Record Client Side", filter.recordClientSide);
            EditorGUILayout.BeginHorizontal();
            filter.recordServerSide = EditorGUILayout.Toggle("Record Server Side", filter.recordServerSide);
            filter.recordServerSideFilter = EditorGUILayout.IntField("Server Client Filter", filter.recordServerSideFilter);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            #endregion
            
            #region Packet Type Filter
            EditorGUILayout.BeginVertical("box");
            if (filter.types.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Packet Type Filter", EditorStyles.boldLabel);
                if (GUILayout.Button("+", GUILayout.Width(20)))
                    _OpenAddTypeContext();
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginVertical();
                foreach (var type in filter.types)
                    if (!_DrawBadge(type))
                        break;
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.LabelField("Packet Type Filter", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("No Packet Types Selected", EditorStyles.centeredGreyMiniLabel);
                
                if (GUILayout.Button("Add Packet Type Filter"))
                    _OpenAddTypeContext();
            }
            EditorGUILayout.EndVertical();
            #endregion
            
            EditorGUI.EndDisabledGroup();
        }

        private bool _DrawBadge(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null)
                return true;
            var needWidth = EditorStyles.helpBox.CalcSize(new GUIContent(type.Name)).x + 20;
            EditorGUILayout.LabelField(type.Name, EditorStyles.helpBox, GUILayout.Width(needWidth));
            var lastRect = GUILayoutUtility.GetLastRect();
            var rect = new Rect(lastRect.x + needWidth - 20, lastRect.y, 20, lastRect.height);

            if (!GUI.Button(rect, "-", EditorStyles.miniButton))
                return true;
            
            Undo.RecordObject(this, "Remove Type");
            filter.types.Remove(typeName);
            Repaint();
            return false;
        }
        
        private void _OpenAddTypeContext()
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

        private void _DrawPacketList()
        {
            EditorGUILayout.BeginVertical("box");
            var centerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            EditorGUILayout.LabelField("Received Packets", centerStyle);
            EditorGUILayout.BeginHorizontal();
            tab = (PacketTabs)GUILayout.Toolbar((int)tab, Enum.GetNames(typeof(PacketTabs)));
            EditorGUILayout.EndHorizontal();
            
            if (tab == PacketTabs.Client)
                _DrawPacketList(clientReceivedPackets, true);
            else
                _DrawPacketList(serverReceivedPackets, false);
            
            EditorGUILayout.EndVertical();
        }

        private void _DrawPacketList(List<PacketData> packets, bool hideOrigin)
        {
            EditorGUILayout.LabelField("Packet List", EditorStyles.boldLabel);
            scrollPackets = EditorGUILayout.BeginScrollView(scrollPackets);  
            EditorGUILayout.BeginVertical();
            
            if (packets.Count == 0)
            {
                EditorGUILayout.LabelField("No Packets", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
                return;
            }

            var i = 0;
            foreach (var t in packets)
                _DrawPacket(t, hideOrigin, i++);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void _DrawPacket(PacketData data, bool hideOrigin, int index)
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
            
            var label = hideOrigin ? data.packet.GetType().Name : $"{data.packet.GetType().Name} (From {data.client})";
            
            InspectorUtilities.DrawObject($"{index}", label, data.packet, foldouts);
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
            if (filter.recordServerSideFilter != -1 && arg1 != filter.recordServerSideFilter)
                return;
            
            if (!ApplyTypeFilterForPacket(arg2))
                return;
            
            serverReceivedPackets.Add(new PacketData
            {
                client = arg1,
                packet = arg2
            });
            
            if(serverReceivedPackets.Count > limit)
                serverReceivedPackets.RemoveAt(0);

            Repaint();
        }

        private void OnClientPacketReceived(IPacket obj)
        {
            if (!ApplyTypeFilterForPacket(obj))
                return;
            
            clientReceivedPackets.Add(new PacketData
            {
                client = -1,
                packet = obj
            });
            
            if(clientReceivedPackets.Count > limit)
                clientReceivedPackets.RemoveAt(0);

            Repaint();
        }

        private bool ApplyTypeFilterForPacket(IPacket packet)
        {
            if (packet == null)
                return false; 
            
            var attr = packet.GetType().GetCustomAttribute<PacketInspectorPriorityAttribute>();
            var priority = attr?.Priority ?? PacketInspectorPriority.Normal;
            if (priority < minPacketPriority)
                return false;
            
            return filter.types.Count == 0 || filter.types.Contains(packet.GetType().AssemblyQualifiedName);
        }
        
        private void _OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && startRecordingAutomatically)
                StartRecording();
        }
    }
    #endif
}