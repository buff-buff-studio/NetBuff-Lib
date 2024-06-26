﻿using UnityEngine;

// ReSharper disable BitwiseOperatorOnEnumWithoutFlags
namespace NetBuff.Components
{
    /// <summary>
    ///     Syncs the components of a transform over the network, along with the velocity and angular velocity of a Rigidbody.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [Icon("Assets/Editor/Icons/NetworkRigidbodyTransform.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-rigidbody-transform")]
    public class NetworkRigidbodyTransform : NetworkTransform
    {
        #region Inspector Fields
        #pragma warning disable 0109
        [SerializeField]
        protected new Rigidbody rigidbody;

        [SerializeField]
        protected bool syncVelocity = true;

        [SerializeField]
        protected float velocityThreshold = 0.001f;

        [SerializeField]
        protected bool syncAngularVelocity = true;

        [SerializeField]
        protected float angularVelocityThreshold = 0.001f;

        [SerializeField]
        protected bool syncIsKinematic;
        #endregion

        #region Internal Fields
        private Vector3 _lastVelocity;
        private Vector3 _lastAngularVelocity;
        private bool _lastIsKinematic;
        #endregion

        #region Helper Properties
        /// <summary>
        ///     The Rigidbody component to sync.
        /// </summary>
        public Rigidbody Rigidbody => rigidbody;

        /// <summary>
        ///     Defines whether the velocity should be synced.
        /// </summary>
        public bool SyncVelocity
        {
            get => syncVelocity;
            set => syncVelocity = value;
        }

        /// <summary>
        ///     Defines the threshold for the velocity to be considered changed.
        /// </summary>
        public float VelocityThreshold
        {
            get => velocityThreshold;
            set => velocityThreshold = value;
        }

        /// <summary>
        ///     Defines whether the angular velocity should be synced.
        /// </summary>
        public bool SyncAngularVelocity
        {
            get => syncAngularVelocity;
            set => syncAngularVelocity = value;
        }

        /// <summary>
        ///     Defines the threshold for the angular velocity to be considered changed.
        /// </summary>
        public float AngularVelocityThreshold
        {
            get => angularVelocityThreshold;
            set => angularVelocityThreshold = value;
        }

        /// <summary>
        ///     Defines whether the isKinematic property should be synced.
        /// </summary>
        public bool SyncIsKinematic
        {
            get => syncIsKinematic;
            set => syncIsKinematic = value;
        }
        #endregion

        protected override void OnEnable()
        {
            base.OnEnable();

            if (rigidbody == null)
                if (TryGetComponent(out Rigidbody rb))
                {
                    rigidbody = rb;
                }
                else
                {
                    Debug.LogError("No Rigidbody component found on " + name);
                    enabled = false;
                    return;
                }

            _lastVelocity = rigidbody.velocity;
            _lastAngularVelocity = rigidbody.angularVelocity;
            _lastIsKinematic = rigidbody.isKinematic;
        }

        #region Virtual Methods
        protected override bool ShouldResend(out TransformPacket packet)
        {
            var positionChanged = Vector3.Distance(transform.position, lastPosition) > positionThreshold;
            var rotationChanged = Vector3.Distance(transform.eulerAngles, lastRotation) > rotationThreshold;
            var scaleChanged = Vector3.Distance(transform.localScale, lastScale) > scaleThreshold;
            var velocityChanged =
                syncVelocity && Vector3.Distance(rigidbody.velocity, _lastVelocity) > velocityThreshold;
            var angularVelocityChanged = syncAngularVelocity &&
                                         Vector3.Distance(rigidbody.angularVelocity, _lastAngularVelocity) >
                                         angularVelocityThreshold;
            var isKinematicChanged = syncIsKinematic && rigidbody.isKinematic != _lastIsKinematic;

            if (positionChanged || rotationChanged || scaleChanged || velocityChanged || angularVelocityChanged ||
                isKinematicChanged)
            {
                components.Clear();

                var t = transform;
                lastPosition = t.position;
                lastRotation = t.eulerAngles;
                lastScale = t.localScale;
                _lastVelocity = rigidbody.velocity;
                _lastAngularVelocity = rigidbody.angularVelocity;
                _lastIsKinematic = rigidbody.isKinematic;

                var flag = (short)0;
                if (positionChanged)
                {
                    flag |= 1;
                    if ((syncMode & SyncMode.PositionX) != 0) components.Add(lastPosition.x);
                    if ((syncMode & SyncMode.PositionY) != 0) components.Add(lastPosition.y);
                    if ((syncMode & SyncMode.PositionZ) != 0) components.Add(lastPosition.z);
                }

                if (rotationChanged)
                {
                    flag |= 2;
                    if ((syncMode & SyncMode.RotationX) != 0) components.Add(lastRotation.x);
                    if ((syncMode & SyncMode.RotationY) != 0) components.Add(lastRotation.y);
                    if ((syncMode & SyncMode.RotationZ) != 0) components.Add(lastRotation.z);
                }

                if (scaleChanged)
                {
                    flag |= 4;
                    if ((syncMode & SyncMode.ScaleX) != 0) components.Add(lastScale.x);
                    if ((syncMode & SyncMode.ScaleY) != 0) components.Add(lastScale.y);
                    if ((syncMode & SyncMode.ScaleZ) != 0) components.Add(lastScale.z);
                }

                if (velocityChanged)
                {
                    flag |= 8;
                    components.Add(_lastVelocity.x);
                    components.Add(_lastVelocity.y);
                    components.Add(_lastVelocity.z);
                }

                if (angularVelocityChanged)
                {
                    flag |= 16;
                    components.Add(_lastAngularVelocity.x);
                    components.Add(_lastAngularVelocity.y);
                    components.Add(_lastAngularVelocity.z);
                }

                if (isKinematicChanged)
                {
                    flag |= 32;
                    components.Add(_lastIsKinematic ? 1 : 0);
                }

                packet = new TransformPacket
                {
                    Id = Id,
                    Components = components.ToArray(),
                    Flag = flag
                };
                return true;
            }

            packet = null;
            return false;
        }

        protected override void ApplyTransformPacket(TransformPacket packet)
        {
            var cmp = packet.Components;
            var flag = packet.Flag;
            var t = transform;

            var index = 0;
            if ((flag & 1) != 0)
            {
                var pos = t.position;
                if ((syncMode & SyncMode.PositionX) != 0) pos.x = cmp[index++];
                if ((syncMode & SyncMode.PositionY) != 0) pos.y = cmp[index++];
                if ((syncMode & SyncMode.PositionZ) != 0) pos.z = cmp[index++];
                t.position = pos;
            }

            if ((flag & 2) != 0)
            {
                var rot = t.eulerAngles;
                if ((syncMode & SyncMode.RotationX) != 0) rot.x = cmp[index++];
                if ((syncMode & SyncMode.RotationY) != 0) rot.y = cmp[index++];
                if ((syncMode & SyncMode.RotationZ) != 0) rot.z = cmp[index++];
                t.eulerAngles = rot;
            }

            if ((flag & 4) != 0)
            {
                var scale = t.localScale;
                if ((syncMode & SyncMode.ScaleX) != 0) scale.x = cmp[index++];
                if ((syncMode & SyncMode.ScaleY) != 0) scale.y = cmp[index++];
                if ((syncMode & SyncMode.ScaleZ) != 0) scale.z = cmp[index++];
                t.localScale = scale;
            }

            if ((flag & 8) != 0)
            {
                var velocity = rigidbody.velocity;
                velocity.x = cmp[index++];
                velocity.y = cmp[index++];
                velocity.z = cmp[index++];

                if (!rigidbody.isKinematic)
                    rigidbody.velocity = velocity;
            }

            if ((flag & 16) != 0)
            {
                var angularVelocity = rigidbody.angularVelocity;
                angularVelocity.x = cmp[index++];
                angularVelocity.y = cmp[index++];
                angularVelocity.z = cmp[index++];
                rigidbody.angularVelocity = angularVelocity;
            }

            if ((flag & 32) != 0) rigidbody.isKinematic = cmp[index] > 0;
        }
        #endregion
    }
}