using System.IO;

namespace NetBuff.Interface
{
    /// <summary>
    ///     Interface for serializing and deserializing network behaviours additional data.
    ///     You can implement this interface in your network behaviour to send additional data over the network.
    /// </summary>
    public interface INetworkBehaviourSerializer
    {
        /// <summary>
        ///     This method is called when the network behaviour is being serialized.
        ///     If forceSendAll is true, all data should be sent, otherwise only dirty data should be sent.
        ///     ForceSendAll will only be true when the server is sending data to a client for the first time.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="forceSendAll"></param>
        void OnSerialize(BinaryWriter writer, bool forceSendAll);

        /// <summary>
        ///     This method is called when the network behaviour is being deserialized.
        /// </summary>
        /// <param name="reader"></param>
        void OnDeserialize(BinaryReader reader);
    }
}