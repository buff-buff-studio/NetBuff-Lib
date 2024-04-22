namespace NetBuff.Interface
{
    /// <summary>
    /// Holds a connection information along with the remote client id.
    /// </summary>
    public interface IClientConnectionInfo : IConnectionInfo
    {
        /// <summary>
        /// The id of the remote client.
        /// </summary>
        public int Id { get; }
    }
}