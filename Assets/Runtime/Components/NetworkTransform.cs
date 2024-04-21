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
        //Determines how often the transform should be synced. When set to -1, the default tick rate of the network manager will be used
        [Header("SETTINGS")]
        public int tickRate = -1;
        //The threshold for the position to be considered changed
        public float positionThreshold = 0.001f;
        //The threshold for the rotation to be considered changed
        public float rotationThreshold = 0.001f;
        //The threshold for the scale to be considered changed
        public float scaleThreshold = 0.001f;
        /// <summary>
        /// The sync mode mask for the transform
        /// This option determines which components of the transform should be synced
        /// </summary>
        [SerializeField]
        protected SyncMode syncMode = SyncMode.PositionX | SyncMode.PositionY | SyncMode.PositionZ | SyncMode.RotationX | SyncMode.RotationY | SyncMode.RotationZ;
        /// <summary>
        /// The sync mode mask for the transform
        /// This option determines which components of the transform should be synced
        /// </summary>
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
        /// Checks if the transform should be resynced
        /// </summary>
        /// <returns></returns>
        protected virtual bool ShouldResend()
        {
            var t = transform;
            return Vector3.Distance(t.position, _lastPosition) > positionThreshold ||
                   Vector3.Distance(t.eulerAngles, _lastRotation) > rotationThreshold ||
                   Vector3.Distance(t.localScale, _lastScale) > scaleThreshold;
        }

        /// <summary>
        /// Creates a transform packet from the transform state
        /// </summary>
        /// <returns></returns>
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
        
        /// <summary>
        /// Applies the transform packet to the game object.
        /// </summary>
        /// <param name="packet"></param>
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

    /// <summary>
    /// Used to sync the transform of a game object over the network.
    /// </summary>
    public class TransformPacket : IOwnedPacket
    {
        /// <summary>
        /// The network id of the game object
        /// </summary>
        public NetworkId Id { get; set; }
        
        /// <summary>
        /// An array of the transform components
        /// </summary>
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