using System;
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

        #region Internal Methods
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
        /// <summary>
        ///     Determines if the transform should be resent to the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
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

        /// <summary>
        ///     Applies the transform components to the transform.
        /// </summary>
        /// <param name="packet"></param>
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

    /// <summary>
    ///     Packet used to sync the transform components.
    /// </summary>
    public class TransformPacket : IOwnedPacket
    {
        /// <summary>
        ///     The components of the transform.
        /// </summary>
        public float[] Components { get; set; }

        /// <summary>
        ///     Determines which components have been changed.
        /// </summary>
        public short Flag { get; set; }

        /// <summary>
        ///     The network id of the transform.
        /// </summary>
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