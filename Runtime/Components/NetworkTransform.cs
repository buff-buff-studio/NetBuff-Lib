using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Components
{
    /// <summary>
    ///     Syncs the components of a transform over the network.
    /// </summary>
    [Icon("Assets/Editor/Icons/NetworkTransform.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-transform")]
    public class NetworkTransform : NetworkBehaviour
    {
        #region Enum
        [Flags]
        public enum SyncMode
        {
            /// <summary>
            ///     No components are synced.
            /// </summary>
            None = 0,

            /// <summary>
            ///     The x position of the transform is synced.
            /// </summary>
            PositionX = 1,

            /// <summary>
            ///     The y position of the transform is synced.
            /// </summary>
            PositionY = 2,

            /// <summary>
            ///     The z position of the transform is synced.
            /// </summary>
            PositionZ = 4,

            /// <summary>
            ///     The x rotation of the transform is synced.
            /// </summary>
            RotationX = 8,

            /// <summary>
            ///     The y rotation of the transform is synced.
            /// </summary>
            RotationY = 16,

            /// <summary>
            ///     The z rotation of the transform is synced.
            /// </summary>
            RotationZ = 32,

            /// <summary>
            ///     The x scale of the transform is synced.
            /// </summary>
            ScaleX = 64,

            /// <summary>
            ///     The y scale of the transform is synced.
            /// </summary>
            ScaleY = 128,

            /// <summary>
            ///     The z scale of the transform is synced.
            /// </summary>
            ScaleZ = 256
        }
        #endregion

        #region Inspector Fields
        public Transform target;
        
        [Header("SETTINGS")]
        [SerializeField]
        protected int tickRate = -1;

        [SerializeField]
        protected float positionThreshold = 0.001f;

        [SerializeField]
        protected float rotationThreshold = 0.001f;

        [SerializeField]
        protected float scaleThreshold = 0.001f;

        [SerializeField]
        protected SyncMode syncMode = SyncMode.PositionX | SyncMode.PositionY | SyncMode.PositionZ |
                                      SyncMode.RotationX | SyncMode.RotationY | SyncMode.RotationZ;
        
        [Header("SMOOTHING - POSITION")]
        [Tooltip("Enable position smoothing")]
        [SerializeField]
        protected bool smoothPosition = true;

        [Tooltip("Maximum position delta before snapping (0 = no limit)")]
        [SerializeField]
        protected float maxPositionDelta = 1f;

        [Tooltip("Position interpolation speed")]
        [SerializeField]
        protected float positionSmoothTime = 0.1f;
        
        [Header("SMOOTHING - ROTATION")]
        [Tooltip("Enable rotation smoothing")]
        [SerializeField]
        protected bool smoothRotation = true;

        [Tooltip("Maximum rotation delta (degrees) before snapping (0 = no limit)")]
        [SerializeField]
        protected float maxRotationDelta = 15f;

        [Tooltip("Rotation interpolation speed")]
        [SerializeField]
        protected float rotationSmoothTime = 0.1f;
        
        [Header("SMOOTHING - SCALE")]
        [Tooltip("Enable scale smoothing")]
        [SerializeField]
        protected bool smoothScale = true;

        [Tooltip("Maximum scale delta before snapping (0 = no limit)")]
        [SerializeField]
        protected float maxScaleDelta = 0.25f;

        [Tooltip("Scale interpolation speed")]
        [SerializeField]
        protected float scaleSmoothTime = 0.1f;
        #endregion

        #region Internal Fields
        protected Vector3 lastPosition;
        protected Vector3 lastRotation;
        protected Vector3 lastScale;
        private bool _running;
        protected readonly List<float> components = new();
        
        // Smoothing targets
        private Vector3 _targetPosition;
        private Vector3 _targetEulerAngles;
        private Vector3 _targetScale;
        private bool _hasTargetPosition;
        private bool _hasTargetRotation;
        private bool _hasTargetScale;
        private Vector3 _positionVelocity;
        private Vector3 _rotationVelocity;
        private Vector3 _scaleVelocity;
        #endregion

        #region Helper Properties
        /// <summary>
        ///     Determines the tick rate of the NetworkAnimator. When set to -1, the default tick rate of the NetworkManager will
        ///     be used.
        /// </summary>
        public int TickRate
        {
            get => tickRate;
            set => tickRate = value;
        }

        /// <summary>
        ///     Defines the threshold for the position to be considered changed.
        /// </summary>
        public float PositionThreshold
        {
            get => positionThreshold;
            set => positionThreshold = value;
        }

        /// <summary>
        ///     Defines the threshold for the rotation to be considered changed.
        /// </summary>
        public float RotationThreshold
        {
            get => rotationThreshold;
            set => rotationThreshold = value;
        }

        /// <summary>
        ///     Defines the threshold for the scale to be considered changed.
        /// </summary>
        public float ScaleThreshold
        {
            get => scaleThreshold;
            set => scaleThreshold = value;
        }

        /// <summary>
        ///     Defines which components of the transform should be synced.
        /// </summary>
        public SyncMode SyncModeMask => syncMode;
        #endregion

        #region Unity Callbacks
        protected virtual void OnEnable()
        {
            if(target == null)
                target = transform;

            lastPosition = target.position;
            lastRotation = target.eulerAngles;
            lastScale = target.localScale;
            
            _hasTargetPosition = false;
            _hasTargetRotation = false;
            _hasTargetScale = false;
            _positionVelocity = Vector3.zero;
            _rotationVelocity = Vector3.zero;
            _scaleVelocity = Vector3.zero;

            if (NetworkManager.Instance != null)
            {
                var man = NetworkManager.Instance;
                if (man.IsServerRunning || man.IsClientRunning)
                    _Begin();
            }
        }

        private void OnDisable()
        {
            _Stop();
            _hasTargetPosition = false;
            _hasTargetRotation = false;
            _hasTargetScale = false;
        }

        protected virtual void Update()
        {
            if (!HasAuthority)
                Interpolate();
        }
        #endregion

        #region Internal Methods
        private void _Begin()
        {
            if (_running) return;
            if (!HasAuthority) return;

            _running = true;
            _running = true;
            StartCoroutine(TickCoroutine());
        }

        private void _Stop()
        {
            _running = false;
        }

        private IEnumerator TickCoroutine()
        {
            var interval = 1f / (tickRate == -1 ? NetworkManager.Instance.DefaultTickRate : tickRate);
            while (_running)
            {
                Tick();
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        private void Tick()
        {
            if (!HasAuthority) return;
            if (ShouldResend(out var packet))
                SendPacket(packet);
        }
        
        protected virtual void Interpolate()
        {
            // Position interpolation
            if (_hasTargetPosition)
            {
                if (smoothPosition)
                {
                    // Check if we need to snap
                    if (maxPositionDelta > 0 && Vector3.Distance(target.position, _targetPosition) > maxPositionDelta)
                    {
                        target.position = _targetPosition;
                        _hasTargetPosition = false;
                        _positionVelocity = Vector3.zero;
                    }
                    else
                    {
                        target.position = Vector3.SmoothDamp(
                            target.position,
                            _targetPosition,
                            ref _positionVelocity,
                            positionSmoothTime
                        );
                        
                        // Check if close enough to snap
                        if (Vector3.Distance(target.position, _targetPosition) < positionThreshold)
                        {
                            target.position = _targetPosition;
                            _hasTargetPosition = false;
                        }
                    }
                }
                else
                {
                    target.position = _targetPosition;
                    _hasTargetPosition = false;
                }
            }

            // Rotation interpolation
            if (_hasTargetRotation)
            {
                if (smoothRotation)
                {
                    // Handle snapping per axis
                    bool needsSnap = false;
                    Vector3 currentEuler = target.eulerAngles;
                    
                    if (maxRotationDelta > 0)
                    {
                        if ((syncMode & SyncMode.RotationX) != 0 && 
                            Mathf.Abs(Mathf.DeltaAngle(currentEuler.x, _targetEulerAngles.x)) > maxRotationDelta)
                            needsSnap = true;
                        
                        if ((syncMode & SyncMode.RotationY) != 0 && 
                            Mathf.Abs(Mathf.DeltaAngle(currentEuler.y, _targetEulerAngles.y)) > maxRotationDelta)
                            needsSnap = true;
                        
                        if ((syncMode & SyncMode.RotationZ) != 0 && 
                            Mathf.Abs(Mathf.DeltaAngle(currentEuler.z, _targetEulerAngles.z)) > maxRotationDelta)
                            needsSnap = true;
                    }

                    if (needsSnap)
                    {
                        // Apply synced axes directly
                        if ((syncMode & SyncMode.RotationX) != 0) currentEuler.x = _targetEulerAngles.x;
                        if ((syncMode & SyncMode.RotationY) != 0) currentEuler.y = _targetEulerAngles.y;
                        if ((syncMode & SyncMode.RotationZ) != 0) currentEuler.z = _targetEulerAngles.z;
                        
                        target.eulerAngles = currentEuler;
                        _hasTargetRotation = false;
                        _rotationVelocity = Vector3.zero;
                    }
                    else
                    {
                        // Smooth each axis separately
                        Vector3 smoothEuler = currentEuler;
                        
                        if ((syncMode & SyncMode.RotationX) != 0)
                            smoothEuler.x = Mathf.SmoothDampAngle(
                                currentEuler.x,
                                _targetEulerAngles.x,
                                ref _rotationVelocity.x,
                                rotationSmoothTime
                            );
                        
                        if ((syncMode & SyncMode.RotationY) != 0)
                            smoothEuler.y = Mathf.SmoothDampAngle(
                                currentEuler.y,
                                _targetEulerAngles.y,
                                ref _rotationVelocity.y,
                                rotationSmoothTime
                            );
                        
                        if ((syncMode & SyncMode.RotationZ) != 0)
                            smoothEuler.z = Mathf.SmoothDampAngle(
                                currentEuler.z,
                                _targetEulerAngles.z,
                                ref _rotationVelocity.z,
                                rotationSmoothTime
                            );
                        
                        target.eulerAngles = smoothEuler;
                        
                        // Check if close enough to snap
                        bool closeEnough = true;
                        if ((syncMode & SyncMode.RotationX) != 0 && 
                            Mathf.Abs(Mathf.DeltaAngle(smoothEuler.x, _targetEulerAngles.x)) > rotationThreshold)
                            closeEnough = false;
                        
                        if ((syncMode & SyncMode.RotationY) != 0 && 
                            Mathf.Abs(Mathf.DeltaAngle(smoothEuler.y, _targetEulerAngles.y)) > rotationThreshold)
                            closeEnough = false;
                        
                        if ((syncMode & SyncMode.RotationZ) != 0 && 
                            Mathf.Abs(Mathf.DeltaAngle(smoothEuler.z, _targetEulerAngles.z)) > rotationThreshold)
                            closeEnough = false;
                        
                        if (closeEnough)
                        {
                            if ((syncMode & SyncMode.RotationX) != 0) smoothEuler.x = _targetEulerAngles.x;
                            if ((syncMode & SyncMode.RotationY) != 0) smoothEuler.y = _targetEulerAngles.y;
                            if ((syncMode & SyncMode.RotationZ) != 0) smoothEuler.z = _targetEulerAngles.z;
                            
                            target.eulerAngles = smoothEuler;
                            _hasTargetRotation = false;
                        }
                    }
                }
                else
                {
                    // Apply rotation directly
                    Vector3 current = target.eulerAngles;
                    if ((syncMode & SyncMode.RotationX) != 0) current.x = _targetEulerAngles.x;
                    if ((syncMode & SyncMode.RotationY) != 0) current.y = _targetEulerAngles.y;
                    if ((syncMode & SyncMode.RotationZ) != 0) current.z = _targetEulerAngles.z;
                    
                    target.eulerAngles = current;
                    _hasTargetRotation = false;
                }
            }

            // Scale interpolation
            if (_hasTargetScale)
            {
                if (smoothScale)
                {
                    // Check if we need to snap
                    if (maxScaleDelta > 0 && Vector3.Distance(target.localScale, _targetScale) > maxScaleDelta)
                    {
                        target.localScale = _targetScale;
                        _hasTargetScale = false;
                        _scaleVelocity = Vector3.zero;
                    }
                    else
                    {
                        target.localScale = Vector3.SmoothDamp(
                            target.localScale,
                            _targetScale,
                            ref _scaleVelocity,
                            scaleSmoothTime
                        );
                        
                        // Check if close enough to snap
                        if (Vector3.Distance(target.localScale, _targetScale) < scaleThreshold)
                        {
                            target.localScale = _targetScale;
                            _hasTargetScale = false;
                        }
                    }
                }
                else
                {
                    target.localScale = _targetScale;
                    _hasTargetScale = false;
                }
            }
        }
        #endregion

        #region Network Callbacks
        public override void OnOwnershipChanged(int oldOwner, int newOwner)
        {
            if (HasAuthority)
            {
                // Reset targets when gaining authority
                _hasTargetPosition = false;
                _hasTargetRotation = false;
                _hasTargetScale = false;
                _Begin();
            }
            else
                _Stop();
        }

        public override void OnSpawned(bool isRetroactive)
        {
            if (gameObject.activeInHierarchy && enabled)
                _Begin();
        }

        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            if (clientId != OwnerId)
                return;

            if (packet is TransformPacket transformPacket && transformPacket.BehaviourId == BehaviourId)
                ServerBroadcastPacketExceptFor(transformPacket, clientId);
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            if (HasAuthority)
                return;

            if (packet is TransformPacket transformPacket && transformPacket.BehaviourId == BehaviourId)
                ApplyTransformPacket(transformPacket);
        }
        #endregion

        #region Virtual Methods
        /// <summary>
        ///     Determines if the transform should be resent to the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        protected virtual bool ShouldResend(out TransformPacket packet)
        {
            var positionChanged = Vector3.Distance(target.position, lastPosition) > positionThreshold;
            var rotationChanged = Vector3.Distance(target.eulerAngles, lastRotation) > rotationThreshold;
            var scaleChanged = Vector3.Distance(target.localScale, lastScale) > scaleThreshold;

            if (positionChanged || rotationChanged || scaleChanged)
            {
                components.Clear();

                lastPosition = target.position;
                lastRotation = target.eulerAngles;
                lastScale = target.localScale;

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

                packet = new TransformPacket
                {
                    Id = Id,
                    Components = components.ToArray(),
                    Flag = flag,
                    BehaviourId = BehaviourId
                };
                return true;
            }

            packet = null;
            return false;
        }

        /// <summary>
        ///     Applies the transform components to the transform.
        /// </summary>
        /// <param name="packet"></param>
        protected virtual void ApplyTransformPacket(TransformPacket packet)
        {
            var cmp = packet.Components;
            var flag = packet.Flag;

            // Instead of applying directly, set target values for interpolation
            int index = 0;
            
            // Position
            if ((flag & 1) != 0)
            {
                Vector3 basePos = _hasTargetPosition ? _targetPosition : target.position;
                
                if ((syncMode & SyncMode.PositionX) != 0) basePos.x = cmp[index++];
                if ((syncMode & SyncMode.PositionY) != 0) basePos.y = cmp[index++];
                if ((syncMode & SyncMode.PositionZ) != 0) basePos.z = cmp[index++];
                
                _targetPosition = basePos;
                _hasTargetPosition = true;
            }

            // Rotation
            if ((flag & 2) != 0)
            {
                Vector3 baseRot = _hasTargetRotation ? _targetEulerAngles : target.eulerAngles;
                
                if ((syncMode & SyncMode.RotationX) != 0) baseRot.x = cmp[index++];
                if ((syncMode & SyncMode.RotationY) != 0) baseRot.y = cmp[index++];
                if ((syncMode & SyncMode.RotationZ) != 0) baseRot.z = cmp[index++];
                
                _targetEulerAngles = baseRot;
                _hasTargetRotation = true;
            }

            // Scale
            if ((flag & 4) != 0)
            {
                Vector3 baseScale = _hasTargetScale ? _targetScale : target.localScale;
                
                if ((syncMode & SyncMode.ScaleX) != 0) baseScale.x = cmp[index++];
                if ((syncMode & SyncMode.ScaleY) != 0) baseScale.y = cmp[index++];
                if ((syncMode & SyncMode.ScaleZ) != 0) baseScale.z = cmp[index];
                
                _targetScale = baseScale;
                _hasTargetScale = true;
            }
        }
        #endregion
    }
}