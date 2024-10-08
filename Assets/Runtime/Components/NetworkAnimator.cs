﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Components
{
    /// <summary>
    ///     Syncs the state of an Animator component over the network, including parameters, layers, speed and time
    /// </summary>
    [SuppressMessage("ReSharper", "ParameterHidesMember")]
    [Icon("Assets/Editor/Icons/NetworkAnimator.png")]
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/components/#network-animator")]
    public class NetworkAnimator : NetworkBehaviour, INetworkBehaviourSerializer
    {
        #region Inspector Fields
        [Header("SETTINGS")]
        [SerializeField]
        protected int tickRate = -1;

        [Header("REFERENCES")]
        [SerializeField]
        protected Animator animator;
        #endregion

        #region Internal Fields
        private float _animatorSpeed;
        private int[] _animationHash;
        private int[] _transitionHash;
        private float[] _layerWeight;

        private AnimatorControllerParameter[] _parameters;
        private int[] _intParameters;
        private float[] _floatParameters;
        private bool[] _boolParameters;

        private bool _running;
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
        ///     The Animator component to sync.
        /// </summary>
        public Animator Animator => animator;
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            _parameters = animator.parameters
                .Where(par => !animator.IsParameterControlledByCurve(par.nameHash))
                .ToArray();

            _intParameters = new int[_parameters.Length];
            _floatParameters = new float[_parameters.Length];
            _boolParameters = new bool[_parameters.Length];

            var layerCount = animator.layerCount;
            _animationHash = new int[layerCount];
            _transitionHash = new int[layerCount];
            _layerWeight = new float[layerCount];

            if (NetworkManager.Instance != null)
            {
                var man = NetworkManager.Instance;
                if (man.IsServerRunning || man.IsClientRunning)
                    _Begin();
            }
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(_Tick));
        }
        #endregion

        #region Internal Methods
        private void _Begin()
        {
            if (_running) return;
            _running = true;
            InvokeRepeating(nameof(_Tick), 0,
                1f / (tickRate == -1 ? NetworkManager.Instance.DefaultTickRate : tickRate));
        }

        private void _Tick()
        {
            if (!HasAuthority)
                return;

            if (!animator.enabled)
                return;

            var layers = new List<AnimatorSyncPacket.LayerInfo>();
            var changes = AnimatorSyncPacket.Changes.None;

            for (var i = 0; i < animator.layerCount; i++)
            {
                if (!_CheckAnimStateChanged(out var stateHash, out var normalizedTime, i))
                    continue;

                layers.Add(new AnimatorSyncPacket.LayerInfo
                {
                    LayerIndex = (byte)i,
                    StateHash = stateHash,
                    NormalizedTime = normalizedTime,
                    LayerWeight = animator.GetLayerWeight(i)
                });

                changes |= AnimatorSyncPacket.Changes.Layers;
            }

            if (Math.Abs(_animatorSpeed - animator.speed) > 0.01f)
            {
                _animatorSpeed = animator.speed;
                changes |= AnimatorSyncPacket.Changes.Speed;
            }

            var changedParameters = _CheckParameters(out var parameterData);
            if (changedParameters > 0)
                changes |= AnimatorSyncPacket.Changes.Parameters;

            if (changes == AnimatorSyncPacket.Changes.None)
                return;

            var packet = new AnimatorSyncPacket
            {
                Id = Id,
                Change = changes,
                Layers = layers.ToArray(),
                Speed = _animatorSpeed,
                ChangedParameters = (byte)changedParameters,
                ParameterData = parameterData
            };

            SendPacket(packet);
        }

        private int _CheckParameters(out byte[] bytes)
        {
            var parameterCount = (byte)_parameters.Length;
            var writer = new BinaryWriter(new MemoryStream());
            var changed = 0;

            for (byte i = 0; i < parameterCount; i++)
            {
                var par = _parameters[i];
                switch (par.type)
                {
                    case AnimatorControllerParameterType.Int:
                    {
                        var newIntValue = animator.GetInteger(par.nameHash);
                        if (_intParameters[i] != newIntValue)
                        {
                            _intParameters[i] = newIntValue;
                            writer.Write(i);
                            writer.Write(newIntValue);
                            changed++;
                        }

                        break;
                    }
                    case AnimatorControllerParameterType.Float:
                    {
                        var newFloatValue = animator.GetFloat(par.nameHash);
                        if (Math.Abs(_floatParameters[i] - newFloatValue) > 0.01f)
                        {
                            _floatParameters[i] = newFloatValue;
                            writer.Write(i);
                            writer.Write(newFloatValue);
                            changed++;
                        }

                        break;
                    }

                    case AnimatorControllerParameterType.Bool:
                    {
                        var newBoolValue = animator.GetBool(par.nameHash);
                        if (_boolParameters[i] != newBoolValue)
                        {
                            _boolParameters[i] = newBoolValue;
                            writer.Write(i);
                            writer.Write(newBoolValue);
                            changed++;
                        }

                        break;
                    }
                    case AnimatorControllerParameterType.Trigger:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            bytes = ((MemoryStream)writer.BaseStream).ToArray();
            return changed;
        }

        private bool _CheckAnimStateChanged(out int stateHash, out float normalizedTime, int layerId)
        {
            var change = false;
            stateHash = 0;
            normalizedTime = 0;
            var info = animator.GetCurrentAnimatorStateInfo(layerId);

            var lw = animator.GetLayerWeight(layerId);
            if (Mathf.Abs(lw - _layerWeight[layerId]) > 0.001f)
            {
                _layerWeight[layerId] = lw;
                if (Math.Abs(lw - 0) < 0.001f || Math.Abs(lw - 1) < 0.001f)
                {
                    stateHash = info.fullPathHash;
                    normalizedTime = info.normalizedTime;
                }

                change = true;
            }

            if (animator.IsInTransition(layerId))
            {
                var tt = animator.GetAnimatorTransitionInfo(layerId);
                if (tt.fullPathHash != _transitionHash[layerId])
                {
                    _transitionHash[layerId] = tt.fullPathHash;
                    _animationHash[layerId] = 0;
                    return true;
                }

                return change;
            }

            var st = animator.GetCurrentAnimatorStateInfo(layerId);
            if (st.fullPathHash != _animationHash[layerId])
            {
                if (_animationHash[layerId] != 0)
                {
                    stateHash = st.fullPathHash;
                    normalizedTime = st.normalizedTime;
                }

                _transitionHash[layerId] = 0;
                _animationHash[layerId] = st.fullPathHash;
                return true;
            }

            return change;
        }

        private void _ApplyAnimatorSyncPacket(AnimatorSyncPacket packet)
        {
            if ((packet.Change & AnimatorSyncPacket.Changes.Layers) != 0)
                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < packet.Layers.Length; i++)
                {
                    var layer = packet.Layers[i];
                    var index = layer.LayerIndex;

                    animator.SetLayerWeight(index, layer.LayerWeight);
                    if (layer.StateHash != 0 && animator.enabled)
                        animator.Play(layer.StateHash, index, layer.NormalizedTime);
                }

            if ((packet.Change & AnimatorSyncPacket.Changes.Speed) != 0)
                animator.speed = packet.Speed;

            if ((packet.Change & AnimatorSyncPacket.Changes.Parameters) != 0)
            {
                var reader = new BinaryReader(new MemoryStream(packet.ParameterData));
                for (var i = 0; i < packet.ChangedParameters; i++)
                {
                    var index = reader.ReadByte();
                    var par = _parameters[index];
                    switch (par.type)
                    {
                        case AnimatorControllerParameterType.Int:
                            animator.SetInteger(par.nameHash, reader.ReadInt32());
                            break;
                        case AnimatorControllerParameterType.Float:
                            animator.SetFloat(par.nameHash, reader.ReadSingle());
                            break;
                        case AnimatorControllerParameterType.Bool:
                            animator.SetBool(par.nameHash, reader.ReadBoolean());
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
        #endregion

        #region Animator Helpers
        /// <summary>
        ///     Sets a trigger on the Animator component.
        /// </summary>
        /// <param name="triggerHash"></param>
        public void SetTrigger(int triggerHash)
        {
            var packet = new AnimatorTriggerPacket
            {
                Id = Id,
                TriggerHash = triggerHash
            };
            SendPacket(packet);
        }

        /// <summary>
        ///     Sets a trigger on the Animator component.
        /// </summary>
        /// <param name="triggerName"></param>
        public void SetTrigger(string triggerName)
        {
            SetTrigger(Animator.StringToHash(triggerName));
            animator.SetTrigger(triggerName);
        }

        /// <summary>
        ///     Gets the value of the specified parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public float GetFloat(string name)
        {
            return animator.GetFloat(name);
        }

        /// <summary>
        ///     Sets the value of the specified parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetFloat(string name, float value)
        {
            animator.SetFloat(name, value);
        }

        /// <summary>
        ///     Gets the value of the specified parameter.
        /// </summary>
        /// <param name="nameHash"></param>
        /// <returns></returns>
        public float GetFloat(int nameHash)
        {
            return animator.GetFloat(nameHash);
        }

        /// <summary>
        ///     Sets the value of the specified parameter.
        /// </summary>
        /// <param name="nameHash"></param>
        /// <param name="value"></param>
        public void SetFloat(int nameHash, float value)
        {
            animator.SetFloat(nameHash, value);
        }

        /// <summary>
        ///     Gets the value of the specified parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool GetBool(string name)
        {
            return animator.GetBool(name);
        }

        /// <summary>
        ///     Sets the value of the specified parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetBool(string name, bool value)
        {
            animator.SetBool(name, value);
        }

        /// <summary>
        ///     Gets the value of the specified parameter.
        /// </summary>
        /// <param name="nameHash"></param>
        /// <returns></returns>
        public bool GetBool(int nameHash)
        {
            return animator.GetBool(nameHash);
        }

        /// <summary>
        ///     Sets the value of the specified parameter.
        /// </summary>
        /// <param name="nameHash"></param>
        /// <param name="value"></param>
        public void SetBool(int nameHash, bool value)
        {
            animator.SetBool(nameHash, value);
        }

        /// <summary>
        ///     Gets the value of the specified parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public int GetInteger(string name)
        {
            return animator.GetInteger(name);
        }

        /// <summary>
        ///     Sets the value of the specified parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetInteger(string name, int value)
        {
            animator.SetInteger(name, value);
        }

        /// <summary>
        ///     Gets the value of the specified parameter.
        /// </summary>
        /// <param name="nameHash"></param>
        /// <returns></returns>
        public int GetInteger(int nameHash)
        {
            return animator.GetInteger(nameHash);
        }

        /// <summary>
        ///     Sets the value of the specified parameter.
        /// </summary>
        /// <param name="nameHash"></param>
        /// <param name="value"></param>
        public void SetInteger(int nameHash, int value)
        {
            animator.SetInteger(nameHash, value);
        }
        #endregion

        #region Network Callbacks
        public override void OnSpawned(bool isRetroactive)
        {
            if (gameObject.activeInHierarchy && enabled)
                _Begin();
        }

        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            if (clientId != OwnerId)
                return;

            switch (packet)
            {
                case AnimatorSyncPacket animatorSyncPacket:
                    ServerBroadcastPacketExceptFor(animatorSyncPacket, clientId);
                    break;
                case AnimatorTriggerPacket triggerPacket:
                    ServerBroadcastPacketExceptFor(triggerPacket, clientId);
                    break;
            }
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            if (HasAuthority)
                return;

            switch (packet)
            {
                case AnimatorSyncPacket syncPacket:
                    _ApplyAnimatorSyncPacket(syncPacket);
                    break;
                case AnimatorTriggerPacket triggerPacket:
                    animator.SetTrigger(triggerPacket.TriggerHash);
                    break;
            }
        }

        public void OnSerialize(BinaryWriter writer, bool forceSendAll)
        {
            var layerCount = (byte)animator.layerCount;
            writer.Write(layerCount);

            for (var i = 0; i < layerCount; i++)
            {
                var st = animator.IsInTransition(i)
                    ? animator.GetNextAnimatorStateInfo(i)
                    : animator.GetCurrentAnimatorStateInfo(i);
                writer.Write(st.fullPathHash);
                writer.Write(st.normalizedTime);
                writer.Write(animator.GetLayerWeight(i));
            }

            var parameterCount = (byte)_parameters.Length;
            writer.Write(parameterCount);
            for (var i = 0; i < parameterCount; i++)
            {
                var par = _parameters[i];
                switch (par.type)
                {
                    case AnimatorControllerParameterType.Int:
                    {
                        var newIntValue = animator.GetInteger(par.nameHash);
                        writer.Write(newIntValue);
                        break;
                    }
                    case AnimatorControllerParameterType.Float:
                    {
                        var newFloatValue = animator.GetFloat(par.nameHash);
                        writer.Write(newFloatValue);
                        break;
                    }
                    case AnimatorControllerParameterType.Bool:
                    {
                        var newBoolValue = animator.GetBool(par.nameHash);
                        writer.Write(newBoolValue);
                        break;
                    }
                }
            }
        }

        public void OnDeserialize(BinaryReader reader)
        {
            var layerCount = reader.ReadByte();

            if (layerCount != animator.layerCount)
                throw new Exception("Layer count mismatch");

            for (var i = 0; i < layerCount; i++)
            {
                var stateHash = reader.ReadInt32();
                var normalizedTime = reader.ReadSingle();
                var weight = reader.ReadSingle();

                animator.SetLayerWeight(i, weight);
                animator.Play(stateHash, i, normalizedTime);
            }

            var parameterCount = reader.ReadByte();
            if (parameterCount != _parameters.Length)
                throw new Exception("Parameter count mismatch");

            var animatorEnabled = animator.enabled;

            for (var i = 0; i < parameterCount; i++)
            {
                var par = _parameters[i];
                switch (par.type)
                {
                    case AnimatorControllerParameterType.Int:
                    {
                        var newIntValue = reader.ReadInt32();
                        if (animatorEnabled)
                            animator.SetInteger(par.nameHash, newIntValue);
                        break;
                    }
                    case AnimatorControllerParameterType.Float:
                    {
                        var newFloatValue = reader.ReadSingle();
                        if (animatorEnabled)
                            animator.SetFloat(par.nameHash, newFloatValue);
                        break;
                    }
                    case AnimatorControllerParameterType.Bool:
                    {
                        var newBoolValue = reader.ReadBoolean();
                        if (animatorEnabled)
                            animator.SetBool(par.nameHash, newBoolValue);
                        break;
                    }
                }
            }
        }
        #endregion
    }

    /// <summary>
    ///     Packet used to sync the state of an Animator component.
    /// </summary>
    public class AnimatorSyncPacket : IOwnedPacket
    {
        /// <summary>
        ///     Represents the changes that have been made to the Animator.
        /// </summary>
        [Flags]
        public enum Changes
        {
            /// <summary>
            ///     No changes have been made.
            /// </summary>
            None = 0,

            /// <summary>
            ///     At least one layer have been changed.
            /// </summary>
            Layers = 1,

            /// <summary>
            ///     At least one parameter have been changed.
            /// </summary>
            Parameters = 2,

            /// <summary>
            ///     The speed of the Animator has been changed.
            /// </summary>
            Speed = 4
        }

        /// <summary>
        ///     Represents the changes that have been made to the Animator.
        /// </summary>
        public Changes Change { get; set; } = Changes.None;

        /// <summary>
        ///     Holds information about the layers that have been changed.
        /// </summary>
        public LayerInfo[] Layers { get; set; } = Array.Empty<LayerInfo>();  

        /// <summary>
        ///     The speed of the Animator.
        /// </summary>
        public float Speed { get; set; }

        /// <summary>
        ///     The amount of parameters that have been changed.
        /// </summary>
        public byte ChangedParameters { get; set; }

        /// <summary>
        ///     Holds the data of the parameters that have been changed.
        /// </summary>
        [InspectorMode(InspectorMode.Data)]
        public byte[] ParameterData { get; set; } = Array.Empty<byte>();

        /// <summary>
        ///     Holds the id of the object.
        /// </summary>
        [InspectorMode(InspectorMode.Object)]
        public NetworkId Id { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);

            writer.Write((byte)Change);

            if ((Change & Changes.Layers) != 0)
            {
                writer.Write((byte)Layers.Length);
                foreach (var layer in Layers)
                {
                    writer.Write(layer.LayerIndex);
                    writer.Write(layer.StateHash);
                    writer.Write(layer.NormalizedTime);
                    writer.Write(layer.LayerWeight);
                }
            }

            if ((Change & Changes.Speed) != 0)
                writer.Write(Speed);

            if ((Change & Changes.Parameters) != 0)
            {
                writer.Write(ChangedParameters);
                writer.Write(ParameterData.Length);
                writer.Write(ParameterData);
            }
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            Change = (Changes)reader.ReadByte();
            if ((Change & Changes.Layers) != 0)
            {
                var count = reader.ReadByte();
                Layers = new LayerInfo[count];
                for (var i = 0; i < count; i++)
                    Layers[i] = new LayerInfo
                    {
                        LayerIndex = reader.ReadByte(),
                        StateHash = reader.ReadInt32(),
                        NormalizedTime = reader.ReadSingle(),
                        LayerWeight = reader.ReadSingle()
                    };
            }

            if ((Change & Changes.Speed) != 0)
                Speed = reader.ReadSingle();

            if ((Change & Changes.Parameters) != 0)
            {
                ChangedParameters = reader.ReadByte();
                var count = reader.ReadInt32();
                ParameterData = reader.ReadBytes(count);
            }
        }

        /// <summary>
        ///     Holds information about a layer.
        /// </summary>
        public class LayerInfo
        {
            /// <summary>
            ///     The index of the layer.
            /// </summary>
            public byte LayerIndex { get; set; }

            /// <summary>
            ///     The hash of the current Animator state.
            /// </summary>
            public int StateHash { get; set; }

            /// <summary>
            ///     The normalized time of the current Animator state.
            /// </summary>
            public float NormalizedTime { get; set; }

            /// <summary>
            ///     The weight of the layer.
            /// </summary>
            public float LayerWeight { get; set; }
        }
    }

    /// <summary>
    ///     Packet used to trigger an Animator trigger.
    /// </summary>
    public class AnimatorTriggerPacket : IOwnedPacket
    {
        /// <summary>
        ///     The hash of the trigger.
        /// </summary>
        public int TriggerHash { get; set; }

        /// <summary>
        ///     The id of the object.
        /// </summary>
        [InspectorMode(InspectorMode.Object)]
        public NetworkId Id { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(TriggerHash);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            TriggerHash = reader.ReadInt32();
        }
    }
}