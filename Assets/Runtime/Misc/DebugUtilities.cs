#if NETBUFF_ADVANCED_DEBUG
using System;
using UnityEngine;
#endif

namespace NetBuff.Misc
{
    public static class DebugUtilities
    {
        #if NETBUFF_ADVANCED_DEBUG
        #if !UNITY_EDITOR
        private static bool _read = false;
        #endif
                
        [Serializable]
        private class DebugSettings
        {
            public bool enableAdvancedDebugging;
            public bool defaultFillBounds;
            
            public bool networkTransformDraw = true;
            public bool networkTransformDrawSleep = true;

            public bool networkIdentityDraw = true;
            public bool networkIdentityDrawNames = true;
            public bool networkIdentityDrawBehaviourNames;
            public bool networkIdentityDrawBehaviourNamesSleep;
        }
        
        //General
        public static bool EnableAdvancedDebugging
        {
            get
            {
                _LoadDebugSettings();
                return _settings.enableAdvancedDebugging;
            }
            set
            {
                _settings.enableAdvancedDebugging = value;
                _SaveDebugSettings();
            }
        }
        
        public static bool DefaultFillBounds
        {
            get
            {
                _LoadDebugSettings();
                return _settings.defaultFillBounds;
            }
            set
            {
                _settings.defaultFillBounds = value;
                _SaveDebugSettings();
            }
        }
        
        //NetworkTransform
        public static bool NetworkTransformDraw
        {
            get
            {
                _LoadDebugSettings();
                return _settings.networkTransformDraw;
            }
            set
            {
                _settings.networkTransformDraw = value;
                _SaveDebugSettings();
            }
        }

        public static bool NetworkTransformDrawSleep
        {
            get
            {
                _LoadDebugSettings();
                return _settings.networkTransformDrawSleep;
            }
            set
            {
                _settings.networkTransformDrawSleep = value;
                _SaveDebugSettings();
            }
        }
        
        //NetworkIdentity
        public static bool NetworkIdentityDraw
        {
            get
            {
                _LoadDebugSettings();
                return _settings.networkIdentityDraw;
            }
            set
            {
                _settings.networkIdentityDraw = value;
                _SaveDebugSettings();
            }
        }
        
        public static bool NetworkIdentityDrawNames
        {
            get
            {
                _LoadDebugSettings();
                return _settings.networkIdentityDrawNames;
            }
            set
            {
                _settings.networkIdentityDrawNames = value;
                _SaveDebugSettings();
            }
        }
        
        //NetworkBehaviour
        public static bool NetworkIdentityDrawBehaviourNames
        {
            get
            {
                _LoadDebugSettings();
                return _settings.networkIdentityDrawBehaviourNames;
            }
            set
            {
                _settings.networkIdentityDrawBehaviourNames = value;
                _SaveDebugSettings();
            }
        }
        
        public static bool NetworkIdentityDrawBehaviourNamesSleep
        {
            get
            {
                _LoadDebugSettings();
                return _settings.networkIdentityDrawBehaviourNamesSleep;
            }
            set
            {
                _settings.networkIdentityDrawBehaviourNamesSleep = value;
                _SaveDebugSettings();
            }
        }

        private static DebugSettings _settings = new();
        private static Material _materialLine;
        private static Material _materialFill;
        
        public static bool DrawOutline(GameObject go, Color color, bool fill = false)
        {
            if(go.TryGetComponent(out Collider collider))
            {
                DrawOutline(collider, color, fill);
                return true;
            }
            
            if (go.TryGetComponent(out Renderer renderer))
            {
                DrawOutline(renderer, color, fill);
                return true;
            }

            return false;
        }
        
        public static void DrawOutline(Collider collider, Color color, bool fill = false)
        {
            DrawOutline(collider.bounds, color, fill);
        }
        
        public static void DrawOutline(Renderer renderer, Color color, bool fill = false)
        {
            DrawOutline(renderer.bounds, color, fill);
        }

        public static void DrawOutline(Bounds bounds, Color color, bool fill = false)
        {
            var center = bounds.center;
            var extents = bounds.extents;
            var points = new Vector3[]
            {
                new(center.x - extents.x, center.y - extents.y, center.z - extents.z),
                new(center.x + extents.x, center.y - extents.y, center.z - extents.z),
                new(center.x + extents.x, center.y + extents.y, center.z - extents.z),
                new(center.x - extents.x, center.y + extents.y, center.z - extents.z),
                new(center.x - extents.x, center.y - extents.y, center.z + extents.z),
                new(center.x + extents.x, center.y - extents.y, center.z + extents.z),
                new(center.x + extents.x, center.y + extents.y, center.z + extents.z),
                new(center.x - extents.x, center.y + extents.y, center.z + extents.z)
            };

            _CheckMaterials();

            for (var i = 0; i < 4; i++)
            {
                _DrawLine(points[i], points[(i + 1) % 4], color);
                _DrawLine(points[i + 4], points[(i + 1) % 4 + 4], color);
                _DrawLine(points[i], points[i + 4], color);
            }
            
            if (!fill)
                return;
            
            var fillColor = new Color(color.r, color.g, color.b, color.a * 0.25f);
            _DrawQuad(points[0], points[1], points[2], points[3], fillColor);
            _DrawQuad(points[4], points[5], points[6], points[7], fillColor);
            _DrawQuad(points[0], points[1], points[5], points[4], fillColor);
            _DrawQuad(points[1], points[2], points[6], points[5], fillColor);
            _DrawQuad(points[2], points[3], points[7], points[6], fillColor);
            _DrawQuad(points[3], points[0], points[4], points[7], fillColor);
        }
        
        private static void _CheckMaterials()
        {
            if (_materialLine == null)
            {
                var shaderLine = Shader.Find("Hidden/Internal-Colored");
                _materialLine = new Material(shaderLine)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                // Turn on alpha blending
                // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
                _materialLine.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
                _materialLine.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
                _materialLine.DisableKeyword("_ALPHATEST_ON");
                _materialLine.EnableKeyword("_ALPHABLEND_ON");
                _materialLine.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                _materialLine.renderQueue = 3000; // Transparent queue
            }
            
            if (_materialFill == null)
            {
                var shaderFill = Shader.Find("Hidden/Internal-Colored");
                _materialFill = new Material(shaderFill)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                
                // Turn on alpha blending
                // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
                _materialFill.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
                _materialFill.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
                _materialFill.DisableKeyword("_ALPHATEST_ON");
                _materialFill.EnableKeyword("_ALPHABLEND_ON");
                _materialFill.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                _materialFill.renderQueue = 3000; // Transparent queue
            }
        }

        private static void _DrawLine(Vector3 p1, Vector3 p2, Color color)
        {
            _materialLine.SetPass(0);
            
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex3(p1.x, p1.y, p1.z);
            GL.Vertex3(p2.x, p2.y, p2.z);
            GL.End();
        }
        
        private static void _DrawQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Color color)
        {
            _materialFill.SetPass(0);
            
            GL.Begin(GL.QUADS);
            GL.Color(color);
            GL.Vertex3(p1.x, p1.y, p1.z);
            GL.Vertex3(p2.x, p2.y, p2.z);
            GL.Vertex3(p3.x, p3.y, p3.z);
            GL.Vertex3(p4.x, p4.y, p4.z);
            GL.End();
        }
        
        private static void _LoadDebugSettings()
        {
            #if !UNITY_EDITOR
            if (_read)
                return;
            _read = true;
            #endif
            
            var path = System.IO.Path.Combine(Application.persistentDataPath, "netbuff_advanced_debug_settings.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                _settings = JsonUtility.FromJson<DebugSettings>(json);
            }
            else
            {
                _settings = new DebugSettings();
                _SaveDebugSettings();
            }
        }
        
        private static void _SaveDebugSettings()
        {
            #if UNITY_EDITOR
            var json = JsonUtility.ToJson(_settings);
            var path = System.IO.Path.Combine(Application.persistentDataPath, "netbuff_advanced_debug_settings.json");
            System.IO.File.WriteAllText(path, json);
            #endif
        }
        #endif
    }
}