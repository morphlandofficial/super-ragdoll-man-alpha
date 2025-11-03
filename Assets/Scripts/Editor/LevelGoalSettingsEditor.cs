using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for LevelGoalSettings to add helpful context
/// </summary>
[CustomEditor(typeof(LevelGoalSettings))]
public class LevelGoalSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "💡 LEVEL UNLOCKING - HOW TO USE:\n\n" +
            "1. Drag SCENE ASSETS from your Project window (not GameObjects!)\n" +
            "2. These will communicate with Level Manager to unlock the matching portals\n\n" +
            "✨ You can unlock MULTIPLE levels per achievement!\n" +
            "Example: Drag 5 scene assets into Bronze to unlock 5 levels at once.\n\n" +
            "Perfect for tutorial → main game transitions!",
            MessageType.Info
        );
    }
}

