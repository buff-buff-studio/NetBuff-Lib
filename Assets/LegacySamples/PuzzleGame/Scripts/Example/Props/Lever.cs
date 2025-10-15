using NetBuff.Misc;
using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Lever : LogicInput
    {
        public BoolNetworkValue isOn = new(false);
        public float radius = 2f;
        
        public Transform handle;
        public float angle = 60;
        
        private void OnEnable()
        {
            handle.localEulerAngles = new Vector3(isOn.Value ? angle : 0, -90f, 0);
            
            //Needs to listen to a specific packet type
            PacketListener.GetPacketListener<PlayerPunchActionPacket>().AddServerListener(OnPlayerPunch);
        }

        private void OnDisable()
        {
            PacketListener.GetPacketListener<PlayerPunchActionPacket>().RemoveServerListener(OnPlayerPunch);
        }

        private void Update()
        {
            handle.localEulerAngles = new Vector3(Mathf.Lerp(handle.localEulerAngles.x, isOn.Value ? angle : 0, Time.deltaTime * 10), -90f, 0);
        }

        private bool OnPlayerPunch(PlayerPunchActionPacket obj, int client)
        {
            var o = GetNetworkObject(obj.Id);
            var dist = Vector3.Distance(o.transform.position, transform.position);

            if (dist > radius)
                return false;

            isOn.Value = !isOn.Value;
            return true;
        }
        public override bool GetInputValue()
        {
            return isOn.Value;
        }
    }
}