using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom inspector for SpawnPoint with conditional skin selection for Default Character
/// </summary>
[CustomEditor(typeof(SpawnPoint))]
public class SpawnPointEditor : Editor
{
    private SpawnPoint spawnPoint;
    
    private void OnEnable()
    {
        spawnPoint = (SpawnPoint)target;
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // Record undo state before making changes
        Undo.RecordObject(spawnPoint, "Modify Spawn Point");
        
        // Track if any changes are made
        EditorGUI.BeginChangeCheck();
        
        // SPAWN SETTINGS
        EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        spawnPoint.playerPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Player Prefab", "The player prefab to spawn"), 
            spawnPoint.playerPrefab, 
            typeof(GameObject), 
            false
        );
        
        // Check if prefab has DefaultBehaviour (is a player character)
        bool isPlayerCharacter = false;
        string[] costumeNames = null;
        
        if (spawnPoint.playerPrefab != null)
        {
            DefaultBehaviour defaultBehaviour = spawnPoint.playerPrefab.GetComponent<DefaultBehaviour>();
            isPlayerCharacter = (defaultBehaviour != null);
            
            // If it's a player character, find all costume hierarchies
            if (isPlayerCharacter)
            {
                costumeNames = GetCostumeNames(spawnPoint.playerPrefab);
            }
        }
        
        // Only show Costume Selection if it's a player character with multiple costumes
        if (isPlayerCharacter && costumeNames != null && costumeNames.Length > 0)
        {
            EditorGUILayout.Space(5);
            
            // Show costume dropdown
            int newCostumeIndex = EditorGUILayout.Popup(
                new GUIContent("Spawn Costume", "Which costume should the character spawn with?"),
                spawnPoint.selectedCostumeIndex,
                costumeNames
            );
            
            // Update if changed
            if (newCostumeIndex != spawnPoint.selectedCostumeIndex)
            {
                spawnPoint.selectedCostumeIndex = newCostumeIndex;
                spawnPoint.selectedCostumeName = costumeNames[newCostumeIndex];
            }
            
            // Show info box
            if (costumeNames.Length > 1)
            {
                EditorGUILayout.HelpBox($"✓ Will spawn with: {costumeNames[spawnPoint.selectedCostumeIndex]}", MessageType.Info);
            }
        }
        
        EditorGUILayout.Space(5);
        
        spawnPoint.spawnOffset = EditorGUILayout.Vector3Field(
            new GUIContent("Spawn Offset", "Offset from spawner position"), 
            spawnPoint.spawnOffset
        );
        
        spawnPoint.useRotation = EditorGUILayout.Toggle(
            new GUIContent("Use Rotation", "Player matches spawner's rotation"), 
            spawnPoint.useRotation
        );
        
        spawnPoint.spawnOnStart = EditorGUILayout.Toggle(
            new GUIContent("Spawn On Start", "Auto-spawn at level start"), 
            spawnPoint.spawnOnStart
        );
        
        spawnPoint.forceSpawnIfExists = EditorGUILayout.Toggle(
            new GUIContent("Force Spawn If Exists", "Spawn even if player already exists"), 
            spawnPoint.forceSpawnIfExists
        );
        
        EditorGUILayout.Space(15);
        
        // RACE SETTINGS
        EditorGUILayout.LabelField("Race Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        EditorGUI.BeginDisabledGroup(!isPlayerCharacter);
        spawnPoint.enableRaceMode = EditorGUILayout.Toggle(
            new GUIContent("Enable Race Mode", "Enable race mode for spawned player"), 
            spawnPoint.enableRaceMode
        );
        EditorGUI.EndDisabledGroup();
        
        if (spawnPoint.enableRaceMode && !isPlayerCharacter)
        {
            EditorGUILayout.HelpBox("⚠️ Race mode requires a prefab with DefaultBehaviour component!", MessageType.Warning);
        }
        
        EditorGUILayout.Space(15);
        
        // GIZMO SETTINGS
        EditorGUILayout.LabelField("Gizmo Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        spawnPoint.gizmoColor = EditorGUILayout.ColorField(
            new GUIContent("Gizmo Color", "Color of spawn point gizmo"), 
            spawnPoint.gizmoColor
        );
        
        spawnPoint.gizmoSize = EditorGUILayout.FloatField(
            new GUIContent("Gizmo Size", "Size of spawn point gizmo"), 
            spawnPoint.gizmoSize
        );
        
        // Mark object as dirty if any changes were made
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(spawnPoint);
            // Also mark scene as dirty to ensure it saves
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(spawnPoint.gameObject.scene);
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    /// <summary>
    /// Get all costume names from the player prefab
    /// </summary>
    private string[] GetCostumeNames(GameObject prefab)
    {
        if (prefab == null) return new string[0];
        
        System.Collections.Generic.List<string> costumeNames = new System.Collections.Generic.List<string>();
        
        // Look through all direct children of the prefab
        foreach (Transform child in prefab.transform)
        {
            // Skip camera and other systems
            if (child.name.Contains("Camera") || child.name.Contains("System"))
                continue;
            
            // Check if it has Animated/Physical structure (costume hierarchy)
            Transform animated = child.Find("Animated");
            Transform physical = child.Find("Physical");
            
            if (animated != null && physical != null)
            {
                costumeNames.Add(child.name);
            }
        }
        
        return costumeNames.ToArray();
    }
}

