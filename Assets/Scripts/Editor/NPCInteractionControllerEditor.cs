using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NPCInteractionController))]
public class NPCInteractionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        NPCInteractionController npc = (NPCInteractionController)target;
        
        // Draw default inspector
        DrawDefaultInspector();
        
        // Add some spacing
        EditorGUILayout.Space();
        
        // Show scene information
        EditorGUILayout.LabelField("Scene Information", EditorStyles.boldLabel);
        
        SerializedProperty levelScenesProperty = serializedObject.FindProperty("levelScenes");
        
        for (int i = 0; i < levelScenesProperty.arraySize; i++)
        {
            SerializedProperty sceneProperty = levelScenesProperty.GetArrayElementAtIndex(i);
            if (sceneProperty.objectReferenceValue != null)
            {
                SceneAsset sceneAsset = sceneProperty.objectReferenceValue as SceneAsset;
                string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                EditorGUILayout.LabelField($"Scene {i + 1}: {sceneAsset.name}");
                EditorGUILayout.LabelField($"Path: {scenePath}", EditorStyles.miniLabel);
                EditorGUILayout.Space(5);
            }
        }
        
        // Instructions
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Drag and drop scene assets from your project into the Level Scenes array above. " +
            "The scene names will automatically be used as display names in the dialogue menu.",
            MessageType.Info
        );
    }
}