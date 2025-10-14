using NetBuff.Components;
using UnityEngine;

namespace Samples.SceneLoading
{
    public class SceneLoadingSimplePlayer : NetworkBehaviour
    {
        public void Update()
        {
            if(!HasAuthority)
                return;

            var move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            transform.position += move * (Time.deltaTime * 3);
        }
    }
}
