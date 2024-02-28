using NetBuff.Interface;
using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Lever : LogicInput
    {
        public bool isOn;
        public float radius = 2f;
        
        public Transform handle;
        public float angle = 60;
        
        private void OnEnable()
        {
            handle.localEulerAngles = new Vector3(isOn ? angle : 0, -90f, 0);
            //Needs to listen to a specific packet type
            GetPacketListener<PlayerPunchActionPacket>().OnServerReceive += OnPlayerPunch;
        }

        private void OnDisable()
        {
            GetPacketListener<PlayerPunchActionPacket>().OnServerReceive -= OnPlayerPunch;
        }

        private void Update()
        {
            handle.localEulerAngles = new Vector3(Mathf.Lerp(handle.localEulerAngles.x, isOn ? angle : 0, Time.deltaTime * 10), -90f, 0);
        }

        private void OnPlayerPunch(PlayerPunchActionPacket obj, int client)
        {
            var o = GetNetworkObject(obj.Id);
            var dist = Vector3.Distance(o.transform.position, transform.position);
            
            if (dist > radius)
                return;
            
            //Change it
            _SetState(!isOn);
        }


        private void _SetState(bool state)
        {
            if(isOn == state)
                return;
            
            isOn = state;
            
            //Reusing another packet
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
            isOn = buttonStatePacket.IsPressed;
        }

        public override bool GetInputValue()
        {
            return isOn;
        }
        
    }
}