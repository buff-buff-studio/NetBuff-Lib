using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Components
{
    /// <summary>
    /// Component that syncs a animator state and parameters over the network
    /// </summary>
    [SuppressMessage("ReSharper", "ParameterHidesMember")]
    public class NetworkAnimator : NetworkBehaviour
    {
        private float _animatorSpeed;
        private int[] _animationHash;
        private int[] _transitionHash;
        private float[] _layerWeight;
        
        private AnimatorControllerParameter[] _parameters;
        private int[] _intParameters;
        private float[] _floatParameters;
        private bool[] _boolParameters;
        
        [Header("SETTINGS")]
        public int tickRate = -1;
        
        [Header("REFERENCES")]
        public Animator animator;
        
        private void OnEnable()
        {
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
            
            InvokeRepeating(nameof(Tick), 0, 1f / (tickRate == -1 ? NetworkManager.Instance.defaultTickRate : tickRate));
        }
        
        private void OnDisable()
        {
            CancelInvoke(nameof(Tick));
        }
        
        private void Tick()
        {
            if (!HasAuthority)
                return;

            if (!animator.enabled)
                return;

            //var layers = new List<AnimatorSyncPacket.LayerInfo>();
            var changes = AnimatorSyncPacket.Changes.None;
            
            /*
            for (var i = 0; i < animator.layerCount; i++)
            {
                if (!CheckAnimStateChanged(out var stateHash, out var normalizedTime, i))
                    continue;
                
                layers.Add(new AnimatorSyncPacket.LayerInfo
                {
                    StateHash = stateHash,
                    NormalizedTime = normalizedTime,
                    LayerWeight = animator.GetLayerWeight(i)
                });
                
                changes |= AnimatorSyncPacket.Changes.Layers;
            }
            */

            if (Math.Abs(_animatorSpeed - animator.speed) > 0.01f)
            {
                _animatorSpeed = animator.speed;
                changes |= AnimatorSyncPacket.Changes.Speed;
            }
            
            //Check parameters
            var changedParameters = CheckParameters(out var parameterData);
            if (changedParameters > 0)
                changes |= AnimatorSyncPacket.Changes.Parameters;


            if (changes == AnimatorSyncPacket.Changes.None) 
                return;
            
            var packet = new AnimatorSyncPacket
            {
                Id = Id,
                Change = changes,
                Layers = Array.Empty<AnimatorSyncPacket.LayerInfo>(), //layers.ToArray(),
                Speed = _animatorSpeed,
                ChangedParameters = (byte) changedParameters,
                ParameterData = parameterData
            };
            
            SendPacket(packet);
        }

        public override void OnServerReceivePacket(IOwnedPacket packet, int clientId)
        {
            switch (packet)
            {
                case AnimatorSyncPacket animatorSyncPacket:
                    if(clientId == OwnerId)
                        ServerBroadcastPacketExceptFor(animatorSyncPacket, clientId);
                    break;
                case AnimatorTriggerPacket triggerPacket:
                    if(clientId == OwnerId)
                        ServerBroadcastPacketExceptFor(triggerPacket, clientId);
                    break;
            }
        }

        public override void OnClientReceivePacket(IOwnedPacket packet)
        {
            switch (packet)
            {
                case AnimatorSyncPacket syncPacket:
                    ApplyAnimatorSyncPacket(syncPacket);
                    break;
                case AnimatorTriggerPacket triggerPacket:
                    animator.SetTrigger(triggerPacket.TriggerHash);
                    break;
            }
        }

        private int CheckParameters(out byte[] bytes)
        {
            var parameterCount = (byte) _parameters.Length;
            var writer = new BinaryWriter(new MemoryStream());
            //writer.Write(parameterCount);
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
            
            bytes = ((MemoryStream) writer.BaseStream).ToArray();
            return changed;
        }
        
        private bool CheckAnimStateChanged(out int stateHash, out float normalizedTime, int layerId)
        {
            var change = false;
            stateHash = 0;
            normalizedTime = 0;

            var lw = animator.GetLayerWeight(layerId);
            if (Mathf.Abs(lw - _layerWeight[layerId]) > 0.001f)
            {
                _layerWeight[layerId] = lw;
                change = true;
            }

            if (animator.IsInTransition(layerId))
            {
                var tt = animator.GetAnimatorTransitionInfo(layerId);
                if (tt.fullPathHash != _transitionHash[layerId])
                {
                    // first time in this transition
                    _transitionHash[layerId] = tt.fullPathHash;
                    _animationHash[layerId] = 0;
                    return true;
                }
                return change;
            }

            var st = animator.GetCurrentAnimatorStateInfo(layerId);
            if (st.fullPathHash != _animationHash[layerId])
            {
                // first time in this animation state
                if (_animationHash[layerId] != 0)
                {
                    // came from another animation directly - from Play()
                    stateHash = st.fullPathHash;
                    normalizedTime = st.normalizedTime;
                }
                _transitionHash[layerId] = 0;
                _animationHash[layerId] = st.fullPathHash;
                return true;
            }
            return change;
        }
        
        private void ApplyAnimatorSyncPacket(AnimatorSyncPacket packet)
        {
            if (packet.Id != Id)
                return;

            if ((packet.Change & AnimatorSyncPacket.Changes.Layers) != 0)
            {
                for (var i = 0; i < packet.Layers.Length; i++)
                {
                    var layer = packet.Layers[i];
                    var info = animator.GetCurrentAnimatorStateInfo(i);
                    Debug.Log($"Layer {i} - {info.fullPathHash} - {info.shortNameHash} - {layer.StateHash}");
                    if (info.fullPathHash != layer.StateHash)
                        animator.Play(layer.StateHash, i, layer.NormalizedTime);
                    animator.SetLayerWeight(i, layer.LayerWeight);
                }
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
        
        public void SetTrigger(int triggerHash)
        {
            var packet = new AnimatorTriggerPacket
            {
                Id = Id,
                TriggerHash = triggerHash
            };
            SendPacket(packet);
        }
        
        public void SetTrigger(string triggerName)
        {
            SetTrigger(Animator.StringToHash(triggerName));
            animator.SetTrigger(triggerName);
        }
        
        public float GetFloat(string name)
        {
            return animator.GetFloat(name);
        }
        
        public void SetFloat(string name, float value)
        {
            animator.SetFloat(name, value);
        }
        
        
        public float GetFloat(int nameHash)
        {
            return animator.GetFloat(nameHash);
        }
        
        public void SetFloat(int nameHash, float value)
        {
            animator.SetFloat(nameHash, value);
        }
        
        public bool GetBool(string name)
        {
            return animator.GetBool(name);
        }
        
        public void SetBool(string name, bool value)
        {
            animator.SetBool(name, value);
        }
        
        public bool GetBool(int nameHash)
        {
            return animator.GetBool(nameHash);
        }
        
        public void SetBool(int nameHash, bool value)
        {
            animator.SetBool(nameHash, value);
        }
        
        public int GetInteger(string name)
        {
            return animator.GetInteger(name);
        }
        
        public void SetInteger(string name, int value)
        {
            animator.SetInteger(name, value);
        }
        
        public int GetInteger(int nameHash)
        {
            return animator.GetInteger(nameHash);
        }
        
        public void SetInteger(int nameHash, int value)
        {
            animator.SetInteger(nameHash, value);
        }
    }

    public class AnimatorSyncPacket : IOwnedPacket
    {
        [Flags]
        public enum Changes
        {
            None = 0,
            Layers = 1,
            Parameters = 2,
            Speed = 4
        }
        
        public class LayerInfo
        {
            public int StateHash { get; set; }
            public float NormalizedTime { get; set; }
            public float LayerWeight { get; set; }
        }
        
        public NetworkId Id { get; set; }
        public Changes Change { get; set; } = Changes.None;
        public LayerInfo[] Layers { get; set; }
        public float Speed { get; set; }
        public byte ChangedParameters { get; set; }
        public byte[] ParameterData { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            
            writer.Write((byte) Change);
            
            if ((Change & Changes.Layers) != 0)
            {
                writer.Write((byte) Layers.Length);
                foreach (var layer in Layers)
                {
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
            Change = (Changes) reader.ReadByte();
            if ((Change & Changes.Layers) != 0)
            {
                var count = reader.ReadByte();
                Layers = new LayerInfo[count];
                for (var i = 0; i < count; i++)
                {
                    Layers[i] = new LayerInfo
                    {
                        StateHash = reader.ReadInt32(),
                        NormalizedTime = reader.ReadSingle(),
                        LayerWeight = reader.ReadSingle()
                    };
                }
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
    }
    
    public class AnimatorTriggerPacket : IOwnedPacket
    {
        public NetworkId Id { get; set; }
        public int TriggerHash { get; set; }

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