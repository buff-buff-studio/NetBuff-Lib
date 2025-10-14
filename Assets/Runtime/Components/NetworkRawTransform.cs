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
    public class NetworkRawTransform : NetworkBehaviour, INetworkBehaviourSerializer
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
        #endregion

        #region Internal Fields
        protected Vector3 lastPosition;
        protected Vector3 lastRotation;
        protected Vector3 lastScale;
        private bool _running;
        protected readonly List<float> components = new();
        
        #if NETBUFF_ADVANCED_DEBUG
        private float _lastUpdateTime = -5f;
        #endif
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
            if (target == null)
                target = transform;

            lastPosition = target.position;
            lastRotation = target.eulerAngles;
            lastScale = target.localScale;

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

        #region Public Methods
        public void Refresh()
        {
            #if NETBUFF_ADVANCED_DEBUG
            _lastUpdateTime = Time.unscaledTime;
            #endif
            if (ShouldResend(out var packet))
            {
                #if NETBUFF_ADVANCED_DEBUG
                _lastUpdateTime = Time.unscaledTime;
                #endif
                SendPacket(packet);
            }
        }
        #endregion

        #region Internal Methods
        private void _Begin()
        {
            if (_running) return;
            if (!HasAuthority) return;

            if (target == null)
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
        #endregion

        #region Network Callbacks
        public override void OnOwnershipChanged(int oldOwner, int newOwner)
        {
            if (HasAuthority)
                _Begin();
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

            var index = 0;
            if ((flag & 1) != 0)
            {
                var pos = target.position;
                if ((syncMode & SyncMode.PositionX) != 0) pos.x = cmp[index++];
                if ((syncMode & SyncMode.PositionY) != 0) pos.y = cmp[index++];
                if ((syncMode & SyncMode.PositionZ) != 0) pos.z = cmp[index++];
                target.position = pos;
            }

            if ((flag & 2) != 0)
            {
                var rot = target.eulerAngles;
                if ((syncMode & SyncMode.RotationX) != 0) rot.x = cmp[index++];
                if ((syncMode & SyncMode.RotationY) != 0) rot.y = cmp[index++];
                if ((syncMode & SyncMode.RotationZ) != 0) rot.z = cmp[index++];
                target.eulerAngles = rot;
            }

            if ((flag & 4) != 0)
            {
                var scale = target.localScale;
                if ((syncMode & SyncMode.ScaleX) != 0) scale.x = cmp[index++];
                if ((syncMode & SyncMode.ScaleY) != 0) scale.y = cmp[index++];
                if ((syncMode & SyncMode.ScaleZ) != 0) scale.z = cmp[index];
                target.localScale = scale;
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