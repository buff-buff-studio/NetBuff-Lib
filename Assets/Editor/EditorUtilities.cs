using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace NetBuff.Editor
{
    #if UNITY_EDITOR
    [InitializeOnLoad]
    public static class EditorUtilities
    {
        private struct Scene
        {
            public string name;
            public string path;
        }

        private static List<Scene> _scenes = new List<Scene>();
        private static ScriptableObject _toolbar;

        static EditorUtilities()
        {
            EditorApplication.delayCall += () =>
            {
                EditorApplication.update -= _Update;
                EditorApplication.update += _Update;
            };
        }

        private static void _Update()
        {
            if (_toolbar != null)
                return;
            
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var toolbars = Resources.FindObjectsOfTypeAll(editorAssembly.GetType("UnityEditor.Toolbar"));
            _toolbar = toolbars.Length > 0 ? (ScriptableObject)toolbars[0] : null;
            if (_toolbar == null) 
                return;
            
            var root = _toolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            if (root == null) 
                return;
            
            var rawRoot = root.GetValue(_toolbar);
            var mRoot = rawRoot as VisualElement;
            
            var elm = mRoot.Q("ToolbarZoneLeftAlign");
            var container = new IMGUIContainer();
            container.onGUIHandler += OnGUI;
            elm.Add(container);
        }
        
        private static void OnGUI()
        {
            _scenes.Clear();
            foreach (var scene in EditorBuildSettings.scenes) 
            {
                if (scene.path == null || scene.path.StartsWith("Assets") == false)
                    continue;

                var scenePath = Application.dataPath + scene.path.Substring(6);
                _scenes.Add(new Scene { name = Path.GetFileNameWithoutExtension(scenePath), path = scenePath });
            }
            
            var sceneName  = SceneManager.GetActiveScene().name;
            var sceneIndex = -1;

            for (var i = 0; i < _scenes.Count; ++i)
            {
                if (sceneName != _scenes[i].name) 
                    continue;
                sceneIndex = i;
                break;
            }
            
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Scene: ", GUILayout.Width(45.0f));
                var newSceneIndex = EditorGUILayout.Popup(sceneIndex, _scenes.Select(x => x.name).ToArray(), GUILayout.Width(150.0f));
                EditorGUILayout.EndHorizontal();
                if (newSceneIndex == sceneIndex) return;
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                    EditorSceneManager.OpenScene(_scenes[newSceneIndex].path, OpenSceneMode.Single);
                }
            }
        }
    }
    #endif
}