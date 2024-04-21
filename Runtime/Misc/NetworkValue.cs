using System;
using System.IO;
using UnityEngine;
using NetBuff.Components;
using UnityEngine.Serialization;

namespace NetBuff.Misc
{
    /// <summary>
    /// Base non-generic class for all network values
    /// </summary>
    [Serializable]
    public abstract class NetworkValue
    {
        public enum ModifierType
        {
            OwnerOnly,
            Server,
            Everybody
        }
        
        /// <summary>
        /// Returns the NetworkBehaviour this value is attached to
        /// </summary>
        public NetworkBehaviour AttachedTo {get; set;}
        
        /// <summary>
        /// Serializes the value to a binary writer
        /// </summary>
        /// <param name="writer"></param>
        public abstract void Serialize(BinaryWriter writer);
        
        /// <summary>
        /// Deserializes the value from a binary reader
        /// </summary>
        /// <param name="reader"></param>
        public abstract void Deserialize(BinaryReader reader);
        
        #if UNITY_EDITOR
        public abstract void EditorForceUpdate();
        #endif
    }
    
    /// <summary>
    /// Base generic class for all network values
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public abstract class NetworkValue<T> : NetworkValue
    {
        /// <summary>
        /// Internally stored value
        /// </summary>
        [FormerlySerializedAs("_value")] 
        [SerializeField]
        protected T value;
    
        /// <summary>
        /// Handles the value of the network value, handling permissions and network updates
        /// </summary>
        public T Value 
        {
            get => value;
            set
            {
                if (value.Equals(this.value))
                    return;

                var man = NetworkManager.Instance;
                if (man == null || man.EndType == NetworkTransport.EndType.None)
                {
                    SetValueCalling(value);
                    return;
                }
                
                if(AttachedTo == null)
                    throw new InvalidOperationException("This value is not attached to any NetworkBehaviour");
                
                if(!CheckPermission())
                    throw new InvalidOperationException("You don't have permission to modify this value");
                
                SetValueCalling(value);
                AttachedTo.MarkValueDirty(this);
            }
        }

        public delegate void ValueChangeHandler(T oldValue, T newValue);
        public event ValueChangeHandler OnValueChanged;

        /// <summary>
        /// Returns the type of permission required to modify the value
        /// </summary>
        [FormerlySerializedAs("_type")] 
        [SerializeField]
        protected ModifierType type;

        protected NetworkValue(T defaultValue, ModifierType type = ModifierType.OwnerOnly)
        {
            value = defaultValue;
            this.type = type;
        }
        
        /// <summary>
        /// Checks if the network end has permission to modify the value
        /// </summary>
        /// <returns></returns>
        public bool CheckPermission()
        {
            switch(type)
            {
                case ModifierType.OwnerOnly:
                    return AttachedTo.HasAuthority;
                case ModifierType.Server:
                    return NetworkManager.Instance.IsServerRunning;
                case ModifierType.Everybody:
                    return true;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Used internally to set the value and invoke the OnValueChanged event
        /// </summary>
        /// <param name="newValue"></param>
        protected void SetValueCalling(T newValue)
        {
            var oldValue = value;
            value = newValue;
            
            OnValueChanged?.Invoke(oldValue, newValue);
        }
        
        #if UNITY_EDITOR
        public override void EditorForceUpdate()
        {
            SetValueCalling(value);
            
            if(AttachedTo != null)
                AttachedTo.MarkValueDirty(this);
        }
        #endif

        public override string ToString()
        {
            return $"NetworkValue({value.ToString()})";
        }
    }
    
    /// <summary>
    /// Handles a network value of type bool
    /// </summary>
    [Serializable]
    public class BoolNetworkValue : NetworkValue<bool>
    {
        public BoolNetworkValue(bool defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = reader.ReadBoolean();
            SetValueCalling(v);
        }
    }
    
    /// <summary>
    /// Handles a network value of type byte
    /// </summary>
    [Serializable]
    public class ByteNetworkValue : NetworkValue<byte>
    {
        public ByteNetworkValue(byte defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = reader.ReadByte();
            SetValueCalling(v);
        }
    }
    
    /// <summary>
    /// Handles a network value of type sbyte
    /// </summary>
    [Serializable]
    public class IntNetworkValue : NetworkValue<int>
    {
        public IntNetworkValue(int defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = reader.ReadInt32();
            SetValueCalling(v);
        }
    }
    
    /// <summary>
    /// Handles a network value of type float
    /// </summary>
    [Serializable]
    public class FloatNetworkValue : NetworkValue<float>
    {
        public FloatNetworkValue(float defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = reader.ReadSingle();
            SetValueCalling(v);
        }
    }
    
    /// <summary>
    /// Handles a network value of type double
    /// </summary>
    [Serializable]
    public class DoubleNetworkValue : NetworkValue<double>
    {
        public DoubleNetworkValue(double defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = reader.ReadDouble();
            SetValueCalling(v);
        }
    }
    
    /// <summary>
    /// Handles a network value of type long
    /// </summary>
    [Serializable]
    public class LongNetworkValue : NetworkValue<long>
    {
        public LongNetworkValue(long defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = reader.ReadInt64();
            SetValueCalling(v);
        }
    }
    
    /// <summary>
    /// Handles a network value of type short
    /// </summary>
    [Serializable]
    public class ShortNetworkValue : NetworkValue<short>
    {
        public ShortNetworkValue(short defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = reader.ReadInt16();
            SetValueCalling(v);
        }
    }
    
    /// <summary>
    /// Handles a network value of type string
    /// </summary>
    [Serializable]
    public class StringNetworkValue : NetworkValue<string>
    {
        public StringNetworkValue(string defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = reader.ReadString();
            SetValueCalling(v);
        }
    }
 
    /// <summary>
    /// Handles a network value of type Vector2
    /// </summary>
    [Serializable]
    public class Vector2NetworkValue : NetworkValue<Vector2>
    {
        public Vector2NetworkValue(Vector2 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            SetValueCalling(new Vector2(x, y));
        }
    }
    
    /// <summary>
    /// Handles a network value of type Vector3
    /// </summary>
    [Serializable]
    public class Vector3NetworkValue : NetworkValue<Vector3>
    {
        public Vector3NetworkValue(Vector3 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            SetValueCalling(new Vector3(x, y, z));
        }
    }
    
    /// <summary>
    /// Handles a network value of type Vector4
    /// </summary>
    [Serializable]
    public class Vector4NetworkValue : NetworkValue<Vector4>
    {
        public Vector4NetworkValue(Vector4 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            var w = reader.ReadSingle();
            SetValueCalling(new Vector4(x, y, z, w));
        }
    }
 
    /// <summary>
    /// Handles a network value of type Quaternion
    /// </summary>
    [Serializable]
    public class QuaternionNetworkValue : NetworkValue<Quaternion>
    {
        public QuaternionNetworkValue(Quaternion defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            var w = reader.ReadSingle();
            SetValueCalling(new Quaternion(x, y, z, w));
        }
    }
    
    /// <summary>
    /// Handles a network value of type Color
    /// </summary>
    [Serializable]
    public class ColorNetworkValue : NetworkValue<Color>
    {
        public ColorNetworkValue(Color defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var r = reader.ReadSingle();
            var g = reader.ReadSingle();
            var b = reader.ReadSingle();
            var a = reader.ReadSingle();
            SetValueCalling(new Color(r, g, b, a));
        }
    }
    
    /// <summary>
    /// Handles a network value of type NetworkId
    /// </summary>
    [Serializable]
    public class NetworkIdNetworkValue : NetworkValue<NetworkId>
    {
        public NetworkIdNetworkValue(NetworkId defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            value.Serialize(writer);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var v = NetworkId.Read(reader);
            SetValueCalling(v);
        }
    }
}