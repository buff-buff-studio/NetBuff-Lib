using System;
using System.IO;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Session
{
    [Serializable]
    public class SessionData
    {
        [SerializeField]
        [HideInInspector]
        private int clientId;

        public int ClientId => clientId;

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