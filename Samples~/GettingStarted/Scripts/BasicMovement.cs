using NetBuff.Components;
using UnityEngine;

namespace NetBuff.Samples.GettingStarted
{
    public class BasicMovement : NetworkBehaviour
    {
        private void Update()
        {
            if (!HasAuthority)
                return;

            var move = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0);
            transform.position += move * Time.deltaTime * 3;
        }
    }
}