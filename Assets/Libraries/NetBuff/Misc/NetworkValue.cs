using System;
using System.IO;
using UnityEngine;
using NetBuff.Components;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NetBuff.Misc
{
    [Serializable]
    public abstract class NetworkValue
    {
        public enum ModifierType
        {
            OwnerOnly,
            Server,
            Everybody
        }

        public NetworkBehaviour AttachedTo {get; set;}

        public abstract void Serialize(BinaryWriter writer);
        public abstract void Deserialize(BinaryReader reader);
    }

    [Serializable]
    public abstract class NetworkValue<T> : NetworkValue
    {
        [SerializeField]
        protected T _value;
    
        public T Value 
        {
            get => _value;
            set
            {
                if (value.Equals(_value))
                    return;
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

        [SerializeField]
        protected ModifierType _type;

        protected NetworkValue(T defaultValue, ModifierType type = ModifierType.OwnerOnly)
        {
            this._value = defaultValue;
            this._type = type;
        }

        public bool CheckPermission()
        {
            switch(_type)
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

        protected void SetValueCalling(T newValue)
        {
            var oldValue = _value;
            _value = newValue;
            OnValueChanged?.Invoke(oldValue, newValue);
        }
    }
    
    [Serializable]
    public class BoolNetworkValue : NetworkValue<bool>
    {
        public BoolNetworkValue(bool defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var value = reader.ReadBoolean();
            SetValueCalling(value);
        }
    }
    
    [Serializable]
    public class ByteNetworkValue : NetworkValue<byte>
    {
        public ByteNetworkValue(byte defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var value = reader.ReadByte();
            SetValueCalling(value);
        }
    }

    [Serializable]
    public class IntNetworkValue : NetworkValue<int>
    {
        public IntNetworkValue(int defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var value = reader.ReadInt32();
            SetValueCalling(value);
        }
    }
    
    [Serializable]
    public class FloatNetworkValue : NetworkValue<float>
    {
        public FloatNetworkValue(float defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var value = reader.ReadSingle();
            SetValueCalling(value);
        }
    }
    
    [Serializable]
    public class DoubleNetworkValue : NetworkValue<double>
    {
        public DoubleNetworkValue(double defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var value = reader.ReadDouble();
            SetValueCalling(value);
        }
    }
    
    [Serializable]
    public class LongNetworkValue : NetworkValue<long>
    {
        public LongNetworkValue(long defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var value = reader.ReadInt64();
            SetValueCalling(value);
        }
    }
    
    [Serializable]
    public class StringNetworkValue : NetworkValue<string>
    {
        public StringNetworkValue(string defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var value = reader.ReadString();
            SetValueCalling(value);
        }
    }
    
    [Serializable]
    public class NetworkIdNetworkValue : NetworkValue<NetworkId>
    {
        public NetworkIdNetworkValue(NetworkId defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            _value.Serialize(writer);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var value = NetworkId.Read(reader);
            SetValueCalling(value);
        }
    }
    
    [Serializable]
    public class Vector2NetworkValue : NetworkValue<Vector2>
    {
        public Vector2NetworkValue(Vector2 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value.x);
            writer.Write(_value.y);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            SetValueCalling(new Vector2(x, y));
        }
    }
    
    [Serializable]
    public class Vector3NetworkValue : NetworkValue<Vector3>
    {
        public Vector3NetworkValue(Vector3 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value.x);
            writer.Write(_value.y);
            writer.Write(_value.z);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            SetValueCalling(new Vector3(x, y, z));
        }
    }
    
    [Serializable]
    public class Vector4NetworkValue : NetworkValue<Vector4>
    {
        public Vector4NetworkValue(Vector4 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value.x);
            writer.Write(_value.y);
            writer.Write(_value.z);
            writer.Write(_value.w);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            var w = reader.ReadSingle();
            SetValueCalling(new Vector4(x, y, z, w));
        }
    }
 
    [Serializable]
    public class QuaternionNetworkValue : NetworkValue<Quaternion>
    {
        public QuaternionNetworkValue(Quaternion defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value.x);
            writer.Write(_value.y);
            writer.Write(_value.z);
            writer.Write(_value.w);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var z = reader.ReadSingle();
            var w = reader.ReadSingle();
            SetValueCalling(new Quaternion(x, y, z, w));
        }
    }
    
    [Serializable]
    public class ColorNetworkValue : NetworkValue<Color>
    {
        public ColorNetworkValue(Color defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type) {}

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(_value.r);
            writer.Write(_value.g);
            writer.Write(_value.b);
            writer.Write(_value.a);
        }

        public override void Deserialize(BinaryReader reader)
        {
            if(_type == ModifierType.OwnerOnly && AttachedTo.HasAuthority)
                return;

            var r = reader.ReadSingle();
            var g = reader.ReadSingle();
            var b = reader.ReadSingle();
            var a = reader.ReadSingle();
            SetValueCalling(new Color(r, g, b, a));
        }
    }
    
    
    #if UNITY_EDITOR
    //DRAW ONLY THE _VALUE PROPERTY
    [CustomPropertyDrawer(typeof(NetworkValue<>), true)]
    public class NetworkValueDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var value = property.FindPropertyRelative("_value");
            var old = EditorStyles.label.normal.textColor;
            EditorStyles.label.normal.textColor = Color.yellow;
            EditorGUI.PropertyField(position, value, label);
            EditorStyles.label.normal.textColor = old;
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("_value"), label);
        }
    }
    #endif
}