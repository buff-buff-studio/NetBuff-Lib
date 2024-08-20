using System;

namespace NetBuff.Relays
{
    /// <summary>
    ///     Main NetBuff component that manages the network environment, network objects, network behaviours callbacks, packet
    ///     sending and receiving, session data, and scene management.
    ///     This component should be placed in the scene that will be used as the main scene for the network environment.
    ///     NetworkManager is a singleton class, meaning that only one instance of this component can exist in the scene.
    ///     Provides support for Relay Network Transport.
    /// </summary>
    public class RelayNetworkManager : NetworkManager
    {
        /// <summary>
        /// Start a relay host with the specified max players and region id.
        /// </summary>
        /// <param name="maxPlayers"></param>
        /// <param name="regionId"></param>
        /// <param name="callback"></param>
        public void StartRelayHost(int maxPlayers, string regionId, Action<bool, string> callback)
        {
            var tp = (RelayNetworkTransport)Transport;

            tp.AllocateRelayServer(maxPlayers, regionId,
                (joinCode) =>
                {
                    StartHost();
                    callback?.Invoke(true, joinCode);
                },
                () => { callback?.Invoke(false, ""); });
        }

        /// <summary>
        /// Start a relay server with the specified max players and region id.
        /// </summary>
        /// <param name="maxPlayers"></param>
        /// <param name="regionId"></param>
        /// <param name="callback"></param>
        public void StartRelayServer(int maxPlayers, string regionId, Action<bool, string> callback)
        {
            var tp = (RelayNetworkTransport)Transport;

            tp.AllocateRelayServer(maxPlayers, regionId,
                (joinCode) =>
                {
                    StartServer();
                    callback?.Invoke(true, joinCode);
                },
                () => { callback?.Invoke(false, ""); });
        }

        /// <summary>
        /// Join a relay server with the specified join code.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="callback"></param>
        public void JoinRelayServer(string code, Action<bool> callback)
        {
            var tp = (RelayNetworkTransport)Transport;

            tp.GetAllocationFromJoinCode(code,
                () =>
                {
                    StartClient();
                    callback?.Invoke(true);
                },
                () => { callback?.Invoke(false); });
        }
    }
}