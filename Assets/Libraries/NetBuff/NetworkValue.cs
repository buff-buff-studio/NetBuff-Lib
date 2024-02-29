using System;
using System.IO;
using UnityEngine;
using NetBuff.Components;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace NetBuff
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

    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(NetworkValue), true)]
    public class NetworkValuePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueProperty = property.FindPropertyRelative("_value");
            //make content yellow
            var before = EditorStyles.label.normal.textColor;
            EditorStyles.label.normal.textColor = Color.yellow;
            var content =  $"{label.text} (NV)";
            EditorGUI.PropertyField(position, valueProperty, new GUIContent(content, label.tooltip));
            EditorStyles.label.normal.textColor = before;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("_value"), label);
        }
    }
    #endif
}