using System;
using System.Collections.Generic;
using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Components
{
    /// <summary>
    /// Component that syncs the transform of a game object over the network.
    /// </summary>
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
            ScaleZ = 256,
        }
        #endregion
        
        #region Public Fields
        [Header("SETTINGS")]
        public int tickRate = -1;
        public float positionThreshold = 0.001f;
        public float rotationThreshold = 0.001f;
        public float scaleThreshold = 0.001f;
        
        [SerializeField]
        protected SyncMode syncMode = SyncMode.PositionX | SyncMode.PositionY | SyncMode.PositionZ | SyncMode.RotationX | SyncMode.RotationY | SyncMode.RotationZ;
        public SyncMode SyncModeMask => syncMode;
        #endregion

        #region Internal Fields
        private Vector3 _lastPosition;
        private Vector3 _lastRotation;
        private Vector3 _lastScale;
        private bool _running;
        #endregion

        #region Unity Callbacks
        protected virtual void OnEnable()
        {
            var t = transform;
            _lastPosition = t.position;
            _lastRotation = t.eulerAngles;
            _lastScale = t.localScale;

            if (NetworkManager.Instance != null)
            {
                var man = NetworkManager.Instance;
                if (man.IsServerRunning || man.IsClientRunning)
                    _Begin();
            }
        }
        
        private void OnDisable()
        {
            if (_running)
            {
                CancelInvoke(nameof(Tick));
                _running = false;
            }
        }
        #endregion
        
        private void _Begin()
        {
            if (_running) return;
            _running = true;
            InvokeRepeating(nameof(Tick), 0, 1f / (tickRate == -1 ? NetworkManager.Instance.defaultTickRate : tickRate));
        }

        
        private void Tick()
        {
            if (!HasAuthority) return;
            if (!ShouldResend()) return;
            
            SendPacket(CreateTransformPacket());
        }

        #region Network Callbacks
        public override void OnSpawned(bool isRetroactive)
        {
            _Begin();
        }
        
        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            if (packet is TransformPacket transformPacket)
            {
                if(clientId == OwnerId)
                    ServerBroadcastPacketExceptFor(transformPacket, clientId);
            }
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            if (packet is TransformPacket transformPacket)
            {
                if (!HasAuthority)
                {
                    ApplyTransformPacket(transformPacket);
                }
            }
        }
        #endregion

        #region Virtual Methods
        protected virtual bool ShouldResend()
        {
            var t = transform;
            return Vector3.Distance(t.position, _lastPosition) > positionThreshold ||
                   Vector3.Distance(t.eulerAngles, _lastRotation) > rotationThreshold ||
                   Vector3.Distance(t.localScale, _lastScale) > scaleThreshold;
        }

        protected virtual TransformPacket CreateTransformPacket()
        {
            var t = transform;
            _lastPosition = t.position;
            _lastRotation = t.eulerAngles;
            _lastScale = t.localScale;
            
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
            
            return new TransformPacket(Id, components.ToArray());
        }
        
        protected virtual void ApplyTransformPacket(TransformPacket packet)
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
            if ((syncMode & SyncMode.ScaleZ) != 0) scale.z = components[index];
            
            t.position = pos;
            t.eulerAngles = rot;
            t.localScale = scale;
        }
        #endregion
    }

    public class TransformPacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public float[] Components { get; set; }
        
        public TransformPacket() {}
        public TransformPacket(NetworkId id, float[] components)
        {
            Id = id;
            Components = components;
        }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write((byte) Components.Length);
            foreach (var t in Components)
                writer.Write(t);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            var count = reader.ReadByte();
            Components = new float[count];
            for (var i = 0; i < count; i++)
                Components[i] = reader.ReadSingle();
        }
    }
}