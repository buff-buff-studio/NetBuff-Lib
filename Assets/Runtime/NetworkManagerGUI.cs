using System;
using System.Collections.Generic;
using NetBuff.Discover;
using NetBuff.Misc;
using NetBuff.UDP;
using UnityEngine;

namespace NetBuff
{
    [RequireComponent(typeof(NetworkManager))]
    [Icon("Assets/Editor/Icons/NetworkManagerGUI.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-manager-gui")]
    public class NetworkManagerGUI : MonoBehaviour
    {
        public enum CurrentGraph
        {
            None,
            FPS,
            Latency,
            PacketSent,
            PacketReceived,
            PacketLoss
        }


        private ServerDiscover.GameInfo[] _serverList;

        private void OnEnable()
        {
            _fpsData.Clear();
            GraphPlottingRate = graphPlottingRate;
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(_UpdateGraphs));
        }

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
                    if (GUILayout.Button("Start Server")) NetworkManager.Instance.StartServer();
                    if (GUILayout.Button("Start Client")) NetworkManager.Instance.StartClient();
                    if (GUILayout.Button("Start Host")) NetworkManager.Instance.StartHost();
                    break;

                case NetworkTransport.EndType.Server:
                    DrawServerStatus();
                    DrawAddressAndPort();
                    if (GUILayout.Button("Start Client")) NetworkManager.Instance.StartClient();
                    if (GUILayout.Button("Close")) NetworkManager.Instance.Close();

                    break;

                case NetworkTransport.EndType.Client:
                    DrawClientStatus();
                    if (GUILayout.Button("Close")) NetworkManager.Instance.Close();
                    break;
                case NetworkTransport.EndType.Host:
                    DrawServerStatus();
                    DrawClientStatus();
                    if (GUILayout.Button("Close")) NetworkManager.Instance.Close();
                    break;
            }

            GUILayout.EndArea();

            if (NetworkManager.Instance.EndType == NetworkTransport.EndType.None)
            {
                GUILayout.BeginArea(new Rect(220, 10, 350, 400));
                DrawServerList();
                GUILayout.EndArea();
            }

            _DrawGraphs();
        }

        private void OnValidate()
        {
            GraphPlottingRate = graphPlottingRate;

            _fpsData.Clear();
            _fpsData.Max = 0;

            _plotLatency.Clear();

            _plotPacketSent.Clear();
            _plotPacketSent.Max = 0;

            _plotPacketReceived.Clear();
            _plotPacketReceived.Max = 0;

            _plotPacketLoss.Clear();
            _plotPacketLoss.Max = 0;
        }

        private void _UpdateGraphs()
        {
            if (plotLatency)
            {
                var info = NetworkManager.Instance.ClientConnectionInfo;
                _plotLatency.AddData(info?.Latency ?? 0, false);
            }

            if (plotPacketRate)
            {
                var info = NetworkManager.Instance.ClientConnectionInfo;

                var packetSent = info?.PacketSent ?? 0;
                var packetReceived = info?.PacketReceived ?? 0;
                var packetLoss = info?.PacketLossPercentage ?? 0;

                _plotPacketSent.AddData(packetSent - _lastPacketSent);
                _plotPacketReceived.AddData(packetReceived - _lastPacketReceived);
                _plotPacketLoss.AddData(packetLoss - _lastPacketLoss);
                _plotPacketSent.Max = _plotPacketReceived.Max = Mathf.Max(_plotPacketSent.Max, _plotPacketReceived.Max);

                _lastPacketSent = packetSent;
                _lastPacketReceived = packetReceived;
                _lastPacketLoss = packetLoss;
            }

            if (plotFPS)
                _fpsData.AddData(1f / Time.deltaTime);
        }

        private void _DrawGraphs()
        {
            var w = Screen.width;
            var h = Screen.height;

            switch (currentGraph)
            {
                case CurrentGraph.FPS:
                    GraphPlotter.DrawGraph(0, h, 2, Color.green, w, _fpsData.Data, _fpsData.Max, h / 4f);
                    break;
                case CurrentGraph.Latency:
                    if (NetworkManager.Instance.EndType == NetworkTransport.EndType.None)
                        return;
                    GraphPlotter.DrawGraph(0, h, 2, Color.red, w, _plotLatency.Data, _plotLatency.Max, h / 4f);
                    break;
                case CurrentGraph.PacketSent:
                    if (NetworkManager.Instance.EndType == NetworkTransport.EndType.None)
                        return;
                    GraphPlotter.DrawGraph(0, h, 2, Color.blue, w, _plotPacketSent.Data, _plotPacketSent.Max, h / 4f);
                    break;
                case CurrentGraph.PacketReceived:
                    if (NetworkManager.Instance.EndType == NetworkTransport.EndType.None)
                        return;
                    GraphPlotter.DrawGraph(0, h, 2, Color.yellow, w, _plotPacketReceived.Data, _plotPacketReceived.Max,
                        h / 2f);
                    break;
                case CurrentGraph.PacketLoss:
                    if (NetworkManager.Instance.EndType == NetworkTransport.EndType.None)
                        return;
                    GraphPlotter.DrawGraph(0, h, 2, Color.magenta, w, _plotPacketLoss.Data, _plotPacketLoss.Max,
                        h / 2f);
                    break;
            }
        }

        private void DrawAddressAndPort()
        {
            if (NetworkManager.Instance.Transport is UDPNetworkTransport udp)
            {
                udp.Address = GUILayout.TextField(udp.Address);
                var s = GUILayout.TextField(udp.Port.ToString());
                if (int.TryParse(s, out var port)) udp.Port = port;
            }
        }

        private void DrawClientStatus()
        {
            var info = NetworkManager.Instance.ClientConnectionInfo;
            if (info == null)
                return;
            GUILayout.Label(
                $"Latency: {info.Latency}ms\nOut: {info.PacketSent}\nIn: {info.PacketReceived}\nLost: {info.PacketLossPercentage}%");
        }

        private void DrawServerStatus()
        {
            GUILayout.Label($"Clients: {NetworkManager.Instance.Transport.GetClientCount()}");
        }

        private void DrawServerList()
        {
            if (_serverList == null)
            {
                _serverList = Array.Empty<ServerDiscover.GameInfo>();
                var list = new List<ServerDiscover.GameInfo>();

                var discoverer = NetworkManager.Instance.Transport.GetServerDiscoverer();
                discoverer?.Search(info =>
                {
                    list.Add(info);
                    _serverList = list.ToArray();
                }, () => { });
            }

            foreach (var info in _serverList)
                if (GUILayout.Button(info.ToString()))
                    info.Join();

            if (GUILayout.Button("Refresh Server List")) _serverList = null;
        }

        #region Inspector Fields
        [SerializeField]
        private int graphPlottingRate = 10;

        [SerializeField]
        private bool plotFPS;

        [SerializeField]
        private bool plotLatency;

        [SerializeField]
        private bool plotPacketRate;

        [SerializeField]
        private CurrentGraph currentGraph = CurrentGraph.None;
        #endregion

        #region Internal Fields
        private readonly GraphPlotter.GraphPlotterData _fpsData = new();
        private readonly GraphPlotter.GraphPlotterData _plotLatency = new() { Max = 250 };
        private readonly GraphPlotter.GraphPlotterData _plotPacketSent = new() { Max = 0 };
        private readonly GraphPlotter.GraphPlotterData _plotPacketReceived = new() { Max = 0 };
        private readonly GraphPlotter.GraphPlotterData _plotPacketLoss = new() { Max = 0 };
        private long _lastPacketSent;
        private long _lastPacketReceived;
        private long _lastPacketLoss;
        #endregion

        #region Helper Properties
        private int GraphPlottingRate
        {
            get => graphPlottingRate;
            set
            {
                graphPlottingRate = Mathf.Max(1, value);

                if (!Application.isPlaying)
                    return;

                CancelInvoke(nameof(_UpdateGraphs));
                InvokeRepeating(nameof(_UpdateGraphs), 0, 1f / graphPlottingRate);
            }
        }

        public bool PlotFPS
        {
            get => plotFPS;
            set
            {
                plotFPS = value;
                OnValidate();
            }
        }

        public bool PlotLatency
        {
            get => plotLatency;
            set
            {
                plotLatency = value;
                OnValidate();
            }
        }

        public bool PlotPacketRate
        {
            get => plotPacketRate;
            set
            {
                plotPacketRate = value;
                OnValidate();
            }
        }

        public CurrentGraph CurrentGraphType
        {
            get => currentGraph;
            set
            {
                currentGraph = value;
                OnValidate();
            }
        }
        #endregion
    }
}