using System;
using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Button : LogicInput
    {
        public BoolNetworkValue isPressed = new(false);
        public Transform knob;
        private Collider[] _results = new Collider[16];

        private void OnEnable()
        {
            WithValues(isPressed);
        }

        public void FixedUpdate()
        {
            if (!HasAuthority)
                return;
            
            //Do sphere cast
            var size = Physics.OverlapSphereNonAlloc(transform.position + new Vector3(0, 0.75f, 0), 0.4f, _results);
            isPressed.Value = size > 0;
        }

        private void Update()
        {
            var o = knob.transform.localPosition.y;
            var f = Mathf.Lerp(o, isPressed.Value ? 0 : 0.05f, Time.deltaTime * 10);
            knob.transform.localPosition = new Vector3(0, f, 0);
        }

        public override bool GetInputValue()
        {
            return isPressed.Value;
        }
    }
}