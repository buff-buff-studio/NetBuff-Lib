using System.Collections.Generic;
using UnityEngine;

// ReSharper disable BitwiseOperatorOnEnumWithoutFlags
namespace NetBuff.Components
{
    /// <summary>
    /// Component that syncs the transform and rigidbody of a game object over the network.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkRigidbodyTransform : NetworkTransform
    {
        private Rigidbody _rigidbody;

        public Rigidbody Rigidbody
        {
            get
            {
                if (_rigidbody == null)
                    _rigidbody = GetComponent<Rigidbody>();
                
                return _rigidbody;
            }
        }
        
        private Vector3 _lastVelocity;
        private Vector3 _lastAngularVelocity;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            var rb = Rigidbody;
            _lastVelocity = rb.velocity;
            _lastAngularVelocity = rb.angularVelocity;
        }
        
        protected override TransformPacket CreateTransformPacket()
        {
            _lastVelocity = Rigidbody.velocity;
            _lastAngularVelocity = Rigidbody.angularVelocity;
            
            var components = new List<float>();
            if ((syncMode & SyncMode.PositionX) != 0) components.Add(transform.position.x);
            if ((syncMode & SyncMode.PositionY) != 0) components.Add(transform.position.y);
            if ((syncMode & SyncMode.PositionZ) != 0) components.Add(transform.position.z);
            if ((syncMode & SyncMode.RotationX) != 0) components.Add(transform.eulerAngles.x);
            if ((syncMode & SyncMode.RotationY) != 0) components.Add(transform.eulerAngles.y);
            if ((syncMode & SyncMode.RotationZ) != 0) components.Add(transform.eulerAngles.z);
            if ((syncMode & SyncMode.ScaleX) != 0) components.Add(transform.localScale.x);
            if ((syncMode & SyncMode.ScaleY) != 0) components.Add(transform.localScale.y);
            if ((syncMode & SyncMode.ScaleZ) != 0) components.Add(transform.localScale.z);

            var rb = Rigidbody;
            var v = rb.velocity;
            var av = rb.angularVelocity;
            var cs = _rigidbody.constraints;
            if((cs & RigidbodyConstraints.FreezePositionX) == 0) components.Add(v.x);
            if((cs & RigidbodyConstraints.FreezePositionY) == 0) components.Add(v.y);
            if((cs & RigidbodyConstraints.FreezePositionZ) == 0) components.Add(v.z);
            if((cs & RigidbodyConstraints.FreezeRotationX) == 0) components.Add(av.x);
            if((cs & RigidbodyConstraints.FreezeRotationY) == 0) components.Add(av.y);
            if((cs & RigidbodyConstraints.FreezeRotationZ) == 0) components.Add(av.z);
            
            return new TransformPacket(Id, components.ToArray());
        }

        protected override void ApplyTransformPacket(TransformPacket packet)
        {
            var components = packet.Components;
            var t = transform;
            var pos = t.position;
            var rot = t.eulerAngles;
            var scale = t.localScale;
            
            
            var index = 0;
            if ((syncMode & SyncMode.PositionX) != 0) pos.x = components[index++];
            if ((syncMode & SyncMode.PositionY) != 0) pos.y = components[index++];
            if ((syncMode & SyncMode.PositionZ) != 0) pos.z = components[index++];
            if ((syncMode & SyncMode.RotationX) != 0) rot.x = components[index++];
            if ((syncMode & SyncMode.RotationY) != 0) rot.y = components[index++];
            if ((syncMode & SyncMode.RotationZ) != 0) rot.z = components[index++];  
            if ((syncMode & SyncMode.ScaleX) != 0) scale.x = components[index++];
            if ((syncMode & SyncMode.ScaleY) != 0) scale.y = components[index++];
            if ((syncMode & SyncMode.ScaleZ) != 0) scale.z = components[index++];
            
            var rb = Rigidbody;
            var v = rb.velocity;
            var av = rb.angularVelocity;
            var cs = _rigidbody.constraints;
            if((cs & RigidbodyConstraints.FreezePositionX) == 0) v.x = components[index++];
            if((cs & RigidbodyConstraints.FreezePositionY) == 0) v.y = components[index++];
            if((cs & RigidbodyConstraints.FreezePositionZ) == 0) v.z = components[index++];
            if((cs & RigidbodyConstraints.FreezeRotationX) == 0) av.x = components[index++];
            if((cs & RigidbodyConstraints.FreezeRotationY) == 0) av.y = components[index++];
            if((cs & RigidbodyConstraints.FreezeRotationZ) == 0) av.z = components[index];
            
            t.position = pos;
            t.eulerAngles = rot;
            t.localScale = scale;
            _rigidbody.velocity = v;
            _rigidbody.angularVelocity = av;
        }

        public override bool ShouldResend()
        {
            return base.ShouldResend() || Vector3.Distance(Rigidbody.velocity, _lastVelocity) > positionThreshold ||
                   Vector3.Distance(Rigidbody.angularVelocity, _lastAngularVelocity) > rotationThreshold;
        }
    }
}