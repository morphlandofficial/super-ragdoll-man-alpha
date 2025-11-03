using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for LevelManager - adds helpful buttons for managing levels
/// </summary>
[CustomEditor(typeof(LevelManager))]
public class LevelManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Level Management Tools", EditorStyles.boldLabel);
        
        LevelManager manager = (LevelManager)target;
        
        // ═══════════════════════════════════════════════════════
        // DISCOVER LEVELS BUTTON
        // ═══════════════════════════════════════════════════════
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🔍 Discover Levels from EARTH", GUILayout.Height(30)))
        {
            manager.DiscoverLevelsFromEarth();
            EditorUtility.SetDirty(manager); // Mark as dirty so changes are saved
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.HelpBox(
            "🔍 INITIAL SETUP ONLY:\n" +
            "Use this button to populate the level list from EARTH hierarchy.\n" +
            "Creates new level entries with DEFAULT unlock states (all unlocked).\n\n" +
            "⚠️ After clicking, configure which levels should be LOCKED in the list above.\n" +
            "💡 At runtime, the system only refreshes portal links (preserves your lock settings).",
            MessageType.Warning
        );
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.HelpBox(
            "PORTAL VISIBILITY: Portal zones can be left active in the Editor for design purposes. " +
            "At RUNTIME, the LevelManager will automatically show/hide them based on unlock states.",
            MessageType.Info
        );
        
        EditorGUILayout.Space(10);
        
        // ═══════════════════════════════════════════════════════
        // RESET PROGRESS BUTTON (Dangerous!)
        // ═══════════════════════════════════════════════════════
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("⚠️ Reset All Saved Progress (PlayerPrefs)", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog(
                "Reset All Saved Progress?",
                "This will delete ALL saved level progress from PlayerPrefs.\n\n" +
                "Note: This only matters if 'Enable Save System' is checked.\n\n" +
                "This action cannot be undone!",
                "Reset Everything",
                "Cancel"))
            {
                manager.ResetAllProgress();
                EditorUtility.SetDirty(manager);
                Debug.Log("[LevelManager] All saved progress reset!");
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.HelpBox(
            "💡 SAVE SYSTEM TOGGLE:\n" +
            "Check 'Enable Save System' above to save progress to PlayerPrefs.\n" +
            "Uncheck it for fresh testing every game run (no persistence).",
            MessageType.Info
        );
    }
}

