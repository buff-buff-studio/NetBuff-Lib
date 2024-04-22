namespace NetBuff.Session
{
    public struct SessionEstablishingResponse
    {
        public enum SessionEstablishingResponseType
        {
            Accept,
            Reject
        }
        
        public SessionEstablishingResponseType Type { get; set; }
        public string Reason { get; set; }
    }
}