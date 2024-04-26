using NetBuff.Components;
using UnityEngine;

namespace Samples.SceneLoading
{
    public class SimpleObjectMover : NetworkBehaviour
    {
        public Vector3 multiplier = new(0, 1, 0);
        
        private void Update()
        {
            if (!HasAuthority)
                return;

            transform.position = multiplier * (Time.time * 2 % 4);
        }
    }
}