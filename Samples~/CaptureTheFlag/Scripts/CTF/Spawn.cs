using System;
using System.Collections.Generic;
using NetBuff.Components;

namespace CTF
{
    public class Spawn : NetworkBehaviour
    {
        public static readonly List<Spawn> Spawns = new();
        
        public int team;

        private void OnEnable()
        {
            Spawns.Add(this);
        }

        private void OnDisable()
        {
            Spawns.Remove(this);
        }
    }
}