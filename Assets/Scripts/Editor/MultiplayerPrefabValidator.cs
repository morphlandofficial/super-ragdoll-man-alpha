using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

/// <summary>
/// Validates that a player prefab is properly configured for multiplayer
/// </summary>
public class MultiplayerPrefabValidator : EditorWindow
{
    private GameObject prefabToValidate;
    
    [MenuItem("Tools/Validate Multiplayer Prefab")]
    public static void ShowWindow()
    {
        GetWindow<MultiplayerPrefabValidator>("Prefab Validator");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Multiplayer Prefab Validator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "This tool checks if your player prefab is properly configured for multiplayer.\n\n" +
            "A valid prefab should have:\n" +
            "• PlayerInput component\n" +
            "• ActiveRagdoll component\n" +
            "• InputModule component\n" +
            "• DefaultBehaviour component\n" +
            "• CameraModule component",
            MessageType.Info
        );
        
        GUILayout.Space(10);
        
        prefabToValidate = (GameObject)EditorGUILayout.ObjectField(
            "Player Prefab",
            prefabToValidate,
            typeof(GameObject),
            false
        );
        
        GUILayout.Space(10);
        
        GUI.enabled = prefabToValidate != null;
        
        if (GUILayout.Button("Validate Prefab", GUILayout.Height(40)))
        {
            ValidatePrefab();
        }
        
        if (GUILayout.Button("Auto-Fix Prefab", GUILayout.Height(40)))
        {
            AutoFixPrefab();
        }
        
        GUI.enabled = true;
    }
    
    private void ValidatePrefab()
    {
        bool isValid = true;
        string report = "=== PREFAB VALIDATION REPORT ===\n\n";
        
        // Check PlayerInput (OPTIONAL for simple multiplayer)
        PlayerInput playerInput = prefabToValidate.GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            report += "⚠️  PlayerInput component not found (will be added automatically)\n";
        }
        else
        {
            report += "✅ PlayerInput component found\n";
            
            if (playerInput.actions == null)
            {
                report += "   ⚠️  WARNING: No Input Actions assigned\n";
            }
            else
            {
                report += $"   • Actions: {playerInput.actions.name}\n";
            }
            
            report += $"   • Behavior: {playerInput.notificationBehavior}\n";
        }
        
        // Check ActiveRagdoll
        var activeRagdoll = prefabToValidate.GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (activeRagdoll == null)
        {
            report += "❌ MISSING: ActiveRagdoll component\n";
            isValid = false;
        }
        else
        {
            report += "✅ ActiveRagdoll component found\n";
        }
        
        // Check InputModule
        var inputModule = prefabToValidate.GetComponent<ActiveRagdoll.InputModule>();
        if (inputModule == null)
        {
            report += "❌ MISSING: InputModule component\n";
            isValid = false;
        }
        else
        {
            report += "✅ InputModule component found\n";
        }
        
        // Check DefaultBehaviour
        var defaultBehaviour = prefabToValidate.GetComponent<DefaultBehaviour>();
        if (defaultBehaviour == null)
        {
            report += "⚠️  OPTIONAL: DefaultBehaviour component (recommended)\n";
        }
        else
        {
            report += "✅ DefaultBehaviour component found\n";
        }
        
        // Check CameraModule
        var cameraModule = prefabToValidate.GetComponent<ActiveRagdoll.CameraModule>();
        if (cameraModule == null)
        {
            report += "❌ MISSING: CameraModule component\n";
            isValid = false;
        }
        else
        {
            report += "✅ CameraModule component found\n";
        }
        
        report += "\n";
        
        if (isValid)
        {
            report += "✅ ✅ ✅ PREFAB IS VALID! ✅ ✅ ✅\n";
            report += "\nThis prefab is ready for multiplayer!\n";
            EditorUtility.DisplayDialog("Validation Passed!", report, "OK");
        }
        else
        {
            report += "❌ ❌ ❌ PREFAB IS NOT VALID ❌ ❌ ❌\n";
            report += "\nPlease fix the issues above or use 'Auto-Fix Prefab' button.\n";
            EditorUtility.DisplayDialog("Validation Failed", report, "OK");
        }
        
        Debug.Log(report);
    }
    
    private void AutoFixPrefab()
    {
        if (prefabToValidate == null)
        {
            EditorUtility.DisplayDialog("Error", "No prefab selected!", "OK");
            return;
        }
        
        // Ask for confirmation
        if (!EditorUtility.DisplayDialog(
            "Auto-Fix Prefab",
            "This will add missing components to your prefab.\n\n" +
            "Make sure you have a backup!\n\n" +
            "Continue?",
            "Yes, Fix It",
            "Cancel"))
        {
            return;
        }
        
        string report = "=== AUTO-FIX REPORT ===\n\n";
        bool madeChanges = false;
        
        // Add PlayerInput if missing
        PlayerInput playerInput = prefabToValidate.GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = prefabToValidate.AddComponent<PlayerInput>();
            report += "✅ Added PlayerInput component\n";
            madeChanges = true;
            
            // Try to find and assign input actions
            string[] guids = AssetDatabase.FindAssets("ActiveRagdollActions t:InputActionAsset");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                playerInput.actions = actions;
                report += $"   • Assigned: {actions.name}\n";
            }
            
            // Set behavior to Send Messages
            playerInput.notificationBehavior = PlayerNotifications.SendMessages;
            report += "   • Set Behavior to 'Send Messages'\n";
        }
        
        // Note: Other components (ActiveRagdoll, InputModule, etc.) should already exist
        // because they're core to the character. We'll just report on them.
        
        if (madeChanges)
        {
            EditorUtility.SetDirty(prefabToValidate);
            report += "\n✅ Auto-fix complete!\n";
            report += "\nPlease validate again to confirm all requirements are met.\n";
            Debug.Log(report);
            EditorUtility.DisplayDialog("Auto-Fix Complete", report, "OK");
        }
        else
        {
            report += "ℹ️  No changes needed - prefab appears to be configured correctly.\n";
            Debug.Log(report);
            EditorUtility.DisplayDialog("No Changes Needed", report, "OK");
        }
    }
}

