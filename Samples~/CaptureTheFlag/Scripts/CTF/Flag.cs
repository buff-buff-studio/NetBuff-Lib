using NetBuff.Components;
using UnityEngine;

namespace CTF
{
    public class Flag : NetworkBehaviour
    {
        public int team = 0;
        
        public Renderer flagRenderer;

        private void OnEnable()
        {
            flagRenderer.materials[1].color = team == 0 ? Color.red : Color.blue;
        }
    }
}