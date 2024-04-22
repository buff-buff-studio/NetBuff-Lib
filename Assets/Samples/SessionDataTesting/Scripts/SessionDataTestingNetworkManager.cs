using System;
using System.IO;
using System.Linq;
using NetBuff;
using NetBuff.Packets;
using NetBuff.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.SessionDataTesting.Scripts
{
    public class SessionDataTestingNetworkManager : NetworkManager
    {
        public InputField nicknameInput;
        public Toggle shouldBeAccepted;

        #region Session Establishing
        public override NetworkSessionEstablishPacket OnCreateSessionEstablishPacket()
        {
            return new ExampleNetworkSessionEstablishPacket
            {
                ShouldBeAccepted = shouldBeAccepted.isOn,
                Nickname = nicknameInput.text
            };
        }

        public override SessionEstablishingResponse GetSessionEstablishingResponse(NetworkSessionEstablishPacket packet)
        {
            var examplePacket = (ExampleNetworkSessionEstablishPacket) packet;
            
            if (examplePacket.ShouldBeAccepted)
                return new SessionEstablishingResponse
                {
                    Type = SessionEstablishingResponse.SessionEstablishingResponseType.Accept,
                };
            
            return new SessionEstablishingResponse
                {
                    Type = SessionEstablishingResponse.SessionEstablishingResponseType.Reject,
                    Reason = "just_testing"
                };
        }
        #endregion
        

        #region Session Data Restoring
        protected override SessionData OnTryToRestoreSessionData(int clientId, NetworkSessionEstablishPacket packet)
        {
            //Keeps the session using the nickname
            var packetData = (ExampleNetworkSessionEstablishPacket) packet;
            return GetAllDisconnectedSessionData<ExampleSessionData>().FirstOrDefault(data => data.nickname == packetData.Nickname);
        }
        #endregion

        #region Session Data Creation
        //Server Side
        protected override SessionData OnCreateNewSessionData(int clientId, NetworkSessionEstablishPacket packet)
        {
            var packetData = (ExampleNetworkSessionEstablishPacket) packet;
            return new ExampleSessionData()
            {
                nickname = packetData.Nickname
            };
        }
        
        public override SessionData OnCreateEmptySessionData()
        {
            return new ExampleSessionData();
        }
        #endregion
        
        #region Session Data Manipulation
        public void Update()
        {
            if (Time.frameCount % 100 != 0)
                return;
            
            if(!IsServerRunning)
                return;
            
            foreach (var sessionData in GetAllSessionData<ExampleSessionData>())
            {
                sessionData.someInt++;
                //Sync data to the client
                sessionData.ApplyChanges();
            }
        }

        //Client Side
        public override void OnLocalSessionDataChanged(SessionData data)
        {
            var exampleSessionData = GetLocalSessionData<ExampleSessionData>();
            //var exampleSessionData = (ExampleSessionData) data;
            
            Debug.LogError($"Test: {exampleSessionData.someInt}");
        }
        #endregion
    }

    public class ExampleNetworkSessionEstablishPacket : NetworkSessionEstablishPacket
    {
        public bool ShouldBeAccepted { get; set; }
        public string Nickname { get; set; }
        
        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(ShouldBeAccepted);
            writer.Write(Nickname);
        }

        public override void Deserialize(BinaryReader reader)
        {
            ShouldBeAccepted = reader.ReadBoolean();
            Nickname = reader.ReadString();
        }
    }
    
    [Serializable]
    public class ExampleSessionData : SessionData
    {
        //Doesn't needs to be synced to the client
        public string nickname;
        
        //Needs to be synced to the client
        public int someInt;

        public override void Serialize(BinaryWriter writer, bool shouldSerializeEverything)
        {
            if (shouldSerializeEverything)
                writer.Write(nickname);
            writer.Write(someInt);
        }

        public override void Deserialize(BinaryReader reader, bool shouldDeserializeEverything)
        {
            if (shouldDeserializeEverything)
                nickname = reader.ReadString();
            someInt = reader.ReadInt32();
        }
    }
}