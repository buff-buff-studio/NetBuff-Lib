using System.IO;

namespace NetBuff.Interface
{
    public interface INetworkBehaviourSerializer
    {
        void OnSerialize(BinaryWriter writer, bool forceSendAll, bool isSnapshot);

        void OnDeserialize(BinaryReader reader, bool isSnapshot);
    }
}