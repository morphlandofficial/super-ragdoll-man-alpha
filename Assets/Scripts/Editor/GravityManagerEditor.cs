using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for GravityManager to provide quick-action buttons and better UI
/// </summary>
[CustomEditor(typeof(GravityManager))]
public class GravityManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();
        
        GravityManager gravityManager = (GravityManager)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        // Only show these buttons when in play mode
        if (Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Apply Gravity"))
            {
                gravityManager.ApplyGravity();
            }
            
            if (GUILayout.Button("Restore Original"))
            {
                gravityManager.RestoreOriginalGravity();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Toggle Zero-G"))
            {
                gravityManager.ToggleZeroGravity();
            }
            
            if (GUILayout.Button("Half Gravity"))
            {
                gravityManager.MultiplyGravity(0.5f);
            }
            
            if (GUILayout.Button("Double Gravity"))
            {
                gravityManager.MultiplyGravity(2.0f);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Display current physics gravity
            EditorGUILayout.LabelField("Current Physics Gravity:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"X: {Physics.gravity.x:F2}, Y: {Physics.gravity.y:F2}, Z: {Physics.gravity.z:F2}");
            
            if (gravityManager.IsTransitioning)
            {
                EditorGUILayout.HelpBox("Gravity is currently transitioning...", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test gravity controls", MessageType.Info);
        }
        
        EditorGUILayout.Space(10);
        
        // Quick preset buttons for easy setup
        EditorGUILayout.LabelField("Quick Preset Setup", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🌊 Underwater"))
        {
            SerializedProperty presetProp = serializedObject.FindProperty("gravityPreset");
            presetProp.enumValueIndex = (int)GravityManager.GravityPreset.Underwater;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("🌍 Earth"))
        {
            SerializedProperty presetProp = serializedObject.FindProperty("gravityPreset");
            presetProp.enumValueIndex = (int)GravityManager.GravityPreset.Earth;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("🌙 Moon"))
        {
            SerializedProperty presetProp = serializedObject.FindProperty("gravityPreset");
            presetProp.enumValueIndex = (int)GravityManager.GravityPreset.Moon;
            serializedObject.ApplyModifiedProperties();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🚀 Zero-G"))
        {
            SerializedProperty presetProp = serializedObject.FindProperty("gravityPreset");
            presetProp.enumValueIndex = (int)GravityManager.GravityPreset.ZeroGravity;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("⬇️ High Gravity"))
        {
            SerializedProperty presetProp = serializedObject.FindProperty("gravityPreset");
            presetProp.enumValueIndex = (int)GravityManager.GravityPreset.HighGravity;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("✏️ Custom"))
        {
            SerializedProperty presetProp = serializedObject.FindProperty("gravityPreset");
            presetProp.enumValueIndex = (int)GravityManager.GravityPreset.Custom;
            serializedObject.ApplyModifiedProperties();
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Tips section
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "💡 Tips for Tide Pool (Fish Tank) Scene:\n" +
            "• Use 'Underwater' preset for a floaty feel\n" +
            "• Enable smooth transition for gradual gravity changes\n" +
            "• Combine with drag on Rigidbodies for better water simulation\n" +
            "• Consider adding buoyancy scripts to objects",
            MessageType.Info
        );
    }
}




