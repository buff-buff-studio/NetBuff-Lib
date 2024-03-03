using System;
using NetBuff.Components;
using UnityEngine;

namespace CTF
{
    public class Shot : NetworkBehaviour
    {
        public float lifeTime = 5.0f;
        public new Rigidbody rigidbody;

        public override void OnSpawned(bool isRetroactive)
        {
            base.OnSpawned(isRetroactive);
            rigidbody.velocity = transform.forward * 20;
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