using System.IO;

namespace NetBuff.Misc
{
    public interface INetworkBehaviourSerializer
    {
        void OnDeserialize(BinaryReader reader);
        void OnSerialize(BinaryWriter writer, bool forceSendAll);
    }
}