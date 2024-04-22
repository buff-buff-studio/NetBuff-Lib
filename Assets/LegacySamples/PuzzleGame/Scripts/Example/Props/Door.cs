using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Door : LogicOutput
    {
        public BoolNetworkValue isOpen = new(false);
        public GameObject open;
        public GameObject closed;

        private void OnEnable()
        {
            WithValues(isOpen);
            UpdateVisuals(isOpen.Value, isOpen.Value);
            isOpen.OnValueChanged += UpdateVisuals;
        }
        
        public override void OnOutputChanged(bool value)
        {
            if(!HasAuthority) return;
            isOpen.Value = value;
        }

        private void UpdateVisuals(bool old, bool now)
        {
            open.SetActive(now);
            closed.SetActive(!now);
        }
    }
}