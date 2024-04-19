using NetBuff.Components;
using UnityEngine;

namespace Samples.AnimationTest
{
    public class AnimationTest : NetworkBehaviour
    {
        public Animator animator;

        public bool waving = false;

        public void Update()
        {
            if (!HasAuthority)
                return;
            float v = 1;
            v = Mathf.Lerp(animator.GetFloat("walking"), Input.GetAxisRaw("Vertical"), Time.deltaTime * 3);
            animator.SetFloat("walking", v);

            if (Input.GetKeyDown(KeyCode.A))
            {
                waving = !waving;
            }
            
            var w = Mathf.Lerp(animator.GetLayerWeight(1), waving ? 1 : 0, Time.deltaTime * 3f);
            animator.SetLayerWeight(1, w);
        }
    }
}