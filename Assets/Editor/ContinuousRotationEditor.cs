using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ContinuousRotation))]
public class ContinuousRotationEditor : Editor
{
    private void OnSceneGUI()
    {
        ContinuousRotation rotationScript = (ContinuousRotation)target;
        
        if (rotationScript.useManualAnchorPoint)
        {
            EditorGUI.BeginChangeCheck();
            
            // Draw a position handle at the anchor point
            Vector3 newAnchorPosition = Handles.PositionHandle(
                rotationScript.manualAnchorPoint,
                Quaternion.identity
            );
            
            if (EditorGUI.EndChangeCheck())
            {
                // Record undo state
                Undo.RecordObject(rotationScript, "Move Anchor Point");
                
                // Update the anchor point
                rotationScript.manualAnchorPoint = newAnchorPosition;
                
                // Mark as dirty to save changes
                EditorUtility.SetDirty(rotationScript);
            }
            
            // Draw a label near the anchor point
            Handles.Label(
                rotationScript.manualAnchorPoint + Vector3.up * 0.5f,
                "Rotation Anchor",
                new GUIStyle()
                {
                    normal = new GUIStyleState() { textColor = Color.yellow },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                }
            );
        }
    }
}

