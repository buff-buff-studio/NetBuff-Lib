using System;
using System.IO;
using System.Reflection;
using NetBuff.Components;
using UnityEngine;
using UnityEngine.Serialization;

namespace NetBuff.Misc
{
    /// <summary>
    /// Base class for all network values.
    /// Used to store values that are synchronized over the network.
    /// </summary>
    [Serializable]
    public abstract class NetworkValue
    {
        /// <summary>
        /// Used to determine who can modify a value.
        /// </summary>
        public enum ModifierType
        {
            /// <summary>
            /// Only the owner of the behaviour that this value is attached to can modify it.
            /// </summary>
            OwnerOnly,
            
            /// <summary>
            /// Only the server can modify it.
            /// </summary>
            Server,
            
            /// <summary>
            /// Everybody can modify it.
            /// </summary>
            Everybody
        }

        #region Internal Fields
        protected NetworkBehaviour attachedTo;
        protected Action<NetworkValue> @delegate;
        private static MethodInfo _markValueDirtyMethod;
        #endregion
        
        /// <summary>
        /// The behaviour that this value is attached to.
        /// </summary>
        public NetworkBehaviour AttachedTo
        {
            get => attachedTo;
            set
            {
                attachedTo = value;
                _markValueDirtyMethod ??= typeof(NetworkBehaviour).GetMethod("_MarkValueDirty", BindingFlags.NonPublic | BindingFlags.Instance);
                @delegate = (Action<NetworkValue>) Delegate.CreateDelegate(typeof(Action<NetworkValue>), AttachedTo, _markValueDirtyMethod!);
            }
        }
        
        /// <summary>
        /// Serializes the value to a binary writer.
        /// </summary>
        /// <param name="writer"></param>
        public abstract void Serialize(BinaryWriter writer);

        /// <summary>
        /// Deserializes the value from a binary reader.
        /// </summary>
        /// <param name="reader"></param>
        public abstract void Deserialize(BinaryReader reader);

        #if UNITY_EDITOR
        public abstract void EditorForceUpdate();
        #endif
    }

    /// <summary>
    /// Base class for all network values.
    /// Used to store values that are synchronized over the network.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public abstract class NetworkValue<T> : NetworkValue
    {
        public delegate void ValueChangeHandler(T oldValue, T newValue);

        [FormerlySerializedAs("_value")]
        [SerializeField]
        protected T value;

        [FormerlySerializedAs("_type")]
        [SerializeField]
        protected ModifierType type;

        protected NetworkValue(T defaultValue, ModifierType type = ModifierType.OwnerOnly)
        {
            value = defaultValue;
            this.type = type;
        }

        /// <summary>
        /// The value of this network value.
        /// Can only be set if the local environment has permission.
        /// Use the CheckPermission method to check if the environment has permission.
        /// </summary>
        public T Value
        {
            get => value;

            [RequiresAuthority]
            set
            {
                if (value.Equals(this.value))
                    return;

                var man = NetworkManager.Instance;
                if (man == null || man.EnvironmentType == NetworkTransport.EnvironmentType.None)
                {
                    SetValueCalling(value);
                    return;
                }

                if (attachedTo == null)
                    throw new InvalidOperationException("This value is not attached to any NetworkBehaviour");

                if (!CheckPermission())
                    throw new InvalidOperationException("You don't have permission to modify this value");

                #if UNITY_EDITOR
                if(@delegate == null)
                    AttachedTo = attachedTo;
                #endif
                
                SetValueCalling(value);
                @delegate(this);
            }
        }

        /// <summary>
        /// Called when the value of this network value changes.
        /// </summary>
        public event ValueChangeHandler OnValueChanged;
        
        /// <summary>
        /// Checks if the environment has permission to modify this value.
        /// </summary>
        /// <returns></returns>
        public bool CheckPermission()
        {
            switch (type)
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
        /// Used to set the value of this network value.
        /// Shall only be used internally.
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

            if (attachedTo != null)
                @delegate(this);
        }
        #endif
        
        /// <summary>
        /// Returns a string representation of this network value.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"NetworkValue({value.ToString()})";
        }
    }

    /// <summary>
    /// USed to store a boolean value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class BoolNetworkValue : NetworkValue<bool>
    {
        public BoolNetworkValue(bool defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

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
    /// Used to store a byte value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class ByteNetworkValue : NetworkValue<byte>
    {
        public ByteNetworkValue(byte defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

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
    /// Used to store a int value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class IntNetworkValue : NetworkValue<int>
    {
        public IntNetworkValue(int defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type)
        {
        }

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
    /// Used to store a float value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class FloatNetworkValue : NetworkValue<float>
    {
        public FloatNetworkValue(float defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

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
    /// Used to store a double value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class DoubleNetworkValue : NetworkValue<double>
    {
        public DoubleNetworkValue(double defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

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
    /// Used to store a long value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class LongNetworkValue : NetworkValue<long>
    {
        public LongNetworkValue(long defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

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
    /// Used to store a short value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class ShortNetworkValue : NetworkValue<short>
    {
        public ShortNetworkValue(short defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

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
    /// Used to store a string value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class StringNetworkValue : NetworkValue<string>
    {
        public StringNetworkValue(string defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

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
    /// Used to store a Vector2 value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class Vector2NetworkValue : NetworkValue<Vector2>
    {
        public Vector2NetworkValue(Vector2 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(
            defaultValue, type)
        {
        }

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
    /// Used to store a Vector3 value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class Vector3NetworkValue : NetworkValue<Vector3>
    {
        public Vector3NetworkValue(Vector3 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(
            defaultValue, type)
        {
        }

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
    /// Used to store a Vector4 value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class Vector4NetworkValue : NetworkValue<Vector4>
    {
        public Vector4NetworkValue(Vector4 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(
            defaultValue, type)
        {
        }

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
    /// Used to store a Quaternion value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class QuaternionNetworkValue : NetworkValue<Quaternion>
    {
        public QuaternionNetworkValue(Quaternion defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(
            defaultValue, type)
        {
        }

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
    /// Used to store a Color value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class ColorNetworkValue : NetworkValue<Color>
    {
        public ColorNetworkValue(Color defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

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
    /// Used to store a Color32 value that is synchronized over the network.
    /// </summary>
    [Serializable]
    public class Color32NetworkValue : NetworkValue<Color32>
    {
        public Color32NetworkValue(Color32 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        public override void Deserialize(BinaryReader reader)
        {
            var r = reader.ReadByte();
            var g = reader.ReadByte();
            var b = reader.ReadByte();
            var a = reader.ReadByte();
            SetValueCalling(new Color32(r, g, b, a));
        }
    }
    
    /// <summary>
    /// Used to store a NetworkId value that is synchronized over the network.
    /// Used to keep reference to other network objets throughout the network.
    /// </summary>
    [Serializable]
    public class NetworkIdNetworkValue : NetworkValue<NetworkId>
    {
        public NetworkIdNetworkValue(NetworkId defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(
            defaultValue, type)
        {
        }

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