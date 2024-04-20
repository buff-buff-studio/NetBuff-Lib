
using System.Text.RegularExpressions;
using NetBuff.Misc;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
#endif

namespace NetBuff.Editor
{
    #if UNITY_EDITOR
    public class EditorSceneCreationSolver : AssetModificationProcessor
    {
        private static void OnWillCreateAsset(string assetName)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(assetName);
            if (type != null)
                return;
            
            if (assetName.EndsWith(".unity"))
                EditorCoroutineUtility.StartCoroutineOwnerless(WaitCreation(assetName));
        }

        private static readonly Regex _RegexGameObject = new Regex("^GameObject:.*$"); 
        private static readonly Regex _RegexMonoBehaviour = new Regex("^MonoBehaviour:.*$");
        private static readonly Regex _RegexEndSet = new Regex("^---.*$");
        private static readonly Regex _RegexGameObjectName = new Regex("^  m_Name:.*$");

        private static readonly Regex _RegexFieldId = new Regex("^  id:.*$");
        private static readonly Regex _RegexFieldHigh = new Regex("^    high:.*$");
        private static readonly Regex _RegexFieldLow = new Regex("^    low:.*$");
        private static readonly Regex _RegexFieldOwnerId = new Regex("^  ownerId:.*$");

        private static void OnCreated(string path)
        {
            Debug.Log("Detected scene creation. Re-generating ids...");
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
                                    var newId = NetworkId.New();
                                    s[i + 1] = $"    high: {newId.High}";
                                    s[i + 2] = $"    low: {newId.Low}";
                                    
                                    Debug.Log($"Generated new NetworkId for GameObject: {gameObjectName} ({newId})");
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
        }
        private static IEnumerator WaitCreation(string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
            while (type == null)
            {
                yield return null;
            }
            
            OnCreated(path);
        }
    }
    #endif
}