using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using NetBuff.Components;
using UnityEngine;

namespace NetBuff.Misc
{
    #region Base Types
    [Serializable]
    public abstract class NetworkValue
    {
        public enum ModifierType
        {
            OwnerOnly,

            Server,

            Everybody
        }

        public NetworkBehaviour AttachedTo
        {
            get => attachedTo;
            set
            {
                attachedTo = value;
                _markValueDirtyMethod ??= typeof(NetworkBehaviour).GetMethod("_MarkValueDirty",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                @delegate = (Action<NetworkValue>)Delegate.CreateDelegate(typeof(Action<NetworkValue>), AttachedTo,
                    _markValueDirtyMethod!);
            }
        }

        public abstract void Serialize(BinaryWriter writer);

        public abstract void Deserialize(BinaryReader reader, bool callCallback);

        public abstract bool CheckPermission();

        #if UNITY_EDITOR
        public abstract void EditorForceUpdate();
        #endif

        #region Internal Fields
        protected NetworkBehaviour attachedTo;
        protected Action<NetworkValue> @delegate;
        private static MethodInfo _markValueDirtyMethod;
        #endregion
    }

    [Serializable]
    public abstract class NetworkValue<T> : NetworkValue
    {
        public delegate void ValueChangeHandler(T oldValue, T newValue);
        
        [SerializeField]
        protected T value;
        
        [SerializeField]
        protected ModifierType type;

        protected NetworkValue()
        {
            
        }
        
        protected NetworkValue(T defaultValue, ModifierType type = ModifierType.OwnerOnly)
        {
            value = defaultValue;
            this.type = type;
        }

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
                if (@delegate == null)
                    AttachedTo = attachedTo;
                #endif
                    
                SetValueCalling(value);
                @delegate!.Invoke(this);
            }
        }

        public event ValueChangeHandler OnValueChanged;
        
        public override bool CheckPermission()
        {
            #if UNITY_EDITOR
            if(!Application.isPlaying)
                return true;

            return type switch
            {
                ModifierType.OwnerOnly => AttachedTo != null && AttachedTo.HasAuthority,
                ModifierType.Server => NetworkManager.Instance != null && NetworkManager.Instance.IsServerRunning,
                ModifierType.Everybody => true,
                _ => false
            };
            #else
            return type switch
            {
                ModifierType.OwnerOnly => AttachedTo.HasAuthority,
                ModifierType.Server => NetworkManager.Instance.IsServerRunning,
                ModifierType.Everybody => true,
                _ => false
            };
            #endif
        }

        public abstract T Deserialize(BinaryReader reader);
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            if (attachedTo == null)
                return;
            
            if (@delegate == null)
                AttachedTo = attachedTo;
            
            // ReSharper disable once PossibleNullReferenceException
            @delegate(this);
        }
        #endif

        public override void Deserialize(BinaryReader reader, bool callCallback)
        {
            var v = Deserialize(reader);
            if(callCallback)
                SetValueCalling(v);
            else
                value = v;
        }

        public override string ToString()
        {
            return $"NetworkValue({value.ToString()})";
        }
    }
    
    [Serializable]
    public abstract class NetworkIdentityBasedValue<T> : NetworkValue
    {
        public delegate void ValueChangeHandler(T oldValue, T newValue);

        [SerializeField]
        protected NetworkId value;

        [SerializeField]
        protected ModifierType type;

        public NetworkId RawValue
        {
            get => value;
            set => SetValueCalling(value);
        }
        
        public T Value
        {
            get => GetFromNetworkId(value);
            
            [RequiresAuthority]
            set
            {
                var cmp = GetFromNetworkId(this.value);
                if (value == null ? cmp == null : value.Equals(cmp))
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
                if (@delegate == null)
                    AttachedTo = attachedTo;
                #endif

                SetValueCalling(value);
                @delegate?.Invoke(this);
            }
        }

        public event ValueChangeHandler OnValueChanged;
        
        protected NetworkIdentityBasedValue(ModifierType type = ModifierType.OwnerOnly)
        {
            this.type = type;
        }

        protected abstract T GetFromNetworkId(NetworkId id);
        
        protected abstract NetworkId GetNetworkId(T value);
        
        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override void Deserialize(BinaryReader reader, bool callCallback)
        {
            var v = reader.ReadNetworkId();
            if(callCallback)
                SetValueCalling(v);
            else
                value = v;
        }
        
        public override bool CheckPermission()
        {
            #if UNITY_EDITOR
            if(!Application.isPlaying)
                return true;

            return type switch
            {
                ModifierType.OwnerOnly => AttachedTo != null && AttachedTo.HasAuthority,
                ModifierType.Server => NetworkManager.Instance != null && NetworkManager.Instance.IsServerRunning,
                ModifierType.Everybody => true,
                _ => false
            };
            #else
            return type switch
            {
                ModifierType.OwnerOnly => AttachedTo.HasAuthority,
                ModifierType.Server => NetworkManager.Instance.IsServerRunning,
                ModifierType.Everybody => true,
                _ => false
            };
            #endif
        }
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetValueCalling(T newValue)
        {
            var oldValue = GetFromNetworkId(value);
            value = GetNetworkId(newValue);

            OnValueChanged?.Invoke(oldValue, newValue);
        }
        
        protected void SetValueCalling(NetworkId newValue)
        {
            var oldValue = GetFromNetworkId(value);
            value = newValue;
            OnValueChanged?.Invoke(oldValue, GetFromNetworkId(value));
        }

        #if UNITY_EDITOR
        public override void EditorForceUpdate()
        {
            SetValueCalling(GetFromNetworkId(value));

            if (attachedTo != null)
                @delegate(this);
        }
        #endif

        public override string ToString()
        {
            return $"NetworkValue({value})";
        }

        #region Internal Util Methods
        protected static NetworkIdentity GetNetworkObject(NetworkId id)
        {
            #if UNITY_EDITOR
            if (NetworkManager.Instance == null)
            {
                foreach (var obj in UnityEngine.Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None))
                    if (obj.Id == id)
                        return obj;
                
                return null;
            }
            #endif
            
            return id == NetworkId.Empty ? null : NetworkManager.Instance.GetNetworkObject(id);
        }
        #endregion
    }
    #endregion

    #region Primite Types
    [Serializable]
    public class BoolNetworkValue : NetworkValue<bool>
    {
        public BoolNetworkValue()
        {
            
        }
        
        public BoolNetworkValue(bool defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override bool Deserialize(BinaryReader reader)
        {
            return reader.ReadBoolean();
        }
    }

    [Serializable]
    public class ByteNetworkValue : NetworkValue<byte>
    {
        public ByteNetworkValue()
        {
            
        }
        
        public ByteNetworkValue(byte defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override byte Deserialize(BinaryReader reader)
        {
            return reader.ReadByte();
        }
    }

    [Serializable]
    public class IntNetworkValue : NetworkValue<int>
    {
        public IntNetworkValue()
        {
            
        }
        
        public IntNetworkValue(int defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override int Deserialize(BinaryReader reader)
        {
            return reader.ReadInt32();
        }
    }

    [Serializable]
    public class FloatNetworkValue : NetworkValue<float>
    {
        public FloatNetworkValue()
        {
            
        }
        
        public FloatNetworkValue(float defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override float Deserialize(BinaryReader reader)
        {
            return reader.ReadSingle();
        }
    }

    [Serializable]
    public class DoubleNetworkValue : NetworkValue<double>
    {
        public DoubleNetworkValue()
        {
            
        }
        
        public DoubleNetworkValue(double defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override double Deserialize(BinaryReader reader)
        {
            return reader.ReadDouble();
        }
    }

    [Serializable]
    public class LongNetworkValue : NetworkValue<long>
    {
        public LongNetworkValue()
        {
            
        }
        
        public LongNetworkValue(long defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override long Deserialize(BinaryReader reader)
        {
            return reader.ReadInt64();
        }
    }

    [Serializable]
    public class ShortNetworkValue : NetworkValue<short>
    {
        public ShortNetworkValue()
        {
        }

        public ShortNetworkValue(short defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override short Deserialize(BinaryReader reader)
        {
            return reader.ReadInt16();
        }
    }
    #endregion

    #region Enum Types
    [Serializable]
    public class EnumNetworkValue<T> : NetworkValue<T> where T : Enum
    {
        public EnumNetworkValue()
        {
            
        }
        
        public EnumNetworkValue(T defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue, type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(Convert.ToInt32(value));
        }
        
        public override T Deserialize(BinaryReader reader)
        {
            return (T)Enum.ToObject(typeof(T), reader.ReadInt32());
        }
    }
    #endregion

    #region Built-In Types
    [Serializable]
    public class StringNetworkValue : NetworkValue<string>
    {
        public StringNetworkValue()
        {
            
        }
        
        public StringNetworkValue(string defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(defaultValue,
            type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override string Deserialize(BinaryReader reader)
        {
            return reader.ReadString();
        }
    }
    #endregion

    #region Unity Built-In Types
    [Serializable]
    public class Vector2NetworkValue : NetworkValue<Vector2>
    {
        public Vector2NetworkValue()
        {
            
        }
        
        public Vector2NetworkValue(Vector2 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(
            defaultValue, type)
        {
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        public override Vector2 Deserialize(BinaryReader reader)
        {
            return new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }
    }

    [Serializable]
    public class Vector3NetworkValue : NetworkValue<Vector3>
    {
        public Vector3NetworkValue()
        {
            
        }
        
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

        public override Vector3 Deserialize(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    [Serializable]
    public class Vector4NetworkValue : NetworkValue<Vector4>
    {
        public Vector4NetworkValue()
        {
            
        }
        
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

        public override Vector4 Deserialize(BinaryReader reader)
        {
            return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    [Serializable]
    public class QuaternionNetworkValue : NetworkValue<Quaternion>
    {
        public QuaternionNetworkValue()
        {
            
        }
        
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

        public override Quaternion Deserialize(BinaryReader reader)
        {
            return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    [Serializable]
    public class ColorNetworkValue : NetworkValue<Color>
    {
        public ColorNetworkValue()
        {
            
        }
        
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

        public override Color Deserialize(BinaryReader reader)
        {
            return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }

    [Serializable]
    public class Color32NetworkValue : NetworkValue<Color32>
    {
        public Color32NetworkValue()
        {
        }
        
        public Color32NetworkValue(Color32 defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(
            defaultValue,
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

        public override Color32 Deserialize(BinaryReader reader)
        {
            return new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()); 
        }
    }
    #endregion

    #region NetBuff Simple Types
    [Serializable]
    public class NetworkIdNetworkValue : NetworkValue<NetworkId>
    {
        public NetworkIdNetworkValue()
        {
        }
        
        public NetworkIdNetworkValue(NetworkId defaultValue, ModifierType type = ModifierType.OwnerOnly) : base(
            defaultValue, type)
        {
        }
        

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }

        public override NetworkId Deserialize(BinaryReader reader)
        {
            return reader.ReadNetworkId();
        }
    }
    #endregion
    
    #region NetBuff Complex Types
    [Serializable]
    public class NetworkIdentityNetworkValue : NetworkIdentityBasedValue<NetworkIdentity>
    {
        public NetworkIdentityNetworkValue(NetworkIdentity defaultValue = null, ModifierType type = ModifierType.OwnerOnly) : base(type)
        {
            value = defaultValue == null ? NetworkId.Empty : defaultValue.Id;
        }
        
        protected override NetworkIdentity GetFromNetworkId(NetworkId id)
        {
            return id == NetworkId.Empty ? null : GetNetworkObject(id);
        }

        protected override NetworkId GetNetworkId(NetworkIdentity v)
        {
            return v == null ? NetworkId.Empty : v.Id;
        }
    }
    
    [Serializable]
    public class NetworkBehaviourNetworkValue<T> : NetworkIdentityBasedValue<T> where T : NetworkBehaviour 
    {
        public NetworkBehaviourNetworkValue(T defaultValue = null, ModifierType type = ModifierType.OwnerOnly) : base(type)
        {
            value = defaultValue == null ? NetworkId.Empty : defaultValue.Id;
        }
        
        protected override T GetFromNetworkId(NetworkId id)
        {
            return id == NetworkId.Empty ? null : GetNetworkObject(id).GetComponent<T>();
        }
        
        protected override NetworkId GetNetworkId(T v)
        {
            return v == null ? NetworkId.Empty : v.Id;
        }
    }
    
    [Serializable]
    public class GameObjectNetworkValue : NetworkIdentityBasedValue<GameObject>
    {
        public GameObjectNetworkValue(GameObject defaultValue = null, ModifierType type = ModifierType.OwnerOnly) : base(type)
        {
            value = defaultValue == null ? NetworkId.Empty : defaultValue.GetComponent<NetworkIdentity>().Id;
        }
        
        protected override GameObject GetFromNetworkId(NetworkId id)
        {
            return id == NetworkId.Empty ? null : GetNetworkObject(id).gameObject;
        }

        protected override NetworkId GetNetworkId(GameObject v)
        {
            if (v == null)
                return null;
            
            var cmp = v.GetComponent<NetworkIdentity>();
            if (cmp == null)
                throw new InvalidOperationException("The component must be attached to a GameObject with a NetworkIdentity component");
            
            return cmp.Id;
        }
    }
    
    [Serializable]
    public class ComponentNetworkValue<T> : NetworkIdentityBasedValue<T> where T : Component
    {
        public ComponentNetworkValue(T defaultValue = null, ModifierType type = ModifierType.OwnerOnly) : base(type)
        {
            value = defaultValue == null ? NetworkId.Empty : defaultValue.GetComponent<NetworkIdentity>().Id;
        }
        
        protected override T GetFromNetworkId(NetworkId id)
        {
            return id == NetworkId.Empty ? null : GetNetworkObject(id).GetComponent<T>();
        }

        protected override NetworkId GetNetworkId(T v)
        {
            if (v == null)
                return null;
            
            var cmp = v.GetComponent<NetworkIdentity>();
            if (cmp == null)
                throw new InvalidOperationException("The component must be attached to a GameObject with a NetworkIdentity component");
            
            return cmp.Id;
        }
    }
    #endregion
}