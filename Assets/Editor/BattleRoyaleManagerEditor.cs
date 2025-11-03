using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(BattleRoyaleManager))]
public class BattleRoyaleManagerEditor : Editor
{
    private SerializedProperty gameMode;
    private SerializedProperty requireManualFinish;
    private SerializedProperty livesMode;
    private SerializedProperty maxLives;
    private SerializedProperty maxActiveRagdollsGlobal;
    private SerializedProperty spawnerObjects;
    private SerializedProperty useWaveSystem;
    private SerializedProperty numberOfWaves;
    private SerializedProperty spawnerWaveAssignments;
    private SerializedProperty waves;
    private SerializedProperty showValidationWarnings;
    
    private void OnEnable()
    {
        gameMode = serializedObject.FindProperty("gameMode");
        requireManualFinish = serializedObject.FindProperty("requireManualFinish");
        livesMode = serializedObject.FindProperty("livesMode");
        maxLives = serializedObject.FindProperty("maxLives");
        maxActiveRagdollsGlobal = serializedObject.FindProperty("maxActiveRagdollsGlobal");
        spawnerObjects = serializedObject.FindProperty("spawnerObjects");
        useWaveSystem = serializedObject.FindProperty("useWaveSystem");
        numberOfWaves = serializedObject.FindProperty("numberOfWaves");
        spawnerWaveAssignments = serializedObject.FindProperty("spawnerWaveAssignments");
        waves = serializedObject.FindProperty("waves");
        showValidationWarnings = serializedObject.FindProperty("showValidationWarnings");
        
        // Initialize wave assignments based on existing data
        InitializeWaveAssignments();
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // Header
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("BATTLE ROYALE MANAGER", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        // Game Mode
        EditorGUILayout.LabelField("═══ GAME MODE ═══", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(gameMode);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("═══ BATTLE ROYALE OPTIONS ═══", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(requireManualFinish);
        
        // Lives System
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("═══ LIVES SYSTEM ═══", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(livesMode);
        EditorGUILayout.PropertyField(maxLives);
        
        // Spawner Settings
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("═══ SPAWNER SETTINGS ═══", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxActiveRagdollsGlobal, new GUIContent("Max Active Ragdolls (Global)"));
        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(spawnerObjects, new GUIContent("Spawner Objects"), true);
        
        // Check if spawner array changed
        if (GUI.changed && !useWaveSystem.boolValue)
        {
            // If spawners changed and waves aren't enabled, just apply
            serializedObject.ApplyModifiedProperties();
            return;
        }
        
        // Wave System
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("═══ WAVE SYSTEM (Optional) ═══", EditorStyles.boldLabel);
        
        bool wasUsingWaves = useWaveSystem.boolValue;
        EditorGUILayout.PropertyField(useWaveSystem, new GUIContent("Use Wave System"));
        
        // If wave system just got enabled, initialize
        if (!wasUsingWaves && useWaveSystem.boolValue)
        {
            InitializeWaveAssignments();
        }
        
        if (useWaveSystem.boolValue)
        {
            DrawWaveSystemUI();
        }
        
        // Validation
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("═══ VALIDATION ═══", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showValidationWarnings);
        
        // Debug info (read-only, shown at runtime)
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Debug info visible during play mode", MessageType.Info);
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void InitializeWaveAssignments()
    {
        int spawnerCount = spawnerObjects.arraySize;
        
        // Ensure spawnerWaveAssignments array matches spawner count
        if (spawnerWaveAssignments.arraySize != spawnerCount)
        {
            spawnerWaveAssignments.arraySize = spawnerCount;
            
            // Initialize new entries to wave 1
            for (int i = 0; i < spawnerCount; i++)
            {
                if (spawnerWaveAssignments.GetArrayElementAtIndex(i).intValue == 0)
                {
                    spawnerWaveAssignments.GetArrayElementAtIndex(i).intValue = 1;
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        // If numberOfWaves is 0 or invalid, set to 1
        if (numberOfWaves.intValue < 1)
        {
            numberOfWaves.intValue = 1;
            serializedObject.ApplyModifiedProperties();
        }
    }
    
    private void DrawWaveSystemUI()
    {
        EditorGUILayout.Space(10);
        
        int spawnerCount = spawnerObjects.arraySize;
        
        if (spawnerCount == 0)
        {
            EditorGUILayout.HelpBox("Add spawners to 'Spawner Objects' above to configure waves.", MessageType.Warning);
            return;
        }
        
        // Ensure array is correct size
        if (spawnerWaveAssignments.arraySize != spawnerCount)
        {
            InitializeWaveAssignments();
        }
        
        // Number of waves selector
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Number of Waves:", GUILayout.Width(120));
        int currentWaveCount = numberOfWaves.intValue;
        int newWaveCount = EditorGUILayout.IntSlider(currentWaveCount, 1, 10);
        if (newWaveCount != currentWaveCount)
        {
            numberOfWaves.intValue = newWaveCount;
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Assign Spawners to Waves:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign each spawner to a wave. Wave 1 spawns immediately, others spawn when previous wave is eliminated.", MessageType.Info);
        
        // Get current wave count once (used by all spawners below)
        int maxWaves = numberOfWaves.intValue;
        
        // Draw spawner wave assignments
        for (int i = 0; i < spawnerCount; i++)
        {
            SerializedProperty spawnerProp = spawnerObjects.GetArrayElementAtIndex(i);
            GameObject spawnerObj = spawnerProp.objectReferenceValue as GameObject;
            
            if (spawnerObj == null)
            {
                EditorGUILayout.HelpBox($"Spawner {i} is null!", MessageType.Error);
                continue;
            }
            
            EditorGUILayout.BeginHorizontal();
            
            // Spawner name
            EditorGUILayout.LabelField(spawnerObj.name, GUILayout.Width(200));
            
            // Wave assignment dropdown
            string[] waveOptions = new string[maxWaves + 1];
            waveOptions[0] = "Unassigned";
            for (int w = 1; w <= maxWaves; w++)
            {
                waveOptions[w] = $"Wave {w}";
            }
            
            SerializedProperty assignmentProp = spawnerWaveAssignments.GetArrayElementAtIndex(i);
            int currentWave = assignmentProp.intValue;
            int newWave = EditorGUILayout.Popup(currentWave, waveOptions);
            
            if (newWave != currentWave)
            {
                assignmentProp.intValue = newWave;
                serializedObject.ApplyModifiedProperties();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        // Build waves button
        EditorGUILayout.Space(10);
        if (GUILayout.Button("Apply Wave Configuration", GUILayout.Height(30)))
        {
            BuildWavesFromAssignments();
            EditorUtility.SetDirty(target);
        }
        
        // Show wave summary
        EditorGUILayout.Space(10);
        DrawWaveSummary();
        
        // Per-wave object activation/deactivation
        EditorGUILayout.Space(10);
        DrawWaveEventsUI();
    }
    
    private void DrawWaveSummary()
    {
        EditorGUILayout.LabelField("Wave Summary:", EditorStyles.boldLabel);
        
        int currentWaveCount = numberOfWaves.intValue;
        
        for (int wave = 1; wave <= currentWaveCount; wave++)
        {
            int count = 0;
            for (int i = 0; i < spawnerWaveAssignments.arraySize; i++)
            {
                if (spawnerWaveAssignments.GetArrayElementAtIndex(i).intValue == wave)
                {
                    count++;
                }
            }
            
            Color backgroundColor = GUI.backgroundColor;
            if (count == 0)
            {
                GUI.backgroundColor = Color.yellow;
            }
            
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField($"Wave {wave}:", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{count} spawner(s)", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            GUI.backgroundColor = backgroundColor;
        }
        
        int unassigned = 0;
        for (int i = 0; i < spawnerWaveAssignments.arraySize; i++)
        {
            if (spawnerWaveAssignments.GetArrayElementAtIndex(i).intValue == 0)
            {
                unassigned++;
            }
        }
        
        if (unassigned > 0)
        {
            EditorGUILayout.HelpBox($"{unassigned} spawner(s) are unassigned and will not spawn!", MessageType.Warning);
        }
    }
    
    private void DrawWaveEventsUI()
    {
        EditorGUILayout.LabelField("Wave Completion Events:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Configure objects to activate/deactivate when each wave completes.", MessageType.Info);
        
        int currentWaveCount = numberOfWaves.intValue;
        
        // Ensure waves array matches numberOfWaves
        if (waves.arraySize != currentWaveCount)
        {
            waves.arraySize = currentWaveCount;
            serializedObject.ApplyModifiedProperties();
        }
        
        for (int i = 0; i < currentWaveCount; i++)
        {
            SerializedProperty wave = waves.GetArrayElementAtIndex(i);
            SerializedProperty objectsToActivate = wave.FindPropertyRelative("objectsToActivate");
            SerializedProperty objectsToDeactivate = wave.FindPropertyRelative("objectsToDeactivate");
            
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Wave {i + 1} Completion:", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(objectsToActivate, new GUIContent("Objects to Activate"), true);
            EditorGUILayout.PropertyField(objectsToDeactivate, new GUIContent("Objects to Deactivate"), true);
            
            EditorGUILayout.EndVertical();
        }
    }
    
    private void BuildWavesFromAssignments()
    {
        int currentWaveCount = numberOfWaves.intValue;
        
        // Clear existing waves
        waves.ClearArray();
        waves.arraySize = currentWaveCount;
        
        // Build each wave
        for (int waveIndex = 0; waveIndex < currentWaveCount; waveIndex++)
        {
            int waveNumber = waveIndex + 1;
            
            SerializedProperty wave = waves.GetArrayElementAtIndex(waveIndex);
            SerializedProperty waveNumberProp = wave.FindPropertyRelative("waveNumber");
            SerializedProperty waveSpawners = wave.FindPropertyRelative("spawners");
            
            // Set wave number
            waveNumberProp.intValue = waveNumber;
            
            // Clear spawners list
            waveSpawners.ClearArray();
            
            // Add spawners assigned to this wave
            int spawnerIndexInWave = 0;
            for (int i = 0; i < spawnerWaveAssignments.arraySize; i++)
            {
                int assignedWave = spawnerWaveAssignments.GetArrayElementAtIndex(i).intValue;
                if (assignedWave == waveNumber)
                {
                    waveSpawners.InsertArrayElementAtIndex(spawnerIndexInWave);
                    SerializedProperty spawnerProp = waveSpawners.GetArrayElementAtIndex(spawnerIndexInWave);
                    spawnerProp.objectReferenceValue = spawnerObjects.GetArrayElementAtIndex(i).objectReferenceValue;
                    spawnerIndexInWave++;
                }
            }
        }
        
        serializedObject.ApplyModifiedProperties();
        
        Debug.Log($"<color=cyan>[Battle Royale Manager Editor]</color> Wave configuration applied! {currentWaveCount} waves configured.");
    }
}

