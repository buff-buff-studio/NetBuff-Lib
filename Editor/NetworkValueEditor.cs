


using NetBuff.Components;
using Unity.VisualScripting;
#if UNITY_EDITOR
using System;
using System.Reflection;
using NetBuff.Misc;

using UnityEngine;
using UnityEditor;
#endif

namespace NetBuff.Editor
{
    #if UNITY_EDITOR
    public abstract class BaseNetworkValueDrawer : PropertyDrawer
    {
        public abstract void HandleField(SerializedProperty value, Rect rect, SerializedProperty property,
            GUIContent label);
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var networkValue = GetTargetObjectOfProperty(property) as NetworkValue;
            
            var value = property.FindPropertyRelative("value");
            var type = property.FindPropertyRelative("type");
            
            var valueRect = new Rect(position.x, position.y, position.width * 3 / 4, position.height);
            var typeRect = new Rect(position.x + position.width * 3 / 4, position.y, position.width / 4, position.height);
            
            EditorGUI.BeginDisabledGroup(!networkValue?.CheckPermission() ?? true);
            
            EditorGUI.BeginChangeCheck();
            EditorStyles.label.normal.textColor = Color.yellow;

            HandleField(value, valueRect, property, label);
            
            EditorStyles.label.normal.textColor = Color.white;
            
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            EditorGUI.PropertyField(typeRect, type, GUIContent.none);
            EditorGUI.EndDisabledGroup();
            
            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.ApplyModifiedProperties();
                networkValue!.EditorForceUpdate();
            }
            
            EditorGUI.EndDisabledGroup();
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("value"), label);
        }
        
        /// <summary>
        /// Gets the object the property represents.
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element[..element.IndexOf("[", StringComparison.Ordinal)];
                    var index = Convert.ToInt32(element[element.IndexOf("[", StringComparison.Ordinal)..].Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }
        
        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }
        
        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            for (var i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }
        
        #region Internal Util Methods
        /// <summary>
        ///     Returns the NetworkIdentity object from the given NetworkId.
        ///     Will return null if the NetworkId is empty.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
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

    #region Types
    [CustomPropertyDrawer(typeof(NetworkValue<>), true)]
    public class NetworkValueDrawer : BaseNetworkValueDrawer
    {
        public override void HandleField(SerializedProperty value, Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(rect, value, label, true);
        }
    }
    
    [CustomPropertyDrawer(typeof(NetworkBehaviourNetworkValue<>), true)]
    public class NetworkBehaviourNetworkValueDrawer : BaseNetworkValueDrawer
    {
        public override void HandleField(SerializedProperty value, Rect rect, SerializedProperty property, GUIContent label)
        {
            var id = (NetworkId) value.GetUnderlyingValue();
            
            var type = property.GetUnderlyingType();
            var genericBehaviourType = type.GetGenericArguments()[0];
            var identity = GetNetworkObject(id);
            var behaviour = identity == null ? null : identity.GetComponent(genericBehaviourType);
            var newValue = EditorGUI.ObjectField(rect, label, behaviour, genericBehaviourType, true) as NetworkBehaviour;
            var newId = newValue == null ? NetworkId.Empty : newValue.Id;
            
            if (id != newId)
                value.SetUnderlyingValue(newId);
        }
    }

    [CustomPropertyDrawer(typeof(NetworkIdentityNetworkValue), true)]
    public class NetworkIdentityNetworkValueDrawer : BaseNetworkValueDrawer
    {
        public override void HandleField(SerializedProperty value, Rect rect, SerializedProperty property, GUIContent label)
        {
            var id = (NetworkId) value.GetUnderlyingValue();
            
            var identity = GetNetworkObject(id);
            var newValue = EditorGUI.ObjectField(rect, label, identity, typeof(NetworkIdentity), true) as NetworkIdentity;
            var newId = newValue == null ? NetworkId.Empty : newValue.Id;

            if (id != newId)
                value.SetUnderlyingValue(newId);
        }
    }
    #endregion
    #endif
}