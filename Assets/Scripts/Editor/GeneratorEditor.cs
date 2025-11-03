using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for Generator to add helpful buttons and preview functionality.
/// </summary>
[CustomEditor(typeof(Generator))]
public class GeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        Generator generator = (Generator)target;
        
        // Add some spacing
        EditorGUILayout.Space(10);
        
        // Add a header
        EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);
        
        // Add info box
        EditorGUILayout.HelpBox(
            "Use these buttons to test generation in Edit Mode. " +
            "In Play Mode, objects will generate automatically at Start.", 
            MessageType.Info);
        
        EditorGUILayout.Space(5);
        
        // Create a horizontal layout for buttons
        EditorGUILayout.BeginHorizontal();
        
        // Generate button
        if (GUILayout.Button("Generate in Editor", GUILayout.Height(30)))
        {
            if (!Application.isPlaying)
            {
                generator.Generate();
            }
            else
            {
// Debug.LogWarning("Use this button in Edit Mode only. In Play Mode, generation happens automatically.");
            }
        }
        
        // Clear button
        if (GUILayout.Button("Clear All", GUILayout.Height(30)))
        {
            if (!Application.isPlaying)
            {
                generator.ClearGeneratedObjects();
            }
            else
            {
// Debug.LogWarning("Use this button in Edit Mode only.");
            }
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Add tip
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "💡 Tip: Position, rotate, and scale this GameObject to place the spawn volume. " +
            "The cyan box gizmo shows where objects will spawn.", 
            MessageType.None);
    }
}

