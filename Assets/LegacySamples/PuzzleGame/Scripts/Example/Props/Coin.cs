using NetBuff.Components;
using NetBuff.Misc;
using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Coin : NetworkBehaviour
    {
        public float radius = 2f;
        private void OnEnable()
        {
            PacketListener.GetPacketListener<PlayerPunchActionPacket>().AddServerListener(OnPlayerPunch);
        }

        private void OnDisable()
        {
            PacketListener.GetPacketListener<PlayerPunchActionPacket>().RemoveServerListener(OnPlayerPunch);
        }
        
        private bool OnPlayerPunch(PlayerPunchActionPacket obj, int client)
        {
            var o = GetNetworkObject(obj.Id);
            var dist = Vector3.Distance(o.transform.position, transform.position);
            
            if (dist > radius)
                return false;
            
            LevelManager.Instance.ChangeLevel(LevelManager.Instance.levelIndex + 1);
            return true;
        }
        
    }
}