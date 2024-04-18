using System;
using NetBuff.Components;
using UnityEngine;

namespace Samples.SceneLoading
{
    public class ObjectMovement : NetworkBehaviour
    {
        private void Update()
        {
            if (!HasAuthority)
                return;

            transform.position = new Vector3(0, Time.time * 2 % 4, 0);
        }
    }
}