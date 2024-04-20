using NetBuff.Components;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NetBuff.Editor
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(NetworkIdentity))]
    public class NetworkIdentityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
            EditorGUI.EndDisabledGroup();
            DrawPropertiesExcluding(serializedObject, "id", "m_Script", "ownerId", "prefabId");
            serializedObject.ApplyModifiedProperties();
        }
    }
    #endif
}