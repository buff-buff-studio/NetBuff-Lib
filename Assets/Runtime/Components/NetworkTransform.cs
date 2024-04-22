using System;
using System.Collections.Generic;
using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Components
{
    [Icon("Assets/Editor/Icons/NetworkTransform.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-transform")]
    public class NetworkTransform : NetworkBehaviour
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
            ScaleZ = 256
        }
        #endregion

        private void _Begin()
        {
            if (_running) return;
            if (!HasAuthority) return;

            _running = true;
            InvokeRepeating(nameof(Tick), 0,
                1f / (tickRate == -1 ? NetworkManager.Instance.DefaultTickRate : tickRate));
        }

        private void _Stop()
        {
            if (_running)
            {
                CancelInvoke(nameof(Tick));
                _running = false;
            }
        }

        private void Tick()
        {
            if (!HasAuthority) return;
            if (ShouldResend(out var packet))
                SendPacket(packet);
        }

        #region Inspector Fields
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
            var t = transform;
            lastPosition = t.position;
            lastRotation = t.eulerAngles;
            lastScale = t.localScale;

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
            _Begin();
        }

        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            if (clientId != OwnerId)
                return;

            if (packet is TransformPacket transformPacket)
                ServerBroadcastPacketExceptFor(transformPacket, clientId);
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            if (HasAuthority)
                return;

            if (packet is TransformPacket transformPacket)
                ApplyTransformPacket(transformPacket);
        }
        #endregion

        #region Virtual Methods
        protected virtual bool ShouldResend(out TransformPacket packet)
        {
            var positionChanged = Vector3.Distance(transform.position, lastPosition) > positionThreshold;
            var rotationChanged = Vector3.Distance(transform.eulerAngles, lastRotation) > rotationThreshold;
            var scaleChanged = Vector3.Distance(transform.localScale, lastScale) > scaleThreshold;

            if (positionChanged || rotationChanged || scaleChanged)
            {
                components.Clear();

                var t = transform;
                lastPosition = t.position;
                lastRotation = t.eulerAngles;
                lastScale = t.localScale;

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
                    Flag = flag
                };
                return true;
            }

            packet = null;
            return false;
        }

        protected virtual void ApplyTransformPacket(TransformPacket packet)
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
                if ((syncMode & SyncMode.ScaleZ) != 0) scale.z = cmp[index];
                t.localScale = scale;
            }
        }
        #endregion
    }

    public class TransformPacket : IOwnedPacket
    {
        public float[] Components { get; set; }
        public short Flag { get; set; }
        public NetworkId Id { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(Flag);

            writer.Write((byte)Components.Length);
            foreach (var t in Components)
                writer.Write(t);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            Flag = reader.ReadInt16();

            var count = reader.ReadByte();
            Components = new float[count];
            for (var i = 0; i < count; i++)
                Components[i] = reader.ReadSingle();
        }
    }
}