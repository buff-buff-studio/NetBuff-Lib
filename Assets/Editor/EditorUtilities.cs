using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AYellowpaper.SerializedCollections;
using NetBuff.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using NetBuff.Misc;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace NetBuff.Editor
{
    #if UNITY_EDITOR
    [InitializeOnLoad]
    public static class EditorUtilities
    {
        private static readonly Regex _RegexGameObject = new Regex("^GameObject:.*$"); 
        private static readonly Regex _RegexMonoBehaviour = new Regex("^MonoBehaviour:.*$");
        private static readonly Regex _RegexEndSet = new Regex("^---.*$");
        private static readonly Regex _RegexGameObjectName = new Regex("^  m_Name:.*$");

        private static readonly Regex _RegexFieldId = new Regex("^  id:.*$");
        private static readonly Regex _RegexFieldHigh = new Regex("^    high:.*$");
        private static readonly Regex _RegexFieldLow = new Regex("^    low:.*$");
        private static readonly Regex _RegexFieldOwnerId = new Regex("^  ownerId:.*$");
        
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
                EditorGUILayout.LabelField("Scene ", GUILayout.Width(40.0f));
                var newSceneIndex = EditorGUILayout.Popup(sceneIndex, _scenes.Select(x => x.name).ToArray(), GUILayout.Width(150.0f));
                EditorGUILayout.EndHorizontal();
                if (newSceneIndex == sceneIndex) return;
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                    EditorSceneManager.OpenScene(_scenes[newSceneIndex].path, OpenSceneMode.Single);
                }
            }
        }
        
        [MenuItem("NetBuff/Check Prefab Registries", priority = 1)]
        public static void CheckPrefabs()
        {
            Debug.Log("Checking prefab registries");
            var search = AssetDatabase.FindAssets("t:NetworkPrefabRegistry");

            foreach (var guid in search)
            {
                var npr = AssetDatabase.LoadAssetAtPath<NetworkPrefabRegistry>(AssetDatabase.GUIDToAssetPath(guid));
                var nprDict = npr.Prefabs;
                var field = nprDict.GetType().GetField("_serializedList", BindingFlags.NonPublic | BindingFlags.Instance);
                var list = (List<SerializedKeyValuePair<NetworkId, GameObject>>) field!.GetValue(nprDict);
                
                var foundKeys = new List<NetworkId>();

                bool hasProblem = false;
                void ShowProblem(string error)
                {
                    Debug.LogError($"Problem found in {npr.name}: {error}");
                    hasProblem = true;
                }

                void Finish()
                {
                    if (!hasProblem) return;
                    Selection.activeObject = npr;
                    EditorGUIUtility.PingObject(npr);
                }
                
                foreach (var tt in list)
                {
                    if (foundKeys.Contains(tt.Key))
                    {
                        ShowProblem($"Duplicate key {tt.Key}");
                    }

                    var prefab = tt.Value;
                    
                    if(prefab == null)
                    {
                        ShowProblem($"Prefab {tt.Key} is null");
                        continue;
                    }
                    
                    var networkIdentities = prefab.GetComponentsInChildren<NetworkIdentity>(true);

                    if (networkIdentities.Length == 0)
                    {
                        ShowProblem($"Prefab {tt.Key} ({prefab.name}) has not network identity");
                        continue;
                    }  
                    
                    if (networkIdentities.Length > 1)
                    {
                        ShowProblem($"Prefab {tt.Key} ({prefab.name}) has more than one network identity");
                        continue;
                    }
                    
                    foundKeys.Add(tt.Key);
                }

                Finish();
            }
            
            //close progress bar
            Debug.Log("Finished checking prefab registries");
        }

        [MenuItem("NetBuff/Solve Scene Internal Network Id Duplicates", priority = 100)]
        public static void SolveCurrentSceneDuplicates()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                 //get current scene asset path
                var scenePath = SceneManager.GetActiveScene().path;
                var controlGroup = new List<NetworkId>();
                _SolveSceneIdDuplicates(scenePath, controlGroup);
            }
        }
        
        [MenuItem("NetBuff/Solve All Scene Network Id Duplicates", priority = 101)]
        public static void SolveAllSceneDuplicates()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                //get all scenes
                var scenes = EditorBuildSettings.scenes;
                var controlGroup = new List<NetworkId>();
                foreach (var scene in scenes)
                {
                    if (scene.path == null || scene.path.StartsWith("Assets") == false)
                        continue;

                    var scenePath = scene.path;
                    _SolveSceneIdDuplicates(scenePath, controlGroup);
                }
            }
        }
        
        [MenuItem("NetBuff/Regenerate Network Ids For All Scenes", priority = 200)]
        public static void RegenerateNetworkIdForAllScenes()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                //get all scenes
                var scenes = EditorBuildSettings.scenes;
                var controlGroup = new List<NetworkId>();
                foreach (var scene in scenes)
                {
                    if (scene.path == null || scene.path.StartsWith("Assets") == false)
                        continue;

                    var scenePath = scene.path;
                    _RegenerateIds(scenePath, controlGroup);
                }
            }
        }

        private static void _SolveSceneIdDuplicates(string path, List<NetworkId> controlGroup)
        {
            Debug.Log("Solving scene NetworkId duplicates: " + path);
            var s = File.ReadAllLines(path);
            var i = 0;
            var found = false;
            
            while (i < s.Length)
            {
                if (_RegexGameObject.IsMatch(s[i]))
                {
                    string gameObjectName = null;
                    var insideMonoBehaviour = false;

                    i++;
                    while (i < s.Length)
                    {
                        if (_RegexGameObject.IsMatch(s[i]))
                            break;
                        
                        if (_RegexGameObjectName.IsMatch(s[i]))
                        {
                            if(gameObjectName == null)
                                gameObjectName = s[i].Substring(10);
                        }
                        
                        if (_RegexMonoBehaviour.IsMatch(s[i]))
                            insideMonoBehaviour = true;

                        if (_RegexEndSet.IsMatch(s[i]))
                            insideMonoBehaviour = false;

                        if (insideMonoBehaviour)
                        {
                            if(_RegexFieldId.IsMatch(s[i]) && i + 3 < s.Length)
                            {
                                if (_RegexFieldHigh.IsMatch(s[i + 1]) && _RegexFieldLow.IsMatch(s[i + 2]) && _RegexFieldOwnerId.IsMatch(s[i + 3]))
                                {
                                    var high = int.Parse(s[i + 1].Substring(10));
                                    var low = int.Parse(s[i + 2].Substring(9));
                                    
                                    var id = new NetworkId(high, low);

                                    if (controlGroup.Contains(id))
                                    {
                                        found = true;
                                        Debug.LogError($"Detected duplicate NetworkId for GameObject: {gameObjectName}: ({id})");

                                        do
                                        {
                                            id = NetworkId.New();
                                        }
                                        while (controlGroup.Contains(id));
                                        
                                        s[i + 1] = $"    high: {id.High}";
                                        s[i + 2] = $"    low: {id.Low}";
                                    }
                                    
                                    controlGroup.Add(id);
                                }
                            }
                        }
                        
                        i++;
                    }
                }
                else
                    i++;
            }
           
            if (found)
            {
                File.WriteAllLines(path, s);
                AssetDatabase.ImportAsset(path);
                if (SceneManager.GetActiveScene().path == path)
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }
        
        private static void _RegenerateIds(string path, List<NetworkId> controlGroup)
        {
            Debug.Log("Regenerating scene NetworkIds: " + path);
            var s = File.ReadAllLines(path);
            var i = 0;
            
            while (i < s.Length)
            {
                if (_RegexGameObject.IsMatch(s[i]))
                {
                    string gameObjectName = null;
                    var insideMonoBehaviour = false;

                    i++;
                    while (i < s.Length)
                    {
                        if (_RegexGameObject.IsMatch(s[i]))
                            break;
                        
                        if (_RegexGameObjectName.IsMatch(s[i]))
                        {
                            if(gameObjectName == null)
                                gameObjectName = s[i].Substring(10);
                        }
                        
                        if (_RegexMonoBehaviour.IsMatch(s[i]))
                            insideMonoBehaviour = true;

                        if (_RegexEndSet.IsMatch(s[i]))
                            insideMonoBehaviour = false;

                        if (insideMonoBehaviour)
                        {
                            if(_RegexFieldId.IsMatch(s[i]) && i + 3 < s.Length)
                            {
                                if (_RegexFieldHigh.IsMatch(s[i + 1]) && _RegexFieldLow.IsMatch(s[i + 2]) && _RegexFieldOwnerId.IsMatch(s[i + 3]))
                                {
                                    NetworkId id;
                                    do
                                    {
                                        id = NetworkId.New();
                                    }
                                    while (controlGroup.Contains(id));
                                    
                                    s[i + 1] = $"    high: {id.High}";
                                    s[i + 2] = $"    low: {id.Low}";
                                    
                                    controlGroup.Add(id);
                                }
                            }
                        }
                        
                        i++;
                    }
                }
                else
                    i++;
            }

            File.WriteAllLines(path, s);
            AssetDatabase.ImportAsset(path);
            if (SceneManager.GetActiveScene().path == path)
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
    #endif
}