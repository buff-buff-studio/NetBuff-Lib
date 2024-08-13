
using System.Linq;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using AYellowpaper.SerializedCollections;
using NetBuff.Components;
using NetBuff.Misc;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using InspectorMode = NetBuff.Misc.InspectorMode;
#endif

namespace NetBuff.Editor.Windows
{
    #if UNITY_EDITOR
    public static class InspectorUtilities
    {
        [Serializable]
        public class FoldStateHolder
        {
            [SerializeField]
            public SerializedDictionary<string, bool> states = new SerializedDictionary<string, bool>();
            
            public void SetState(string path, bool state)
            {
                states[path] = state;
            }
            
            public bool GetState(string path)
            {
                return states.GetValueOrDefault(path, false);
            }
        }

        [Serializable]
        public class DrawOptions
        {
            public bool drawHeader = true;
            
            public bool drawFields = true;
            public bool drawProperties = true;
            
            public InspectorMode networkIdDrawMode = InspectorMode.Standard;

            public void LoadAttributes(MemberInfo info)
            {
                var mode = info.GetCustomAttribute<InspectorModeAttribute>();
                networkIdDrawMode = mode?.InspectorMode ?? InspectorMode.Standard;
            }
        }
        
        public static void DrawObject(string parentPath, string name, object value, FoldStateHolder fold, DrawOptions options = null)
        {
            var path = $"{parentPath}.{name}";  
            options ??= new DrawOptions();

            switch (value)
            {
                case null:
                    EditorGUILayout.LabelField(name, "null");
                    return;
                
                case string str:
                    EditorGUILayout.LabelField(name, str);
                    return;
                
                case byte byteValue:
                    EditorGUILayout.IntField(name, byteValue);
                    return;
                
                case sbyte sbyteValue:
                    EditorGUILayout.IntField(name, sbyteValue);
                    return;
                
                case short shortValue:
                    EditorGUILayout.IntField(name, shortValue);
                    return;
                
                case ushort ushortValue:
                    EditorGUILayout.IntField(name, ushortValue);
                    return;
                
                case int intValue:
                    switch (options.networkIdDrawMode)
                    {
                        case InspectorMode.Owner:
                            EditorGUILayout.TextField($"{name}", intValue == -1 ? "Server" : $"Client {intValue}");
                            return;
                        
                        case InspectorMode.Behaviour:
                            EditorGUILayout.TextField($"{name} (BH)", $"Behaviour {intValue}");
                            return;
                        
                        case InspectorMode.Scene:
                            EditorGUILayout.TextField($"{name} (Scene)", _GetSceneName(intValue));
                            return;
                        
                        default:
                            EditorGUILayout.IntField(name, intValue);
                            return;
                    }
                
                case uint uintValue:
                    EditorGUILayout.IntField(name, (int) uintValue);
                    return;
                
                case long longValue:
                    EditorGUILayout.LongField(name, longValue);
                    return;
                
                case ulong ulongValue:
                    EditorGUILayout.LongField(name, (long) ulongValue);
                    return;
                
                case float floatValue:
                    EditorGUILayout.FloatField(name, floatValue);
                    return;
                
                case double doubleValue:
                    EditorGUILayout.DoubleField(name, doubleValue);
                    return;
                
                case bool boolValue:
                    EditorGUILayout.Toggle(name, boolValue);
                    return;
                
                case Enum enumValue:
                    EditorGUILayout.EnumPopup(name, enumValue);
                    return;
                
                case Vector2 vector2Value:
                    EditorGUILayout.Vector2Field(name, vector2Value);
                    return;
                
                case Vector3 vector3Value:
                    EditorGUILayout.Vector3Field(name, vector3Value);
                    return;
                
                case Vector4 vector4Value:
                    EditorGUILayout.Vector4Field(name, vector4Value);
                    return;
                
                case Color colorValue:
                    EditorGUILayout.ColorField(name, colorValue);
                    return;
                
                case Color32 color32Value:
                    EditorGUILayout.ColorField(name, color32Value);
                    return;
                
                case Bounds boundsValue:
                    EditorGUILayout.BoundsField(name, boundsValue);
                    return;
                
                case Rect rectValue:
                    EditorGUILayout.RectField(name, rectValue);
                    return;
                
                case AnimationCurve animationCurveValue:
                    EditorGUILayout.CurveField(name, animationCurveValue);
                    return;
                
                case Gradient gradientValue:
                    EditorGUILayout.GradientField(name, gradientValue);
                    return;
                
                case LayerMask layerMaskValue:
                    EditorGUILayout.MaskField(name, layerMaskValue, InternalEditorUtility.layers);
                    return;
                
                case Quaternion quaternionValue:
                    EditorGUILayout.Vector4Field(name, new Vector4(quaternionValue.x, quaternionValue.y, quaternionValue.z, quaternionValue.w));
                    return;

                case ArraySegment<byte> a:
                {
                    var count = a.Count;
                            
                    var dataState = EditorGUILayout.BeginFoldoutHeaderGroup(fold.GetState(path), $"{name} ({count})");
                    fold.SetState(path, dataState);
                    EditorGUI.EndFoldoutHeaderGroup();
                            
                    if (!dataState) 
                        return;
                            
                    const int limitPerRow = 8;
                            
                    EditorGUILayout.BeginVertical("box");
                            
                    for (var i = 0; i < count; i++)
                    {
                        if (i % limitPerRow == 0)
                            EditorGUILayout.BeginHorizontal();
                                
                        var hex = Convert.ToString(a.Array![a.Offset + i], 16).ToUpper();
                        GUILayout.Label(hex, GUILayout.Width(40));
                                
                        if (i % limitPerRow == limitPerRow - 1 || i == count - 1)
                            EditorGUILayout.EndHorizontal();
                    }
                            
                    EditorGUILayout.EndVertical();
                }
                    return;

                case Array a:
                {
                    switch (options.networkIdDrawMode)
                    {
                        case InspectorMode.Data:
                            var count = a.Length;
                            
                            var dataState = EditorGUILayout.BeginFoldoutHeaderGroup(fold.GetState(path), $"{name} ({count})");
                            fold.SetState(path, dataState);
                            EditorGUI.EndFoldoutHeaderGroup();
                            
                            if (!dataState) 
                                return;
                            
                            const int limitPerRow = 8;
                            
                            EditorGUILayout.BeginVertical("box");
                            
                            for (var i = 0; i < count; i++)
                            {
                                if (i % limitPerRow == 0)
                                    EditorGUILayout.BeginHorizontal();
                                
                                var hex = Convert.ToString((byte) a.GetValue(i), 16).ToUpper();
                                GUILayout.Label(hex, GUILayout.Width(40));
                                
                                if (i % limitPerRow == limitPerRow - 1 || i == count - 1)
                                    EditorGUILayout.EndHorizontal();
                            }
                            
                            EditorGUILayout.EndVertical();
                            break;
                        
                        default:
                            var state = EditorGUILayout.BeginFoldoutHeaderGroup(fold.GetState(path), $"{name} ({a.Length})");
                            fold.SetState(path, state);
                            EditorGUI.EndFoldoutHeaderGroup();

                            if (!state) 
                                return;
                    
                            if (a.Length == 0)
                            {
                                EditorGUILayout.LabelField("Empty", EditorStyles.centeredGreyMiniLabel);
                                break;
                            }
                            
                            EditorGUILayout.BeginVertical("box");
                            for (var i = 0; i < a.Length; i++)
                            {
                                DrawObject(path, $"Element {i}", a.GetValue(i), fold, options);
                            }
                            EditorGUILayout.EndVertical();
                            break;
                    }
                } 
                    return;
                
                case NetworkId networkIdValue:
                    switch (options.networkIdDrawMode)
                    {
                        case InspectorMode.Standard:
                            EditorGUILayout.TextField(name, networkIdValue.ToString());
                            return;
                        case InspectorMode.Object:
                            var obj = _GetNetworkObject(networkIdValue);
                            var label = $"{name} (Object)";
                            if(obj == null)
                                EditorGUILayout.TextField(label, networkIdValue.ToString());
                            else
                                EditorGUILayout.ObjectField(label, obj, typeof(NetworkIdentity), true);
                            return;
                        case InspectorMode.Prefab:
                            var prefab = _GetNetworkPrefab(networkIdValue);
                            var prefabLabel = $"{name} (Prefab)";
                            if(prefab == null)
                                EditorGUILayout.TextField(prefabLabel, networkIdValue.ToString());
                            else
                                EditorGUILayout.ObjectField(prefabLabel, prefab, typeof(GameObject), true);
                            return;
                    }
                    return;
                
                case UnityEngine.Object objValue:
                    EditorGUILayout.ObjectField(name, objValue, objValue.GetType(), true);
                    return;
            }

            if (options.drawHeader)
            {
                var state = EditorGUILayout.BeginFoldoutHeaderGroup(fold.GetState(path), name);
                fold.SetState(path, state);
                EditorGUI.EndFoldoutHeaderGroup();
                
                if (!state)
                    return;
            }
            
            EditorGUILayout.BeginVertical("box");

            var drewSomething = false;
            if (options.drawFields)
            {
                var fields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
                foreach (var field in fields)
                {
                    var fieldValue = field.GetValue(value);
                    options.LoadAttributes(field);
                    
                    DrawObject(path, field.Name, fieldValue, fold, options);
                    drewSomething = true;
                }
            }
            
            if (options.drawProperties)
            {
                var properties = value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                foreach (var property in properties)
                {
                    if(property.GetIndexParameters().Length > 0)
                        continue;
                    var propertyValue = property.GetValue(value);
                    options.LoadAttributes(property);
                    
                    DrawObject(path, property.Name, propertyValue, fold, options);
                    drewSomething = true;
                }
            }
            
            if (!drewSomething)
                EditorGUILayout.LabelField("No Fields / Properties", EditorStyles.centeredGreyMiniLabel);
            
            EditorGUILayout.EndVertical();
        }
        
        private static GameObject _GetNetworkPrefab(NetworkId id)
        {
            var manager = NetworkManager.Instance;
            if (manager == null)
                return null;
            
            if (manager.PrefabRegistry == null)
                return null;
            
            return manager.PrefabRegistry.GetPrefab(id);
        }

        private static NetworkIdentity _GetNetworkObject(NetworkId id)
        {
            if (NetworkManager.Instance == null)
            {
                foreach (var obj in UnityEngine.Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None))
                    if (obj.Id == id)
                        return obj;
                
                return null;
            }

            return id == NetworkId.Empty ? null : NetworkManager.Instance.GetNetworkObject(id);
        }
        
        private static string _GetSceneName(int sceneIndex)
        {
            if(NetworkManager.Instance == null)
                return $"Scene {sceneIndex}";
            
            var scenes = NetworkManager.Instance.LoadedScenes.ToArray();
            if (sceneIndex < 0 || sceneIndex >= scenes.Length)
                return $"Scene {sceneIndex}";

            return scenes[sceneIndex];
        }
    }
    #endif
}