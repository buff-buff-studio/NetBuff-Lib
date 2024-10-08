﻿using NetBuff.Components;
using UnityEngine;

namespace Samples.AnimationTest
{
    public class AnimationTest : NetworkBehaviour
    {
        private static readonly int _Walking = Animator.StringToHash("walking");
        
        public Animator animator;
        public bool waving;
        
        public void Update()
        {
            if (!HasAuthority)
                return;

            if (Input.GetKeyDown(KeyCode.A))
                waving = !waving;
            
            var v = Mathf.Lerp(animator.GetFloat(_Walking), Input.GetAxisRaw("Vertical"), Time.deltaTime * 3);
            animator.SetFloat(_Walking, v);

            var w = Mathf.Lerp(animator.GetLayerWeight(1), waving ? 1 : 0, Time.deltaTime * 3f);
            animator.SetLayerWeight(1, w);
        }
    }
}