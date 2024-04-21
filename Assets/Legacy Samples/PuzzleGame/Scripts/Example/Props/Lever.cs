using NetBuff.Interface;
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
            WithValues(isOn);
            handle.localEulerAngles = new Vector3(isOn.Value ? angle : 0, -90f, 0);
            //Needs to listen to a specific packet type
            GetPacketListener<PlayerPunchActionPacket>().OnServerReceive += OnPlayerPunch;
        }

        private void OnDisable()
        {
            GetPacketListener<PlayerPunchActionPacket>().OnServerReceive -= OnPlayerPunch;
        }

        private void Update()
        {
            handle.localEulerAngles = new Vector3(Mathf.Lerp(handle.localEulerAngles.x, isOn.Value ? angle : 0, Time.deltaTime * 10), -90f, 0);
        }

        private void OnPlayerPunch(PlayerPunchActionPacket obj, int client)
        {
            var o = GetNetworkObject(obj.Id);
            var dist = Vector3.Distance(o.transform.position, transform.position);

            if (dist > radius)
                return;

            isOn.Value = !isOn.Value;
        }
        public override bool GetInputValue()
        {
            return isOn.Value;
        }
    }
}