using System.IO;
using NetBuff.Components;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace Samples.Docs
{
    public class BasicMovement : NetworkBehaviour
    {
        public ColorNetworkValue color = new ColorNetworkValue(Color.white);
        public IntNetworkValue team = new IntNetworkValue(2, NetworkValue.ModifierType.Server);
        
        private void OnEnable()
        {
            WithValues(color);
        }
        
        private void Update()
        {
            if (!HasAuthority)
                return;
            
            var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            transform.position += move * Time.deltaTime * 3;
        }
    }
    
    public class CustomNetworkBehaviour : NetworkBehaviour, INetworkBehaviourSerializer
    {
        public float[] valueTooComplex = new float[0];

        public void OnSerialize(BinaryWriter writer, bool forceSendAll)
        {
            MarkSerializerDirty();
            writer.Write(valueTooComplex.Length);
            for (var i = 0; i < valueTooComplex.Length; i++)
            {
                writer.Write(valueTooComplex[i]);
            }   
        }
    
        public void OnDeserialize(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            valueTooComplex = new float[length];
            for (var i = 0; i < length; i++)
            {
                valueTooComplex[i] = reader.ReadSingle();
            }
        }
    }
}