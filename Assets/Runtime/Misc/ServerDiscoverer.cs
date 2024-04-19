using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using LiteNetLib.Utils;
using UnityEngine;

namespace NetBuff.Misc
{
    public static class ServerDiscoverer
    {
        public class GameInfo
        {
            public string Name { get; set; }
            public int Players { get; set; }
            public int MaxPlayers { get; set; }
            public Platform Platform { get; set; }
            public bool HasPassword { get; set; }

            public override string ToString()
            {
                return $"{Name}'s game - Players: {Players}/{MaxPlayers}, Platform: {Platform}, HasPassword: {HasPassword}";
            }
        }
        public class EthernetGameInfo : GameInfo
        {
            public IPAddress Address { get; set; }

            public override string ToString()
            {
                return
                    $"Address: {Address}, Players: {Players}/{MaxPlayers}, Platform: {Platform}, HasPassword: {HasPassword}";
            }
        }
        
        public static async void FindServers(int port, Action<GameInfo> foundServer, Action finalized)
        {
            var waiting = 0;

            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var adapter in interfaces)
            {
                if (adapter.IsReceiveOnly)
                    continue;

                var properties = adapter.GetIPProperties();

                foreach (var uip in properties.UnicastAddresses)
                {
                    if (uip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var address = uip.Address;

#pragma warning disable CS4014
                        Task.Run(async () =>
#pragma warning restore CS4014
                        {
                            waiting++;
                            
                            try
                            {
                                //try udp the address
                                var udpClient = new UdpClient();
                                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket,
                                    SocketOptionName.ReuseAddress,
                                    true);
                                udpClient.Client.Bind(new IPEndPoint(address, 0));
                                udpClient.EnableBroadcast = true;
                                udpClient.Client.ReceiveTimeout = 1000;
                                var writer = new NetDataWriter();
                                writer.Put((byte)8);
                                writer.Put("server_search");
                                var data = writer.CopyData();
                                await udpClient.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, port));

                                var address2 = new IPEndPoint(IPAddress.Any, port);
                                var response = udpClient.Receive(ref address2);
                                udpClient.Close();
                                var reader = new NetDataReader(response);
                                reader.GetByte(); //just discard it

                                if (reader.GetString(50) == "server_answer")
                                {
                                    var name = reader.GetString(50);
                                    var players = reader.GetInt();
                                    var maxPlayers = reader.GetInt();
                                    var platform = (Platform)reader.GetInt();
                                    var hasPassword = reader.GetBool();

                                    foundServer(new EthernetGameInfo
                                    {
                                        Name = name,
                                        Address = address2.Address,
                                        Players = players,
                                        MaxPlayers = maxPlayers,
                                        Platform = platform,
                                        HasPassword = hasPassword
                                    });
                                }
                            }
                            catch
                            {
                                // ignored
                            }

                            waiting--;
                        });
                    }
                }
            }

            while (waiting > 0)
                await Task.Delay(100);
        }
    }
}
