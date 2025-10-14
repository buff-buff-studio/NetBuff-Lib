using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NetBuff.Interface;
using NetBuff.Packets;
using UnityEngine;

#if NETBUFF_ADVANCED_DEBUG
using NetBuff.Misc;
#endif

namespace NetBuff.Components
{
    [Icon("Assets/Editor/Icons/NetworkTransform.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-transform")]
    public class NetworkTransform : NetworkBehaviour, INetworkBehaviourSerializer
    {
        #region Enum
        [Flags]
        public enum SyncMode
        {
            None = 0,

            PositionX = 1,
            PositionY = 2,
            PositionZ = 4,

            RotationX = 8,
            RotationY = 16,
            RotationZ = 32,

            ScaleX = 64,
            ScaleY = 128,
            ScaleZ = 256,
            
            Position = PositionX | PositionY | PositionZ,
            Rotation = RotationX | RotationY | RotationZ,
            Scale = ScaleX | ScaleY | ScaleZ,
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
        
        #if NETBUFF_ADVANCED_DEBUG
        private float _lastUpdateTime = -5;
        #endif
        
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
        public int TickRate
        {
            get => tickRate;
            set => tickRate = value;
        }

        public float PositionThreshold
        {
            get => positionThreshold;
            set => positionThreshold = value;
        }

        public float RotationThreshold
        {
            get => rotationThreshold;
            set => rotationThreshold = value;
        }

        public float ScaleThreshold
        {
            get => scaleThreshold;
            set => scaleThreshold = value;
        }

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

        #if NETBUFF_ADVANCED_DEBUG
        private void OnRenderObject()
        {
            if (!DebugUtilities.EnableAdvancedDebugging)
                return;
            
            if (!DebugUtilities.NetworkTransformDraw)
                return;
            
            if(target == null)
                return;
            
            var interval = 1f / (tickRate == -1 ? NetworkManager.Instance.DefaultTickRate : tickRate);
            var isSleep = Time.unscaledTime - _lastUpdateTime > interval * 1.25f;
            
            if (!DebugUtilities.NetworkTransformDrawSleep && isSleep)
                return;
            
            DebugUtilities.DrawOutline(target.gameObject,isSleep ? Color.gray : Color.cyan, DebugUtilities.DefaultFillBounds);
        }
        #endif
        #endregion

        #region Internal Methods
        private void _Begin()
        {
            if (_running) return;
            if (!HasAuthority) return;

            if(target == null)
                target = transform;

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
            {
                #if NETBUFF_ADVANCED_DEBUG
                _lastUpdateTime = Time.unscaledTime;
                #endif
                SendPacket(packet);
            }
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
                    var needsSnap = false;
                    var currentEuler = target.eulerAngles;
                    
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
                        var smoothEuler = currentEuler;
                        
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
                        var closeEnough = true;
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
                    var current = target.eulerAngles;
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

            if (packet is NetworkTransformPacket transformPacket && transformPacket.BehaviourId == BehaviourId)
                ServerBroadcastPacketExceptFor(transformPacket, clientId);
        }

        public override void OnReceivePacket(IOwnedPacket packet)
        {
            if (HasAuthority)
                return;

            if (packet is NetworkTransformPacket transformPacket && transformPacket.BehaviourId == BehaviourId)
                ApplyTransformPacket(transformPacket);
        }
        #endregion

        #region Virtual Methods
        protected virtual bool ShouldResend(out NetworkTransformPacket packet)
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
                if (positionChanged && (syncMode & SyncMode.Position) != 0)
                {
                    flag |= 1;
                    if ((syncMode & SyncMode.PositionX) != 0) components.Add(lastPosition.x);
                    if ((syncMode & SyncMode.PositionY) != 0) components.Add(lastPosition.y);
                    if ((syncMode & SyncMode.PositionZ) != 0) components.Add(lastPosition.z);
                }

                if (rotationChanged && (syncMode & SyncMode.Rotation) != 0)
                {
                    flag |= 2;
                    if ((syncMode & SyncMode.RotationX) != 0) components.Add(lastRotation.x);
                    if ((syncMode & SyncMode.RotationY) != 0) components.Add(lastRotation.y);
                    if ((syncMode & SyncMode.RotationZ) != 0) components.Add(lastRotation.z);
                }

                if (scaleChanged && (syncMode & SyncMode.Scale) != 0)
                {
                    flag |= 4;
                    if ((syncMode & SyncMode.ScaleX) != 0) components.Add(lastScale.x);
                    if ((syncMode & SyncMode.ScaleY) != 0) components.Add(lastScale.y);
                    if ((syncMode & SyncMode.ScaleZ) != 0) components.Add(lastScale.z);
                }

                packet = new NetworkTransformPacket
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

        protected virtual void ApplyTransformPacket(NetworkTransformPacket packet)
        {
            #if NETBUFF_ADVANCED_DEBUG
            _lastUpdateTime = Time.unscaledTime;
            #endif
            
            var cmp = packet.Components;
            var flag = packet.Flag;

            // Instead of applying directly, set target values for interpolation
            var index = 0;
            
            // Position
            if ((flag & 1) != 0)
            {
                var basePos = _hasTargetPosition ? _targetPosition : target.position;
                
                if ((syncMode & SyncMode.PositionX) != 0) basePos.x = cmp[index++];
                if ((syncMode & SyncMode.PositionY) != 0) basePos.y = cmp[index++];
                if ((syncMode & SyncMode.PositionZ) != 0) basePos.z = cmp[index++];
                
                _targetPosition = basePos;
                _hasTargetPosition = true;
            }

            // Rotation
            if ((flag & 2) != 0)
            {
                var baseRot = _hasTargetRotation ? _targetEulerAngles : target.eulerAngles;
                
                if ((syncMode & SyncMode.RotationX) != 0) baseRot.x = cmp[index++];
                if ((syncMode & SyncMode.RotationY) != 0) baseRot.y = cmp[index++];
                if ((syncMode & SyncMode.RotationZ) != 0) baseRot.z = cmp[index++];
                
                _targetEulerAngles = baseRot;
                _hasTargetRotation = true;
            }

            // Scale
            if ((flag & 4) != 0)
            {
                var baseScale = _hasTargetScale ? _targetScale : target.localScale;
                
                if ((syncMode & SyncMode.ScaleX) != 0) baseScale.x = cmp[index++];
                if ((syncMode & SyncMode.ScaleY) != 0) baseScale.y = cmp[index++];
                if ((syncMode & SyncMode.ScaleZ) != 0) baseScale.z = cmp[index];
                
                _targetScale = baseScale;
                _hasTargetScale = true;
            }
        }
        #endregion
        
        #region Custom Serialization
        public virtual void OnSerialize(BinaryWriter writer, bool forceSendAll, bool isSnapshot)
        {
            if((syncMode & SyncMode.PositionX) != 0)
                writer.Write(target.position.x);
            if((syncMode & SyncMode.PositionY) != 0)
                writer.Write(target.position.y);
            if((syncMode & SyncMode.PositionZ) != 0)
                writer.Write(target.position.z);
            if((syncMode & SyncMode.RotationX) != 0)
                writer.Write(target.eulerAngles.x);
            if((syncMode & SyncMode.RotationY) != 0)
                writer.Write(target.eulerAngles.y);
            if((syncMode & SyncMode.RotationZ) != 0)
                writer.Write(target.eulerAngles.z);
            if((syncMode & SyncMode.ScaleX) != 0)
                writer.Write(target.localScale.x);
            if((syncMode & SyncMode.ScaleY) != 0)
                writer.Write(target.localScale.y);
            if((syncMode & SyncMode.ScaleZ) != 0)
                writer.Write(target.localScale.z);
        }

        public virtual void OnDeserialize(BinaryReader reader, bool isSnapshot)
        {
            var pos = target.position;
            
            if((syncMode & SyncMode.PositionX) != 0)
                pos.x = reader.ReadSingle();
            if((syncMode & SyncMode.PositionY) != 0)
                pos.y = reader.ReadSingle();
            if((syncMode & SyncMode.PositionZ) != 0)
                pos.z = reader.ReadSingle();
            target.position = pos;
            
            var rot = target.eulerAngles;
            if((syncMode & SyncMode.RotationX) != 0)
                rot.x = reader.ReadSingle();
            if((syncMode & SyncMode.RotationY) != 0)
                rot.y = reader.ReadSingle();
            if((syncMode & SyncMode.RotationZ) != 0)
                rot.z = reader.ReadSingle();
            target.eulerAngles = rot;
            
            var scale = target.localScale;
            if((syncMode & SyncMode.ScaleX) != 0)
                scale.x = reader.ReadSingle();
            if((syncMode & SyncMode.ScaleY) != 0)
                scale.y = reader.ReadSingle();
            
            target.localScale = scale;
        }
        #endregion
    }
}