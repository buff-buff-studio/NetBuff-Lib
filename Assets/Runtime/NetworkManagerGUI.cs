using System;
using System.Collections.Generic;
using NetBuff.Misc;
using NetBuff.UDP;
using UnityEngine;

namespace NetBuff
{
    /// <summary>
    /// Debug GUI for NetworkManager
    /// </summary>
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
                GUILayout.BeginArea(new Rect(220, 10, 300, 400));
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

        
        private ServerDiscoverer.GameInfo[] _serverList;

        private void DrawServerList()
        {
            if (_serverList == null)
            {
                _serverList = Array.Empty<ServerDiscoverer.GameInfo>();
                var list = new List<ServerDiscoverer.GameInfo>();
                
                if (NetworkManager.Instance.transport is UDPNetworkTransport udp)
                {
                    ServerDiscoverer.FindServers(udp.port, (info) =>
                    {
                        list.Add(info);
                        _serverList = list.ToArray();
                    }, () => {});
                }
            }

            if (NetworkManager.Instance.transport is UDPNetworkTransport transport)
            {
                foreach (var info in _serverList)
                {
                    if (info is ServerDiscoverer.EthernetGameInfo egi)
                    {
                        if (GUILayout.Button($"{egi.Address} - {egi.Players}/{egi.MaxPlayers} - {egi.Platform}"))
                        {
                            transport.address = egi.Address.ToString();
                            NetworkManager.Instance.StartClient();
                        }
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