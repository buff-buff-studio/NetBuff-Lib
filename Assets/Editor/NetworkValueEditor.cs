
using System;
using System.Reflection;
using NetBuff.Misc;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NetBuff.Editor
{
    #if UNITY_EDITOR
    
    [CustomPropertyDrawer(typeof(NetworkValue<>), true)]
    public class NetworkValueDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var value = property.FindPropertyRelative("value");
            var type = property.FindPropertyRelative("type");
            
            //first rect 3/3
            //last rect 1/3
            var valueRect = new Rect(position.x, position.y, position.width * 3 / 4, position.height);
            var typeRect = new Rect(position.x + position.width * 3 / 4, position.y, position.width / 4, position.height);
            
            EditorGUI.BeginChangeCheck();
            EditorStyles.label.normal.textColor = Color.yellow;
            EditorGUI.PropertyField(valueRect, value, label);
            EditorStyles.label.normal.textColor = Color.white;
            
            EditorGUI.PropertyField(typeRect, type, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.ApplyModifiedProperties();
                (GetTargetObjectOfProperty(property) as NetworkValue)!.EditorForceUpdate();
            }
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
    }
    #endif
}