namespace NetBuff.Interface
{
    /// <summary>
    /// Represents the connection information of a client on the client side
    /// </summary>
    public interface IClientConnectionInfo : IConnectionInfo
    {
        /// <summary>
        /// Local client remote id on server
        /// </summary>
        public int Id { get; }
    }
}