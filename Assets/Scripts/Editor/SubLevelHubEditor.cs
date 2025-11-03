using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SubLevelHub))]
public class SubLevelHubEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        // Show helpful info
        EditorGUILayout.HelpBox(
            "🏆 SUB-LEVEL HUB TRACKER + UNLOCKING\n\n" +
            "This component tracks progress of child levels AND unlocks other levels!\n\n" +
            "SETUP:\n" +
            "1. Hub Scene Name auto-fills from current scene\n" +
            "2. NPC Controller auto-finds the NPCInteractionController\n" +
            "3. Child levels extracted from NPC Controller automatically\n" +
            "4. Configure Bronze/Silver/Gold thresholds\n" +
            "5. Drag scene assets to unlock at each tier\n\n" +
            "EXAMPLE:\n" +
            "• Bronze = Any child completed\n" +
            "• Silver = All children have Silver+\n" +
            "• Gold = All children have Gold\n\n" +
            "When Silver is reached → Unlocks specified scenes!\n\n" +
            "This hub checks progress automatically when you return!",
            MessageType.Info
        );
        
        EditorGUILayout.Space(10);
        
        // Force check button
        if (Application.isPlaying)
        {
            SubLevelHub hub = (SubLevelHub)target;
            
            if (GUILayout.Button("🔄 Force Check Progress & Trigger Unlocks", GUILayout.Height(30)))
            {
                hub.ForceCheckProgress();
                EditorUtility.DisplayDialog(
                    "Progress Checked!",
                    "Hub progress has been recalculated.\nCheck the console for detailed status.",
                    "OK"
                );
            }
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("📊 Show Current Hub Achievement", GUILayout.Height(25)))
            {
                Achievement currentAchievement = hub.GetHubAchievement();
                EditorUtility.DisplayDialog(
                    "Current Hub Achievement",
                    $"This hub's calculated achievement:\n\n{currentAchievement}\n\nCheck console for child level details.",
                    "OK"
                );
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test progress checking and unlocking.", MessageType.Info);
        }
    }
}

