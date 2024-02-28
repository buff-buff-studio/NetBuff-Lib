using System.IO;
using BuffBuffNetcode.Interface;
using BuffBuffNetcode.Misc;
using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Button : LogicInput
    {
        public bool isPressed;
        public Transform knob;
        private Collider[] _results = new Collider[16];
        
        public void FixedUpdate()
        {
            if (!HasAuthority)
                return;
            
            //Do sphere cast
            var size = Physics.OverlapSphereNonAlloc(transform.position + new Vector3(0, 0.75f, 0), 0.4f, _results);
            if(size > 0)
            {
                _SetState(true);
            }
            else
            {
                _SetState(false);
            }
        }

        private void Update()
        {
            var o = knob.transform.localPosition.y;
            var f = Mathf.Lerp(o, isPressed ? 0 : 0.05f, Time.deltaTime * 10);
            knob.transform.localPosition = new Vector3(0, f, 0);
        }

        private void _SetState(bool state)
        {
            if(isPressed == state)
                return;
            
            isPressed = state;
            
            SendPacket(new ButtonStatePacket
            {
                Id = Id,
                IsPressed = state
            }, true);
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            if(HasAuthority)
                return;
            
            if (packet is not ButtonStatePacket buttonStatePacket) return;
            isPressed = buttonStatePacket.IsPressed;
        }

        public override bool GetInputValue()
        {
            return isPressed;
        }
    }

    public class ButtonStatePacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public bool IsPressed { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(IsPressed);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            IsPressed = reader.ReadBoolean();
        }
    }
}