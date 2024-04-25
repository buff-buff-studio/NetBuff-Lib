using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetBuff.Discover;
using NetBuff.Misc;

namespace NetBuff.UDP
{
    /// <summary>
    ///     Used to find all available servers using UDP.
    ///     Searches through all available network interfaces and sends a broadcast message to find servers.
    /// </summary>
    public class UDPServerDiscoverer : ServerDiscoverer<UDPServerDiscoverer.UDPServerInfo>
    {
        public UDPServerDiscoverer(int magicNumber, int port)
        {
            _magicNumber = magicNumber;
            _port = port;
        }

        public override async void Search(Action<UDPServerInfo> onFindServer, Action onFinish)
        {
            var id = ++_searchId;
            var waiting = 0;

            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var adapter in interfaces)
            {
                if (adapter.IsReceiveOnly)
                    continue;

                var properties = adapter.GetIPProperties();

                foreach (var uip in properties.UnicastAddresses)
                {
                    if (id != _searchId)
                        return;

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
                                var udpClient = new UdpClient();
                                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket,
                                    SocketOptionName.ReuseAddress,
                                    true);
                                udpClient.Client.Bind(new IPEndPoint(address, 0));
                                udpClient.EnableBroadcast = true;
                                udpClient.Client.ReceiveTimeout = 1000;
                                var writer = new BinaryWriter(new MemoryStream());
                                writer.Write((byte)7);
                                writer.Write("server_search");
                                writer.Write(_magicNumber);

                                var data = ((MemoryStream)writer.BaseStream).ToArray();
                                await udpClient.SendAsync(data, data.Length,
                                    new IPEndPoint(IPAddress.Broadcast, _port));

                                var address2 = new IPEndPoint(IPAddress.Any, _port);
                                var response = udpClient.Receive(ref address2);
                                udpClient.Close();
                                var reader = new BinaryReader(new MemoryStream(response));

                                if (reader.ReadString() == "server_answer")
                                {
                                    var name = reader.ReadString();
                                    var players = reader.ReadInt32();
                                    var maxPlayers = reader.ReadInt32();
                                    var platform = (Platform)reader.ReadInt32();
                                    var hasPassword = reader.ReadBoolean();

                                    if (id != _searchId)
                                        return;
                                    onFindServer(new UDPServerInfo
                                    {
                                        Name = name,
                                        Address = address2.Address,
                                        Players = players,
                                        MaxPlayers = maxPlayers,
                                        Platform = platform,
                                        HasPassword = hasPassword,
                                        Method = "UDP"
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

            if (id == _searchId)
                onFinish?.Invoke();
        }

        public override void Cancel()
        {
            _searchId++;
        }

        /// <summary>
        ///     Holds the information about a UDP server.
        /// </summary>
        public class UDPServerInfo : ServerInfo
        {
            /// <summary>
            ///     The server's IP address.
            /// </summary>
            public IPAddress Address { get; set; }

            public override string ToString()
            {
                return
                    $"{Name}'s game ({Address}) - {Players}/{MaxPlayers} {Platform} {(HasPassword ? "[Password]" : "")}";
            }

            public override bool Join()
            {
                var transport = NetworkManager.Instance.Transport;
                var udp = transport as UDPNetworkTransport;
                if (udp == null)
                    throw new Exception("Transport is not NetLibUDP");
                udp.Address = Address.ToString();
                NetworkManager.Instance.StartClient();
                return true;
            }
        }

        #region Internal Fields
        private readonly int _magicNumber;
        private readonly int _port;
        private int _searchId;
        #endregion
    }
}