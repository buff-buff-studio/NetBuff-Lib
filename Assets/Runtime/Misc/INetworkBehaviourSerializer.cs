using System.IO;

namespace NetBuff.Misc
{
    public interface INetworkBehaviourSerializer
    {
        void OnSerialize(BinaryWriter writer, bool forceSendAll);
        void OnDeserialize(BinaryReader reader);
    }
}