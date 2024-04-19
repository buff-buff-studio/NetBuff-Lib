using System.IO;

namespace NetBuff.Interface
{
    public interface INetworkBehaviourSerializer
    {
        void OnSerialize(BinaryWriter writer, bool forceSendAll);
        void OnDeserialize(BinaryReader reader);
    }
}