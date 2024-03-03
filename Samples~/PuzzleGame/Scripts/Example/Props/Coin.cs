using NetBuff.Components;
using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Coin : NetworkBehaviour
    {
        public float radius = 2f;
        private void OnEnable()
        {
            GetPacketListener<PlayerPunchActionPacket>().OnServerReceive += OnPlayerPunch;
        }

        private void OnDisable()
        {
            GetPacketListener<PlayerPunchActionPacket>().OnServerReceive -= OnPlayerPunch;
        }
        
        private void OnPlayerPunch(PlayerPunchActionPacket obj, int client)
        {
            var o = GetNetworkObject(obj.Id);
            var dist = Vector3.Distance(o.transform.position, transform.position);
            
            if (dist > radius)
                return;
            
            LevelManager.Instance.ChangeLevel(LevelManager.Instance.levelIndex + 1);
        }
        
    }
}