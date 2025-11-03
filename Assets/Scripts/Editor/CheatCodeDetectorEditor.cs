using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for CheatCodeDetector to add helper info
/// </summary>
[CustomEditor(typeof(CheatCodeDetector))]
public class CheatCodeDetectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "📍 SETUP: Attach to the Level Manager GameObject.\n\n" +
            "🎮 CHEAT CODE SEQUENCE:\n\n" +
            "1. Hold ButtonNorth (Y/Triangle - Ragdoll Mode)\n" +
            "2. While holding, quickly press:\n" +
            "   L2 → L1 → R2 → R1 → L2 → L1 → R2 → R1 → L2 → L1 → R2 → R1\n\n" +
            "✅ R1 respawn is disabled while ragdoll is held!\n" +
            "🔊 Drag an audio clip to play a sound on unlock!\n\n" +
            "⚠️ Controller only.\n" +
            "💡 Enable Debug Mode to see progress.",
            MessageType.Info
        );
    }
}

