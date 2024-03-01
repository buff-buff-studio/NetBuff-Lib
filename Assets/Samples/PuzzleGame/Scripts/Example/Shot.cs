using System;
using NetBuff.Components;
using UnityEngine;

namespace ExamplePlatformer
{
    public class Shot : NetworkBehaviour
    {
        public float lifeTime = 5.0f;

        private void Update()
        {
            if(!HasAuthority)
                return;
            
            transform.position +=  Time.deltaTime * 3f * transform.forward;
            
            lifeTime -= Time.deltaTime;
            
            if (lifeTime <= 0)
                Despawn();
        }
    }
}