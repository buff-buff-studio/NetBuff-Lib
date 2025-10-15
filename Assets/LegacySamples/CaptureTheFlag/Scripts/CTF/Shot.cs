using NetBuff.Components;
using UnityEngine;

namespace CTF
{
    public class Shot : NetworkBehaviour
    {
        public float lifeTime = 5.0f;
        #pragma warning disable 0109
        public new Rigidbody rigidbody;

        public override void OnSpawned(bool isRetroactive)
        {
            base.OnSpawned(isRetroactive);
            rigidbody.linearVelocity = transform.forward * 20;
        }

        private void Update()
        {
            if(!HasAuthority)
                return;
            
            lifeTime -= Time.deltaTime;
            
            if (lifeTime <= 0)
                Despawn();
        }
    }
}