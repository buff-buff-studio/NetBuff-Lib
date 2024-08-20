using UnityEngine;

namespace NetBuff.Relays
{
    /// <summary>
    ///     RelayNetworkManagerGUI is a simple GUI for RelayNetworkManager.
    ///     Provides a simple way to start a server, client, or host.
    ///     Also provides a way to see the server list and connect to them.
    ///     Can plot graphs for FPS, Latency, Packet Sent, Packet Received, and Packet Loss.
    ///     Displays the current status of the server and client.
    ///     Provides controls to create and join a room.
    /// </summary>
    [Icon("Assets/Editor/Icons/UDPNetworkTransport.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/subpackages/relay")]
    [RequireComponent(typeof(RelayNetworkManager))]
    public class RelayNetworkManagerGUI : NetworkManagerGUI
    {
        public string code = "";
        
        protected override void OnGUI()
        {
            base.OnGUI();

            GUILayout.BeginArea(new Rect(10, 85, 200, 200));

            if (NetworkManager.Instance is RelayNetworkManager rnm)
            {
                if (rnm.Transport.Type == NetworkTransport.EnvironmentType.None)
                {
                    GUILayout.Label("Relay Room: ");
                    code = GUILayout.TextField(code);

                    if (GUILayout.Button("Join"))
                    {
                        rnm.JoinRelayServer(code, (success) => { Debug.Log("Success: " + success); });
                    }

                    if (GUILayout.Button("Create"))
                    {
                        rnm.StartRelayHost(4, "", (success, c) =>
                        {
                            code = c;
                            Debug.Log("Success: " + success + " Code: " + code);
                        });
                    }
                }
            }

            GUILayout.EndArea();
        }
    }
}