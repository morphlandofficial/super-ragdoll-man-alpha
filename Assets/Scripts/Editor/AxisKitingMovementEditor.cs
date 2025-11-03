using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for AxisKitingMovement to provide better UI and quick actions
/// </summary>
[CustomEditor(typeof(AxisKitingMovement))]
public class AxisKitingMovementEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        DrawDefaultInspector();
        
        AxisKitingMovement kitingMovement = (AxisKitingMovement)target;
        
        EditorGUILayout.Space(10);
        
        // Runtime info
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField($"Current Distance: {kitingMovement.CurrentDistance:F2}");
            EditorGUILayout.LabelField($"Is Moving: {(kitingMovement.IsMoving ? "Yes" : "No")}");
            EditorGUILayout.LabelField($"Target: {(kitingMovement.Target != null ? kitingMovement.Target.name : "None")}");
            EditorGUILayout.LabelField($"Target Prefab: {(kitingMovement.TargetPrefab != null ? kitingMovement.TargetPrefab.name : "None")}");
            EditorGUILayout.LabelField($"Movement Axis: {kitingMovement.CurrentAxis}");
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            // Quick action buttons
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Refresh Target"))
            {
                kitingMovement.RefreshTarget();
            }
            
            if (GUILayout.Button("Find Player"))
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    kitingMovement.SetTarget(player.transform);
                    Debug.Log($"Set target to: {player.name}");
                }
                else
                {
                    Debug.LogWarning("No GameObject with 'Player' tag found");
                }
            }
            
            if (GUILayout.Button("Toggle Enable"))
            {
                kitingMovement.SetEnabled(!kitingMovement.enabled);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Distance +5"))
            {
                kitingMovement.SetTargetDistance(kitingMovement.CurrentDistance + 5f);
            }
            
            if (GUILayout.Button("Distance -5"))
            {
                kitingMovement.SetTargetDistance(Mathf.Max(0, kitingMovement.CurrentDistance - 5f));
            }
            
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see runtime info and controls", MessageType.Info);
        }
        
        EditorGUILayout.Space(10);
        
        // Quick preset buttons
        EditorGUILayout.LabelField("Quick Axis Setup", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("↔️ X Axis"))
        {
            SerializedProperty axisProp = serializedObject.FindProperty("movementAxis");
            axisProp.enumValueIndex = (int)AxisKitingMovement.MovementAxis.X;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("↕️ Y Axis"))
        {
            SerializedProperty axisProp = serializedObject.FindProperty("movementAxis");
            axisProp.enumValueIndex = (int)AxisKitingMovement.MovementAxis.Y;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("↗️ Z Axis"))
        {
            SerializedProperty axisProp = serializedObject.FindProperty("movementAxis");
            axisProp.enumValueIndex = (int)AxisKitingMovement.MovementAxis.Z;
            serializedObject.ApplyModifiedProperties();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Distance presets
        EditorGUILayout.LabelField("Distance Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Near (5)"))
        {
            SerializedProperty distProp = serializedObject.FindProperty("targetDistance");
            distProp.floatValue = 5f;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("Mid (10)"))
        {
            SerializedProperty distProp = serializedObject.FindProperty("targetDistance");
            distProp.floatValue = 10f;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("Far (20)"))
        {
            SerializedProperty distProp = serializedObject.FindProperty("targetDistance");
            distProp.floatValue = 20f;
            serializedObject.ApplyModifiedProperties();
        }
        
        if (GUILayout.Button("Very Far (50)"))
        {
            SerializedProperty distProp = serializedObject.FindProperty("targetDistance");
            distProp.floatValue = 50f;
            serializedObject.ApplyModifiedProperties();
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Tips section
        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "💡 Usage Tips:\n" +
            "• Perfect for objects like the moon that should follow along one axis\n" +
            "• Target Prefab: Drag your player prefab here to auto-find spawned instances\n" +
            "• Search Method: Choose how to find the prefab (ByName is most reliable)\n" +
            "• Target Distance: How far the object stays from the target\n" +
            "• Distance Threshold: Dead zone where no movement occurs\n" +
            "• Use smooth movement for gentle, organic motion\n" +
            "• Enable constraints to limit movement range\n" +
            "• Turn on Show Debug to visualize behavior in Scene view\n" +
            "• Use 'Refresh Target' button if target spawns after scene load",
            MessageType.Info
        );
    }
}

