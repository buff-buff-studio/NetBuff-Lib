using System;
using System.Collections.Generic;
using System.Linq;
using NetBuff.Interface;
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
        public PacketTabs tab = PacketTabs.Server;
        
        public List<PacketData> serverReceivedPackets = new();
        public List<PacketData> clientReceivedPackets = new();
        public bool recording;
        public InspectorUtilities.FoldStateHolder foldouts = new();
    
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
            
            var label = hideOrigin ? data.packet.GetType().Name : $"{data.packet.GetType().Name} (From: {data.client})";
            
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
    }
    #endif
}