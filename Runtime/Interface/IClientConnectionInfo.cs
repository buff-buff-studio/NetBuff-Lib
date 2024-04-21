namespace NetBuff.Interface
{
    /// <summary>
    /// Represents the connection information of a client on the server side
    /// </summary>
    public interface IConnectionInfo
    {
        /// <summary>
        /// Current connection RTT (Round Trip Time) in milliseconds
        /// </summary>
        public int Latency { get; }
        
        /// <summary>
        /// Total packets sent to the client
        /// </summary>
        public long PacketSent { get; }
        
        /// <summary>
        /// total packets received from the client
        /// </summary>
        public long PacketReceived { get; }
        
        /// <summary>
        /// Total packets lost between the client and the server
        /// </summary>
        public long PacketLoss { get; }
        
        /// <summary>
        /// Percentage of packets lost between the client and the server
        /// </summary>
        public long PacketLossPercentage => PacketSent == 0 ? 0 : PacketLoss * 100 / PacketSent;
    }
}