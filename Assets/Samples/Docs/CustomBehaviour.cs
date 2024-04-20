using NetBuff.Components;
using NetBuff.Misc;
using UnityEngine;

namespace Samples.Docs
{
    public class CustomBehaviour : NetworkBehaviour
    {
        //Only the NetworkIdentity owner can modify this value
        public ColorNetworkValue color = new ColorNetworkValue(Color.white);
        //Only the server can modifiy this value
        public IntNetworkValue team = new IntNetworkValue(2, NetworkValue.ModifierType.Server);
        
        private void OnEnable()
        {
            //Register values
            WithValues(color, team);
            team.Value = 2;
        }
    
        private void Update()
        {
            if(!HasAuthority)
                return;
    
            if(Input.GetKeyDown(KeyCode.Space))
                team.Value ++;   
        }
    }
}