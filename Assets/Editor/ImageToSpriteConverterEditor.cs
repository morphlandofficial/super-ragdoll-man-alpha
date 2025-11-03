using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ImageToSpriteConverter))]
public class ImageToSpriteConverterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();

        // Add some space
        EditorGUILayout.Space(10);

        // Get reference to the target script
        ImageToSpriteConverter converter = (ImageToSpriteConverter)target;

        // Disable buttons during play mode
        bool isPlayMode = Application.isPlaying;
        
        if (isPlayMode)
        {
            EditorGUILayout.HelpBox("Editor-only tool: Buttons are disabled during play mode.", MessageType.Info);
        }

        // Disable GUI if in play mode
        EditorGUI.BeginDisabledGroup(isPlayMode);

        // Create the "Create Sprite" button
        if (GUILayout.Button("Create Sprite", GUILayout.Height(30)))
        {
            converter.ConvertToSprite();
        }

        // Add some space
        EditorGUILayout.Space(5);

        // Create the "Clear Sprite" button
        if (GUILayout.Button("Clear Sprite", GUILayout.Height(25)))
        {
            converter.ClearSprite();
        }

        EditorGUI.EndDisabledGroup();
    }
}

