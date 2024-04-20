using System;
using System.Collections.Generic;
using NetBuff.Discover;
using NetBuff.Misc;
using NetBuff.UDP;
using UnityEngine;

namespace NetBuff
{
    /// <summary>
    /// Debug GUI for NetworkManager
    /// </summary>
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

            if (NetworkManager.Instance.transport == null)
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
            if (NetworkManager.Instance.transport is UDPNetworkTransport udp)
            {
                udp.address = GUILayout.TextField(udp.address);
                var s = GUILayout.TextField(udp.port.ToString());
                if (int.TryParse(s, out var port))
                {
                    udp.port = port;
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
            GUILayout.Label($"Clients: {NetworkManager.Instance.transport.GetClientCount()}");
        }

        
        private UDPServerDiscoverer.UDPGameInfo[] _serverList;

        private void DrawServerList()
        {
            if (_serverList == null)
            {
                _serverList = Array.Empty<UDPServerDiscoverer.UDPGameInfo>();
                var list = new List<UDPServerDiscoverer.UDPGameInfo>();
                
                if (NetworkManager.Instance.transport is UDPNetworkTransport udp)
                {
                    var discoverer = new UDPServerDiscoverer(NetworkManager.Instance.versionMagicNumber, udp.port);
                    discoverer.Search((info) =>
                    {
                        list.Add(info);
                        _serverList = list.ToArray();
                    }, () => { });
                }
            }

            if (NetworkManager.Instance.transport is UDPNetworkTransport transport)
            {
                foreach (var info in _serverList)
                {
                    if (GUILayout.Button($"{info.Address} - {info.Players}/{info.MaxPlayers} - {info.Platform} [{info.HasPassword}]"))
                    {
                        transport.address = info.Address.ToString();
                        NetworkManager.Instance.StartClient();
                    }
                }
            }
            //GameServerLocator
            
            if (GUILayout.Button("Refresh Server List"))
            {
                _serverList = null;
            }
        }
    }
}