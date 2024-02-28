using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Door : LogicOutput
    {
        public bool isOpen;
        
        public GameObject open;
        public GameObject closed;

        private void OnEnable()
        {
            UpdateVisuals();
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            if (packet is not DoorStatePacket doorStatePacket) return;
            isOpen = doorStatePacket.IsOpen;
            UpdateVisuals();
        }

        public override void OnOutputChanged(bool value)
        {
            if(!HasAuthority) return;
            
            if (isOpen != value)
                OnSetState(value);
        }

        private void UpdateVisuals()
        {
            open.SetActive(isOpen);
            closed.SetActive(!isOpen);
        }
        
        private void OnSetState(bool state)
        {
            isOpen = state;
            var packet = new DoorStatePacket
            {
                Id = Id,
                IsOpen = state
            };
            SendPacket(packet, true);
        }
    }

    public class DoorStatePacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public bool IsOpen { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(IsOpen);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            IsOpen = reader.ReadBoolean();
        }
    }
}