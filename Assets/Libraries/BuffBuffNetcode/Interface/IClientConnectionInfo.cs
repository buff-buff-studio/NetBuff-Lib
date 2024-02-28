namespace BuffBuffNetcode.Interface
{
    public interface IConnectionInfo
    {
        public int Latency { get; }
        public long PacketSent { get; }
        public long PacketReceived { get; }
        public long PacketLoss { get; }

        public long PacketLossPercentage => PacketSent == 0 ? 0 : PacketLoss * 100 / PacketSent;
    }
}