using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

/// <summary>
/// Editor helper to set up multiplayer quickly
/// </summary>
public class MultiplayerSetupHelper : EditorWindow
{
    
    [MenuItem("Tools/Multiplayer Setup Helper")]
    public static void ShowWindow()
    {
        GetWindow<MultiplayerSetupHelper>("Multiplayer Setup");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Multiplayer Setup Helper", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "Simple Multiplayer Setup - Controllers Only!\n\n" +
            "How it works:\n" +
            "• 1 controller: Normal single-player mode\n" +
            "• 2+ controllers: Multiplayer - each presses START to join\n\n" +
            "Screen splits automatically:\n" +
            "• 2 players = Top/Bottom split\n" +
            "• 4 players = Quadrant split\n\n" +
            "No keyboard/mouse in multiplayer - controllers only!", 
            MessageType.Info
        );
        
        GUILayout.Space(10);
        
        GUI.enabled = true;
        
        if (GUILayout.Button("Setup Multiplayer in Scene", GUILayout.Height(40)))
        {
            SetupMultiplayer();
        }
        
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "After setup:\n" +
            "- MultiplayerManagerSimple added to scene\n" +
            "- Auto-detects your SpawnPoint and player prefab\n" +
            "- Connect 2+ controllers and press Play!\n" +
            "- Each controller presses START to join", 
            MessageType.None
        );
    }
    
    private void SetupMultiplayer()
    {
        // Create or find MultiplayerManagerSimple
        MultiplayerManagerSimple manager = FindFirstObjectByType<MultiplayerManagerSimple>();
        
        if (manager == null)
        {
            GameObject managerObj = new GameObject("Multiplayer Manager");
            manager = managerObj.AddComponent<MultiplayerManagerSimple>();
            Undo.RegisterCreatedObjectUndo(managerObj, "Create Multiplayer Manager");
        }
        
        // Configure manager
        Undo.RecordObject(manager, "Configure Multiplayer Manager");
        manager.enableMultiplayer = true;
        
        EditorUtility.SetDirty(manager);
        
        EditorUtility.DisplayDialog(
            "Success!", 
            "Multiplayer setup complete!\n\n" +
            "How it works:\n" +
            "• 1 controller: Normal single-player\n" +
            "• 2+ controllers: Multiplayer mode\n" +
            "• Each controller presses START to join\n\n" +
            "Simple and clean!", 
            "OK"
        );
    }
    
}

