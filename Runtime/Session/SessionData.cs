using System;
using System.IO;
using NetBuff.Misc;

// ReSharper disable ConvertToAutoProperty

namespace NetBuff.Session
{
    public class SessionData
    {
        private int _clientId;

        public int ClientId => _clientId;


        public virtual void Serialize(BinaryWriter writer, bool shouldSerializeEverything)
        {
        }

        public virtual void Deserialize(BinaryReader reader, bool shouldDeserializeEverything)
        {
        }

        [ServerOnly]
        public void ApplyChanges()
        {
            var manager = NetworkManager.Instance;

            if (manager == null)
                throw new Exception("NetworkManager is null");

            if (!manager.IsServerRunning)
                throw new Exception("Only server can apply changes to session data");

            manager.SendSessionDataToClient(this);
        }
    }
}