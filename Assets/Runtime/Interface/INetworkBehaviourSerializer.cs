using System.IO;

namespace NetBuff.Interface
{
    /// <summary>
    /// Base interface used to create custom network behaviour serialization
    /// </summary>
    public interface INetworkBehaviourSerializer
    {
        /// <summary>
        /// Called when the object needs to be serialized
        /// If forceSendAll is true, all the data should be sent
        /// Otherwise, only the data that has changed should be sent
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="forceSendAll"></param>
        void OnSerialize(BinaryWriter writer, bool forceSendAll);
        
        /// <summary>
        /// Called when the object receives serialized data
        /// </summary>
        /// <param name="reader"></param>
        void OnDeserialize(BinaryReader reader);
    }
}