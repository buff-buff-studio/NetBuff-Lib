namespace NetBuff.Interface
{
    /// <summary>
    ///     Holds a connection information
    /// </summary>
    public interface IConnectionInfo
    {
        /// <summary>
        ///     The latency of the connection (in milliseconds).
        ///     Represents the RTT (Round Trip Time) of the connection.
        /// </summary>
        public int Latency { get; }

        /// <summary>
        ///     The number of packets sent through the connection.
        /// </summary>
        public long PacketSent { get; }

        /// <summary>
        ///     The number of packets received through the connection.
        /// </summary>
        public long PacketReceived { get; }

        /// <summary>
        ///     The number of packets lost through the connection.
        /// </summary>
        public long PacketLoss { get; }

        /// <summary>
        ///     The percentage of packet loss through the connection.
        ///     It is calculated as PacketLoss * 100 / PacketSent.
        ///     Float value between 0 and 100.
        /// </summary>
        public long PacketLossPercentage => PacketSent == 0 ? 0 : PacketLoss * 100 / PacketSent;
    }
}