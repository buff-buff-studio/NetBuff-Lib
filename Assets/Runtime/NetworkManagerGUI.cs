using System;
using System.Collections.Generic;
using NetBuff.Discover;
using NetBuff.UDP;
using UnityEngine;

namespace NetBuff
{
    [RequireComponent(typeof(NetworkManager))]
    [Icon("Assets/Editor/Icons/NetworkManagerGUI.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-manager-gui")]
    public class NetworkManagerGUI : MonoBehaviour
    {
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 400));
            
            if (NetworkManager.Instance == null)
            {
                GUILayout.Label("NetworkManager not found");
                GUILayout.EndArea();
                return;
            }

            if (NetworkManager.Instance.Transport == null)
            {
                GUILayout.Label("NetworkTransport not set");
                GUILayout.EndArea();
                return;
            }

            switch (NetworkManager.Instance.EndType)
            {
                case NetworkTransport.EndType.None:
                    DrawAddressAndPort();
                    if (GUILayout.Button("Start Server"))
                    {
                        NetworkManager.Instance.StartServer();
                    }
                    if (GUILayout.Button("Start Client"))
                    {
                        NetworkManager.Instance.StartClient();
                    }
                    if (GUILayout.Button("Start Host"))
                    {
                        NetworkManager.Instance.StartHost();
                    }
                    break;
                
                case NetworkTransport.EndType.Server:
                    DrawServerStatus();
                    DrawAddressAndPort();
                    if (GUILayout.Button("Start Client"))
                    {
                        NetworkManager.Instance.StartClient();
                    }
                    if (GUILayout.Button("Close"))
                    {
                        NetworkManager.Instance.Close();
                    }
                    
                    break;
                
                case NetworkTransport.EndType.Client:
                    DrawClientStatus();
                    if (GUILayout.Button("Close"))
                    {
                        NetworkManager.Instance.Close();
                    }
                    break;
                case NetworkTransport.EndType.Host:
                    DrawServerStatus();
                    DrawClientStatus();
                    if (GUILayout.Button("Close"))
                    {
                        NetworkManager.Instance.Close();
                    }
                    break;
            }
            
            GUILayout.EndArea();
            
            if (NetworkManager.Instance.EndType == NetworkTransport.EndType.None)
            {
                GUILayout.BeginArea(new Rect(220, 10, 350, 400));
                DrawServerList();
                GUILayout.EndArea();
            }
        }

        private void DrawAddressAndPort()
        {
            if (NetworkManager.Instance.Transport is UDPNetworkTransport udp)
            {
                udp.Address = GUILayout.TextField(udp.Address);
                var s = GUILayout.TextField(udp.Port.ToString());
                if (int.TryParse(s, out var port))
                {
                    udp.Port = port;
                }
            }
        }

        private void DrawClientStatus()
        {
            var info = NetworkManager.Instance.ClientConnectionInfo;
            if (info == null)
                return;
            GUILayout.Label($"Latency: {info.Latency}ms\nOut: {info.PacketSent}\nIn: {info.PacketReceived}\nLost: {info.PacketLossPercentage}%");
        }
        
        private void DrawServerStatus()
        {
            GUILayout.Label($"Clients: {NetworkManager.Instance.Transport.GetClientCount()}");
        }

        
        private ServerDiscover.GameInfo[] _serverList;

        private void DrawServerList()
        {
            if (_serverList == null)
            {
                _serverList = Array.Empty<ServerDiscover.GameInfo>();
                var list = new List<ServerDiscover.GameInfo>();

                var discoverer = NetworkManager.Instance.Transport.GetServerDiscoverer();
                discoverer?.Search((info) =>
                {
                    list.Add(info);
                    _serverList = list.ToArray();
                }, () => { });
            }

            foreach (var info in _serverList)
            {
                if (GUILayout.Button(info.ToString()))
                    info.Join();
            }

            if (GUILayout.Button("Refresh Server List"))
            {
                _serverList = null;
            }
        }
    }
}