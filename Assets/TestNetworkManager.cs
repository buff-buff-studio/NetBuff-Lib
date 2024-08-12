using System;
using NetBuff;
using NetBuff.Packets;
using NetBuff.Session;

namespace DefaultNamespace
{
    [Serializable]
    public class TestSessionData : SessionData
    {
        public int test;
    }
    
    public class TestNetworkManager : NetworkManager
    {
        protected override SessionData OnCreateNewSessionData(int clientId, NetworkSessionEstablishRequestPacket requestPacket)
        {
            return new TestSessionData();
        }

        protected override SessionData OnCreateEmptySessionData()
        {
            return new TestSessionData();
        }
    }
}