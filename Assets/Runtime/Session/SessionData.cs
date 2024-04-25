using System;
using System.IO;
using NetBuff.Misc;

// ReSharper disable ConvertToAutoProperty

namespace NetBuff.Session
{
    /// <summary>
    ///     Base class for session data. Used to store data such as client score, team, etc.
    ///     Persistent in case of reconnection. Check Managing Sessions section in the documentation for more info.
    /// </summary>
    public class SessionData
    {
        private int _clientId;

        /// <summary>
        ///     Holds the client id that owns this session data.
        /// </summary>
        public int ClientId => _clientId;


        /// <summary>
        ///     Serializes data to byte array.
        ///     The parameter shouldSerializeEverything will only be true when the server is hot-reloading.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="shouldSerializeEverything"></param>
        public virtual void Serialize(BinaryWriter writer, bool shouldSerializeEverything)
        {
        }

        /// <summary>
        ///     Deserializes data from byte array.
        ///     The parameter shouldDeserializeEverything will only be true when the server is hot-reloading.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="shouldDeserializeEverything"></param>
        public virtual void Deserialize(BinaryReader reader, bool shouldDeserializeEverything)
        {
        }

        /// <summary>
        ///     Used to re-sync data to the client.
        /// </summary>
        /// <exception cref="Exception"></exception>
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