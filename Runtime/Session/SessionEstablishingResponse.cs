namespace NetBuff.Session
{
    /// <summary>
    ///     Response to a session establishing request.
    ///     If the response is accept, the session will be established.
    ///     If the response is reject, the session will not be established, and the client will be disconnected.
    /// </summary>
    public struct SessionEstablishingResponse
    {
        public enum SessionEstablishingResponseType
        {
            /// <summary>
            ///     Accept the session establishing request.
            /// </summary>
            Accept,

            /// <summary>
            ///     Reject the session establishing request.
            /// </summary>
            Reject
        }

        /// <summary>
        ///     The type of response.
        /// </summary>
        public SessionEstablishingResponseType Type { get; set; }

        /// <summary>
        ///     The reason for rejecting the session establishing request.
        /// </summary>
        public string Reason { get; set; }
    }
}