using UnityEngine;

namespace ExamplePlatformer.Props
{
    public class Platform : LogicOutput
    {
        public bool state;
        public Vector3 pointOff;
        public Vector3 pointOn;
        
        private void Update()
        {
            var t = transform;
            var parentPos = t.parent.position;
            transform.position = Vector3.MoveTowards(t.position, parentPos + (state ? pointOn : pointOff), Time.deltaTime * 4f);
        }
        
        public override void OnOutputChanged(bool value)
        {
            state = value;
        }
    }
}