using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Custom inspector for AIRagdollSpawner with organized sections and dynamic mode-specific settings
/// </summary>
[CustomEditor(typeof(AIRagdollSpawner))]
public class AIRagdollSpawnerEditor : Editor
{
    private AIRagdollSpawner spawner;
    
    // Section foldouts
    private bool showMasterSettings = true;
    private bool showSpawnerSettings = true;
    private bool showAudioSettings = true;
    private bool showModeSettings = true;
    private bool showAdvancedSettings = false;
    private bool showGizmoSettings = false;
    
    // Waypoint selection tracking
    private static int selectedWaypointIndex = -1;
    
    private void OnEnable()
    {
        spawner = (AIRagdollSpawner)target;
    }
    
    /// <summary>
    /// Draw interactive handles in the Scene view for waypoints
    /// </summary>
    private void OnSceneGUI()
    {
        if (spawner == null) return;
        
        // Only draw waypoint handles for Path Movement mode
        if (spawner.aiMode != ActiveRagdoll.RagdollAIController.AIMode.PathMovement)
        {
            selectedWaypointIndex = -1; // Reset selection when not in path mode
            return;
        }
        
        if (spawner.pathWaypoints == null || spawner.pathWaypoints.Count == 0)
        {
            selectedWaypointIndex = -1; // Reset selection when no waypoints
            return;
        }
        
        // Validate selected index
        if (selectedWaypointIndex >= spawner.pathWaypoints.Count)
        {
            selectedWaypointIndex = -1;
        }
        
        // Handle F key to frame selected waypoint
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F && selectedWaypointIndex != -1)
        {
            // Frame the selected waypoint in the Scene view
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                Vector3 waypointPos = spawner.pathWaypoints[selectedWaypointIndex];
                sceneView.Frame(new Bounds(waypointPos, Vector3.one * 5f), false);
                e.Use(); // Consume the event
            }
        }
        
        // Draw spawn point (Point 0) - not editable here since it's controlled by transform
        Vector3 spawnPos = spawner.GetSpawnPosition();
        Handles.color = Color.white;
        Handles.Label(spawnPos + Vector3.up * 0.5f, "Point 0 (Spawn)", EditorStyles.boldLabel);
        
        // Draw all waypoints (clickable spheres and labels)
        for (int i = 0; i < spawner.pathWaypoints.Count; i++)
        {
            bool isSelected = (i == selectedWaypointIndex);
            
            // Draw clickable sphere button for waypoint selection
            if (isSelected)
            {
                // Selected waypoint - draw position handle
                Handles.color = Color.cyan;
                
                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.PositionHandle(spawner.pathWaypoints[i], Quaternion.identity);
                
                if (EditorGUI.EndChangeCheck())
                {
                    // Record undo
                    Undo.RecordObject(spawner, "Move Waypoint");
                    spawner.pathWaypoints[i] = newPosition;
                    EditorUtility.SetDirty(spawner);
                }
                
                // Draw highlighted sphere for selected waypoint
                Handles.color = Color.cyan;
                Handles.SphereHandleCap(0, spawner.pathWaypoints[i], Quaternion.identity, 0.5f, EventType.Repaint);
                
                // Draw label
                Handles.Label(spawner.pathWaypoints[i] + Vector3.up * 0.8f, $"Point {i + 1} [SELECTED]", EditorStyles.boldLabel);
            }
            else
            {
                // Non-selected waypoint - draw clickable sphere
                if (i == 0)
                    Handles.color = Color.green; // First user waypoint
                else
                    Handles.color = Color.yellow; // Remaining waypoints
                
                // Make sphere clickable
                float buttonSize = 0.4f;
                if (Handles.Button(spawner.pathWaypoints[i], Quaternion.identity, buttonSize, buttonSize, Handles.SphereHandleCap))
                {
                    selectedWaypointIndex = i;
                    Repaint();
                }
                
                // Draw label
                Handles.Label(spawner.pathWaypoints[i] + Vector3.up * 0.5f, $"Point {i + 1}", EditorStyles.boldLabel);
            }
        }
        
        // Draw path lines
        Handles.color = new Color(0f, 1f, 0f, 0.5f);
        
        // Line from spawn to first waypoint
        if (spawner.pathWaypoints.Count > 0)
        {
            Handles.DrawLine(spawnPos, spawner.pathWaypoints[0]);
        }
        
        // Lines between waypoints
        for (int i = 0; i < spawner.pathWaypoints.Count - 1; i++)
        {
            Handles.DrawLine(spawner.pathWaypoints[i], spawner.pathWaypoints[i + 1]);
        }
        
        // Draw return path or loop
        if (spawner.pathWaypoints.Count > 0)
        {
            if (spawner.returnToStartAndStop)
            {
                Handles.color = new Color(0f, 1f, 1f, 0.5f); // Cyan for return
                Handles.DrawDottedLine(spawner.pathWaypoints[spawner.pathWaypoints.Count - 1], spawnPos, 4f);
            }
            else if (spawner.loopPathForever)
            {
                Handles.color = new Color(1f, 0f, 1f, 0.5f); // Magenta for loop
                Handles.DrawDottedLine(spawner.pathWaypoints[spawner.pathWaypoints.Count - 1], spawnPos, 4f);
            }
        }
        
        // Instructions at the top of the scene view
        if (selectedWaypointIndex == -1)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 300, 80));
            GUILayout.BeginVertical("box");
            GUILayout.Label("Path Movement Mode", EditorStyles.boldLabel);
            GUILayout.Label("Click on a waypoint to select and move it");
            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
        }
        else
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 350, 80));
            GUILayout.BeginVertical("box");
            GUILayout.Label($"Point {selectedWaypointIndex + 1} Selected", EditorStyles.boldLabel);
            GUILayout.Label("Drag handle to move | Click another to select");
            GUILayout.Label("Press F to frame/focus on this point");
            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // Record undo state before making changes
        Undo.RecordObject(spawner, "Modify AI Ragdoll Spawner");
        
        // Track if any changes are made
        EditorGUI.BeginChangeCheck();
        
        EditorGUILayout.Space(10);
        
        // ==================== MASTER SETTINGS ====================
        DrawMasterSettings();
        
        EditorGUILayout.Space(15);
        
        // ==================== SPAWNER SETTINGS ====================
        DrawSpawnerSettings();
        
        EditorGUILayout.Space(15);
        
        // ==================== AUDIO SETTINGS ====================
        DrawAudioSettings();
        
        EditorGUILayout.Space(15);
        
        // ==================== MODE SELECTION & MODE-SPECIFIC SETTINGS ====================
        DrawModeSettings();
        
        EditorGUILayout.Space(15);
        
        // ==================== ADVANCED SETTINGS ====================
        DrawAdvancedSettings();
        
        EditorGUILayout.Space(15);
        
        // ==================== GIZMO SETTINGS ====================
        DrawGizmoSettings();
        
        // Mark object as dirty if any changes were made
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(spawner);
            // Also mark scene as dirty to ensure it saves
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            }
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawMasterSettings()
    {
        // Header with foldout
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showMasterSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showMasterSettings, "⚙️  MASTER SETTINGS");
        
        if (showMasterSettings)
        {
            EditorGUILayout.Space(5);
            
            // Costume Selection
            EditorGUILayout.LabelField("Costume Selection", EditorStyles.boldLabel);
            DrawCostumeSelection();
            
            EditorGUILayout.Space(5);
            
            // Legacy Ragdoll Skin (deprecated)
            spawner.ragdollSkin = (Material)EditorGUILayout.ObjectField(new GUIContent("Ragdoll Skin (Legacy)", "(DEPRECATED) Use costume selection above instead"), spawner.ragdollSkin, typeof(Material), false);
            
            if (spawner.ragdollSkin != null)
            {
                EditorGUILayout.HelpBox("⚠️ Material override is deprecated. Use costume selection above for better control.", MessageType.Warning);
            }
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Character Capabilities", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            spawner.canJump = EditorGUILayout.Toggle(new GUIContent("Can Jump", "AI can jump over gaps and obstacles"), spawner.canJump);
            spawner.canRun = EditorGUILayout.Toggle(new GUIContent("Can Run", "AI can run (applies run speed multiplier)"), spawner.canRun);
            
            // Show "Run Only When Chasing" option only if Can Run is disabled
            if (!spawner.canRun)
            {
                EditorGUI.indentLevel++;
                spawner.runOnlyWhenChasing = EditorGUILayout.Toggle(new GUIContent("Run Only When Chasing", "Allow running during chase/attack sequences only"), spawner.runOnlyWhenChasing);
                EditorGUI.indentLevel--;
            }
            
            spawner.disableGrabbing = EditorGUILayout.Toggle(new GUIContent("Disable Grabbing", "AI cannot grab the player or any objects"), spawner.disableGrabbing);
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Kill Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            spawner.contactKillsInstantly = EditorGUILayout.Toggle(new GUIContent("Contact Kills Instantly", "Touching the player instantly kills both player and AI"), spawner.contactKillsInstantly);
            
            EditorGUI.BeginDisabledGroup(spawner.disableGrabbing);
            spawner.grabKillsPlayer = EditorGUILayout.Toggle(new GUIContent("Grab Kills Player", "If AI grabs player, holding for X seconds kills both"), spawner.grabKillsPlayer);
            
            if (spawner.grabKillsPlayer && !spawner.disableGrabbing)
            {
                EditorGUI.indentLevel++;
                spawner.grabsRequiredToKill = EditorGUILayout.IntSlider(new GUIContent("Grabs Required To Kill", "Number of AIs that must grab simultaneously to trigger kill (1 = any single AI)"), spawner.grabsRequiredToKill, 1, 10);
                spawner.grabKillDuration = EditorGUILayout.Slider(new GUIContent("Grab Kill Duration", "Time in seconds AI must hold player before kill"), spawner.grabKillDuration, 1f, 10f);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Vignette Effect", EditorStyles.miniBoldLabel);
                spawner.vignetteStartIntensity = EditorGUILayout.Slider(new GUIContent("Start Intensity", "Vignette intensity when grab begins"), spawner.vignetteStartIntensity, 0f, 1f);
                spawner.vignetteMaxIntensity = EditorGUILayout.Slider(new GUIContent("Max Intensity", "Vignette intensity at end of grab duration"), spawner.vignetteMaxIntensity, 0f, 1f);
                
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Movement Stats", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            spawner.walkSpeed = EditorGUILayout.FloatField(new GUIContent("Walk Speed", "Base walking speed"), spawner.walkSpeed);
            spawner.runSpeedMultiplier = EditorGUILayout.FloatField(new GUIContent("Run Multiplier", "Speed multiplier when running"), spawner.runSpeedMultiplier);
            
            EditorGUI.BeginDisabledGroup(!spawner.canJump);
            spawner.jumpForce = EditorGUILayout.FloatField(new GUIContent("Jump Force", "How high AI can jump"), spawner.jumpForce);
            spawner.jumpCooldown = EditorGUILayout.FloatField(new GUIContent("Jump Cooldown", "Cooldown between jumps (seconds)"), spawner.jumpCooldown);
            EditorGUI.EndDisabledGroup();
            
            // Jump If Stuck - only show when Can Jump is disabled
            if (!spawner.canJump)
            {
                EditorGUILayout.Space(5);
                spawner.jumpIfStuck = EditorGUILayout.Toggle(new GUIContent("Jump If Stuck", "AI will jump if stuck against obstacle"), spawner.jumpIfStuck);
                
                EditorGUI.BeginDisabledGroup(!spawner.jumpIfStuck);
                spawner.stuckTimeThreshold = EditorGUILayout.Slider(new GUIContent("  Stuck Time Threshold", "Seconds stuck before jumping"), spawner.stuckTimeThreshold, 0.5f, 5f);
                EditorGUI.EndDisabledGroup();
                
                if (spawner.jumpIfStuck)
                {
                    EditorGUILayout.HelpBox("AI will jump when stuck for " + spawner.stuckTimeThreshold.ToString("F1") + " seconds", MessageType.Info);
                }
            }
            
            spawner.airSteeringForce = EditorGUILayout.FloatField(new GUIContent("Air Steering Force", "Force for mid-air control"), spawner.airSteeringForce);
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Death & Respawn", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            spawner.shouldRespawn = EditorGUILayout.Toggle(new GUIContent("Should Respawn", "AI respawns after death. If false, stays dead on ground."), spawner.shouldRespawn);
            
            spawner.staysDeadIfShot = EditorGUILayout.Toggle(new GUIContent("Stays Dead If Shot", "If true, corpse stays on field permanently like Red Light Green Light mode"), spawner.staysDeadIfShot);
            
            EditorGUI.BeginDisabledGroup(!spawner.shouldRespawn);
            spawner.deathDelay = EditorGUILayout.Slider(new GUIContent("Death Delay", "Time that ragdoll parts stay visible before respawning"), spawner.deathDelay, 0.1f, 10f);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(5);
            spawner.shotsToKill = EditorGUILayout.IntSlider(new GUIContent("Shots To Kill", "Number of hits required to kill this AI"), spawner.shotsToKill, 1, 20);
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Boundary Containment", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUILayout.HelpBox("📦 Boundary containment is now managed by AIBoundaryBox GameObjects in the scene. Create one, add the AIBoundaryBox component, and AIs spawned inside will auto-register!", MessageType.Info);
            
            EditorGUI.indentLevel--;
            
            // ===== AWARENESS SETTINGS (Conditional based on AI mode) =====
            bool modeUsesAwareness = ModeUsesAwareness(spawner.aiMode);
            
            if (modeUsesAwareness)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Player Awareness", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                
                spawner.awarenessType = (AIRagdollSpawner.AwarenessType)EditorGUILayout.EnumPopup(
                    new GUIContent("Detection Type", "How AI detects the player"),
                    spawner.awarenessType
                );
                
                if (spawner.awarenessType == AIRagdollSpawner.AwarenessType.None)
                {
                    EditorGUILayout.HelpBox("AI will not detect player (useful for patrol/scripted paths)", MessageType.Info);
                }
                else if (spawner.awarenessType == AIRagdollSpawner.AwarenessType.ProximityRadius)
                {
                    EditorGUILayout.HelpBox("360° proximity detection - AI detects player within radius regardless of direction", MessageType.Info);
                    
                    spawner.awarenessRadius = EditorGUILayout.Slider(
                        new GUIContent("Detection Radius", "Distance to detect player (360° around AI)"),
                        spawner.awarenessRadius, 1f, 50f
                    );
                    
                    spawner.awarenessLoseRadius = EditorGUILayout.Slider(
                        new GUIContent("Lose Radius", "Distance before AI stops chasing (should be > detection)"),
                        spawner.awarenessLoseRadius, 1f, 50f
                    );
                    
                    // Validation
                    if (spawner.awarenessLoseRadius <= spawner.awarenessRadius)
                    {
                        EditorGUILayout.HelpBox("⚠️ Lose Radius should be greater than Detection Radius to prevent flickering!", MessageType.Warning);
                    }
                }
                else if (spawner.awarenessType == AIRagdollSpawner.AwarenessType.RaycastVision)
                {
                    EditorGUILayout.HelpBox("Vision cone detection - AI detects player within specified angle using raycast", MessageType.Info);
                    
                    spawner.awarenessVisionAngle = EditorGUILayout.Slider(
                        new GUIContent("Vision Cone Angle", "Field of view in degrees (360° = see all directions)"),
                        spawner.awarenessVisionAngle, 10f, 360f
                    );
                    
                    spawner.awarenessVisionDistance = EditorGUILayout.Slider(
                        new GUIContent("Vision Distance", "Max raycast detection distance"),
                        spawner.awarenessVisionDistance, 1f, 50f
                    );
                    
                    // Visual help
                    string angleDescription = spawner.awarenessVisionAngle >= 350f ? "Nearly omnidirectional" :
                                             spawner.awarenessVisionAngle >= 180f ? "Wide field of view" :
                                             spawner.awarenessVisionAngle >= 90f ? "Moderate cone" : "Narrow vision";
                    EditorGUILayout.LabelField($"  └ {angleDescription}", EditorStyles.miniLabel);
                }
                else if (spawner.awarenessType == AIRagdollSpawner.AwarenessType.BoundaryBox)
                {
                    EditorGUILayout.HelpBox("🎯 NEW: Boundary boxes are now managed by AIBoundaryBox GameObjects in the scene!", MessageType.Info);
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("How to use:\n1. Create an empty GameObject in your scene\n2. Add the 'AIBoundaryBox' component\n3. Position, rotate, and scale it as a transform\n4. AIs spawned inside will auto-register and be contained", MessageType.None);
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("✅ Benefits:\n- ONE detection sound when player enters (not multiple!)\n- Easy visual editing in Scene view\n- Multiple boxes in one scene\n- AIs auto-discover boxes on spawn", MessageType.None);
                }
                
                EditorGUI.indentLevel--;
            }
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    
    /// <summary>
    /// Check if the AI mode uses awareness/detection
    /// </summary>
    private bool ModeUsesAwareness(ActiveRagdoll.RagdollAIController.AIMode mode)
    {
        // Modes that use awareness (not Homing - it has its own alwaysKnowPlayerPosition setting)
        return mode == ActiveRagdoll.RagdollAIController.AIMode.ExploreThenAttack ||
               mode == ActiveRagdoll.RagdollAIController.AIMode.RoamThenAttack ||
               mode == ActiveRagdoll.RagdollAIController.AIMode.IdleThenAttack ||
               mode == ActiveRagdoll.RagdollAIController.AIMode.PathMovement; // PathThenAttack variant
    }
    
    private void DrawSpawnerSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showSpawnerSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showSpawnerSettings, "🎯  SPAWNER SETTINGS");
        
        if (showSpawnerSettings)
        {
            EditorGUILayout.Space(5);
            
            spawner.aiRagdollPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("AI Ragdoll Prefab", "The AI ragdoll prefab to spawn"), spawner.aiRagdollPrefab, typeof(GameObject), false);
            
            if (spawner.aiRagdollPrefab == null)
            {
                EditorGUILayout.HelpBox("⚠️ No prefab assigned! Assign an AI ragdoll prefab to spawn.", MessageType.Warning);
            }
            
            EditorGUILayout.Space(5);
            
            spawner.spawnOffset = EditorGUILayout.Vector3Field(new GUIContent("Spawn Offset", "Offset from spawner position"), spawner.spawnOffset);
            spawner.useRotation = EditorGUILayout.Toggle(new GUIContent("Use Rotation", "Ragdoll matches spawner's rotation"), spawner.useRotation);
            spawner.spawnOnStart = EditorGUILayout.Toggle(new GUIContent("Spawn On Start", "Auto-spawn at level start"), spawner.spawnOnStart);
            
            EditorGUI.BeginDisabledGroup(!spawner.spawnOnStart);
            spawner.initialSpawnCount = EditorGUILayout.IntSlider(new GUIContent("Initial Spawn Count", "Number to spawn at level start"), spawner.initialSpawnCount, 0, 10);
            spawner.spawnAllAtOnce = EditorGUILayout.Toggle(new GUIContent("Spawn All At Once", "If false, use 2-second delay between spawns"), spawner.spawnAllAtOnce);
            EditorGUI.EndDisabledGroup();
            
            spawner.maxActiveRagdolls = EditorGUILayout.IntSlider(new GUIContent("Max Active Ragdolls", "Maximum concurrent instances"), spawner.maxActiveRagdolls, 1, 10);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Spawn Limits", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            spawner.limitTotalSpawns = EditorGUILayout.Toggle(new GUIContent("Limit Total Spawns", "Spawner stops after reaching max total spawns"), spawner.limitTotalSpawns);
            
            EditorGUI.BeginDisabledGroup(!spawner.limitTotalSpawns);
            spawner.maxTotalSpawns = EditorGUILayout.IntField(new GUIContent("Max Total Spawns", "Maximum total spawns allowed"), spawner.maxTotalSpawns);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawAudioSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showAudioSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showAudioSettings, "🔊  AUDIO SETTINGS");
        
        if (showAudioSettings)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Configure all audio for spawned AI ragdolls. Leave empty to use prefab defaults. All sounds are 3D spatial audio.", MessageType.Info);
            EditorGUILayout.Space(5);
            
            // Kill Sounds
            EditorGUILayout.LabelField("Kill Sounds", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUI.BeginDisabledGroup(!spawner.contactKillsInstantly);
            spawner.contactKillSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Contact Kill Sound", "Sound when AI kills player on contact (2D)"), spawner.contactKillSound, typeof(AudioClip), false);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.BeginDisabledGroup(!spawner.grabKillsPlayer || spawner.disableGrabbing);
            spawner.grabKillSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Grab Kill Sound", "Sound when AI kills player by grabbing (2D)"), spawner.grabKillSound, typeof(AudioClip), false);
            EditorGUI.EndDisabledGroup();
            
            spawner.aiDeathSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("AI Death Sound", "Sound when AI dies (3D spatial)"), spawner.aiDeathSound, typeof(AudioClip), false);
            spawner.bulletHitSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Bullet Hit Sound", "Sound when AI gets shot by bullet (2D - always audible)"), spawner.bulletHitSound, typeof(AudioClip), false);
            spawner.headshotSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Headshot Sound", "Special sound for headshots (2D - always audible) - if null, uses Bullet Hit Sound"), spawner.headshotSound, typeof(AudioClip), false);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Visual Effects", EditorStyles.miniBoldLabel);
            spawner.deathMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Death Material", "Material to apply to AI when it dies (e.g., red unlit material)"), spawner.deathMaterial, typeof(Material), false);
            
            EditorGUILayout.Space(3);
            spawner.enableBulletImpactEffect = EditorGUILayout.Toggle(new GUIContent("Enable Bullet Impact Effects", "Enable particle effects when this AI gets shot by player"), spawner.enableBulletImpactEffect);
            EditorGUI.BeginDisabledGroup(!spawner.enableBulletImpactEffect);
            spawner.bulletImpactEffectPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Bullet Impact Effect Prefab (KILL)", "Particle effect for KILLING BLOW (e.g., explosion) - spawned when AI dies"), spawner.bulletImpactEffectPrefab, typeof(GameObject), false);
            spawner.bulletDamageEffectPrefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Bullet Damage Effect Prefab", "Particle effect for DAMAGE (non-lethal hits) - spawned when AI survives"), spawner.bulletDamageEffectPrefab, typeof(GameObject), false);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            
            // AI Behavior Sounds
            EditorGUILayout.LabelField("AI Behavior Sounds", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            bool modeUsesAwareness = ModeUsesAwareness(spawner.aiMode) || 
                                    (spawner.aiMode == ActiveRagdoll.RagdollAIController.AIMode.PathMovement && spawner.pathThenAttackEnabled);
            
            EditorGUI.BeginDisabledGroup(!modeUsesAwareness);
            spawner.playerDetectionSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Player Detection Sound", "Sound when AI spots player (3D spatial)"), spawner.playerDetectionSound, typeof(AudioClip), false);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            
            // Movement & Impact Sounds
            EditorGUILayout.LabelField("Movement & Impact Sounds", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            // Impact Sounds Array
            EditorGUILayout.LabelField("Impact Sounds (collisions & footsteps)", EditorStyles.miniBoldLabel);
            if (spawner.impactSounds == null) spawner.impactSounds = new AudioClip[0];
            int impactSize = spawner.impactSounds.Length;
            int newImpactSize = EditorGUILayout.IntField("Size", impactSize);
            if (newImpactSize != impactSize)
            {
                System.Array.Resize(ref spawner.impactSounds, Mathf.Max(0, newImpactSize));
            }
            for (int i = 0; i < spawner.impactSounds.Length; i++)
            {
                spawner.impactSounds[i] = (AudioClip)EditorGUILayout.ObjectField($"Element {i}", spawner.impactSounds[i], typeof(AudioClip), false);
            }
            
            EditorGUILayout.Space(5);
            
            // Jump Sounds Array
            EditorGUILayout.LabelField("Jump Sounds", EditorStyles.miniBoldLabel);
            if (spawner.jumpSounds == null) spawner.jumpSounds = new AudioClip[0];
            int jumpSize = spawner.jumpSounds.Length;
            int newJumpSize = EditorGUILayout.IntField("Size", jumpSize);
            if (newJumpSize != jumpSize)
            {
                System.Array.Resize(ref spawner.jumpSounds, Mathf.Max(0, newJumpSize));
            }
            for (int i = 0; i < spawner.jumpSounds.Length; i++)
            {
                spawner.jumpSounds[i] = (AudioClip)EditorGUILayout.ObjectField($"Element {i}", spawner.jumpSounds[i], typeof(AudioClip), false);
            }
            
            EditorGUILayout.Space(5);
            
            // Landing Sounds Array
            EditorGUILayout.LabelField("Landing Sounds (optional)", EditorStyles.miniBoldLabel);
            if (spawner.landingSounds == null) spawner.landingSounds = new AudioClip[0];
            int landingSize = spawner.landingSounds.Length;
            int newLandingSize = EditorGUILayout.IntField("Size", landingSize);
            if (newLandingSize != landingSize)
            {
                System.Array.Resize(ref spawner.landingSounds, Mathf.Max(0, newLandingSize));
            }
            for (int i = 0; i < spawner.landingSounds.Length; i++)
            {
                spawner.landingSounds[i] = (AudioClip)EditorGUILayout.ObjectField($"Element {i}", spawner.landingSounds[i], typeof(AudioClip), false);
            }
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            
            // Atmospheric Sounds
            EditorGUILayout.LabelField("Atmospheric Sounds", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            spawner.windSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Wind Sound", "Looping wind sound when falling (3D spatial)"), spawner.windSound, typeof(AudioClip), false);
            spawner.ragdollLoopSound = (AudioClip)EditorGUILayout.ObjectField(new GUIContent("Ragdoll Loop Sound", "Looping sound in ragdoll mode (3D spatial)"), spawner.ragdollLoopSound, typeof(AudioClip), false);
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawModeSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showModeSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showModeSettings, "🤖  AI MODE & BEHAVIOR");
        
        if (showModeSettings)
        {
            EditorGUILayout.Space(5);
            
            // Mode dropdown
            spawner.aiMode = (ActiveRagdoll.RagdollAIController.AIMode)EditorGUILayout.EnumPopup(new GUIContent("AI Mode", "Behavior mode for this AI"), spawner.aiMode);
            
            // Mode description
            EditorGUILayout.HelpBox(GetModeDescription(spawner.aiMode), MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Draw mode-specific settings based on selected mode
            switch (spawner.aiMode)
            {
                case ActiveRagdoll.RagdollAIController.AIMode.Idle:
                    DrawIdleSettings();
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.Roam:
                    DrawRoamSettings();
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.Explore:
                    DrawExploreSettings();
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.Homing:
                    DrawHomingSettings();
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.PathMovement:
                    DrawPathMovementSettings();
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.ExploreThenAttack:
                    DrawExploreSettings();
                    EditorGUILayout.Space(10);
                    DrawAttackDetectionSettings("Explore Then Attack");
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.RoamThenAttack:
                    DrawRoamSettings();
                    EditorGUILayout.Space(10);
                    DrawAttackDetectionSettings("Roam Then Attack");
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.IdleThenAttack:
                    DrawIdleSettings();
                    EditorGUILayout.Space(10);
                    DrawAttackDetectionSettings("Idle Then Attack");
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.RaceToFinish:
                    EditorGUILayout.HelpBox("No additional settings for Race to Finish mode. AI automatically finds and races to the finish flag.", MessageType.Info);
                    break;
                    
                case ActiveRagdoll.RagdollAIController.AIMode.RedLightGreenLight:
                    EditorGUILayout.HelpBox("Red Light Green Light settings are managed by the RedLightGreenLightManager component.", MessageType.Info);
                    break;
            }
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawIdleSettings()
    {
        EditorGUILayout.LabelField("Idle Mode Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        spawner.idlePaceRadius = EditorGUILayout.FloatField(new GUIContent("Pace Radius", "Ragdoll wanders within this circle around spawn point"), spawner.idlePaceRadius);
        spawner.idleWaitTime = EditorGUILayout.FloatField(new GUIContent("Wait Time", "How long to pause at each spot"), spawner.idleWaitTime);
        
        if (spawner.aiMode == ActiveRagdoll.RagdollAIController.AIMode.IdleThenAttack)
        {
            spawner.idleVisionConeAngle = EditorGUILayout.FloatField(new GUIContent("Vision Cone Angle", "Vision cone angle for player detection (degrees)"), spawner.idleVisionConeAngle);
        }
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawRoamSettings()
    {
        EditorGUILayout.LabelField("Roam Mode Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        spawner.roamWaitTime = EditorGUILayout.FloatField(new GUIContent("Wait Time", "How long to wait at each roam point"), spawner.roamWaitTime);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("📦 Roam Mode requires an AIBoundaryBox GameObject in the scene. Create one and position it where you want AIs to roam.", MessageType.Info);
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawExploreSettings()
    {
        EditorGUILayout.LabelField("Explore Mode Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        EditorGUILayout.HelpBox("Explore mode uses 360° raycast pathfinding with memory system. Boundary containment controlled by Master Settings.", MessageType.Info);
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawHomingSettings()
    {
        EditorGUILayout.LabelField("Homing Mode Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        spawner.targetObject = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Target Object", "Target to chase (null = auto-find player)"), spawner.targetObject, typeof(GameObject), true);
        spawner.alwaysKnowPlayerPosition = EditorGUILayout.Toggle(new GUIContent("Always Know Position", "Always track player vs raycast detection"), spawner.alwaysKnowPlayerPosition);
        spawner.homingStopDistance = EditorGUILayout.FloatField(new GUIContent("Stop Distance", "How close to get before stopping"), spawner.homingStopDistance);
        
        EditorGUI.BeginDisabledGroup(spawner.disableGrabbing);
        spawner.grabPlayerInHomingMode = EditorGUILayout.Toggle(new GUIContent("Grab Player", "Try to grab player when close"), spawner.grabPlayerInHomingMode);
        
        if (spawner.grabPlayerInHomingMode && !spawner.disableGrabbing)
        {
            EditorGUI.indentLevel++;
            spawner.armExtendDistance = EditorGUILayout.FloatField(new GUIContent("Arm Extend Distance", "Distance to extend arms forward"), spawner.armExtendDistance);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawPathMovementSettings()
    {
        EditorGUILayout.LabelField("Path Movement Settings", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        // Waypoints list
        EditorGUILayout.LabelField("Waypoints (Point 0 is spawn position)", EditorStyles.miniBoldLabel);
        
        // Draw each waypoint with individual remove button
        for (int i = 0; i < spawner.pathWaypoints.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            spawner.pathWaypoints[i] = EditorGUILayout.Vector3Field($"Point {i + 1}", spawner.pathWaypoints[i]);
            
            // Remove button for this waypoint
            if (GUILayout.Button("-", GUILayout.Width(25)))
            {
                Undo.RecordObject(spawner, "Remove Waypoint");
                spawner.pathWaypoints.RemoveAt(i);
                EditorUtility.SetDirty(spawner);
                break; // Exit loop since we modified the list
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        // Add waypoint button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Waypoint", GUILayout.Width(120)))
        {
            Undo.RecordObject(spawner, "Add Waypoint");
            
            // Get position for new waypoint
            Vector3 newWaypointPosition;
            if (spawner.pathWaypoints.Count > 0)
            {
                // Start at last waypoint's position
                newWaypointPosition = spawner.pathWaypoints[spawner.pathWaypoints.Count - 1];
            }
            else
            {
                // Start at spawn position if it's the first waypoint
                newWaypointPosition = spawner.GetSpawnPosition();
            }
            
            spawner.pathWaypoints.Add(newWaypointPosition);
            EditorUtility.SetDirty(spawner);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        spawner.pathReachedDistance = EditorGUILayout.FloatField(new GUIContent("Reached Distance", "Distance to consider waypoint 'reached'"), spawner.pathReachedDistance);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Path Behavior", EditorStyles.miniBoldLabel);
        
        spawner.respawnAtLastPathPoint = EditorGUILayout.Toggle(new GUIContent("Respawn At Last Point", "When AI dies, it respawns at the last waypoint it reached instead of at spawn point"), spawner.respawnAtLastPathPoint);
        spawner.loopPathForever = EditorGUILayout.Toggle(new GUIContent("Loop Forever", "Infinite path cycles"), spawner.loopPathForever);
        
        EditorGUI.BeginDisabledGroup(spawner.loopPathForever);
        spawner.returnToStartAndStop = EditorGUILayout.Toggle(new GUIContent("Return To Start & Stop", "Return to Point A and stop after final waypoint"), spawner.returnToStartAndStop);
        spawner.numberOfCycles = EditorGUILayout.IntField(new GUIContent("Number of Cycles", "Cycles before stopping (0 = infinite)"), spawner.numberOfCycles);
        spawner.endAtFinishTrigger = EditorGUILayout.Toggle(new GUIContent("End At Finish Trigger", "Navigate to finish flag after path completion"), spawner.endAtFinishTrigger);
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Path Then Attack", EditorStyles.miniBoldLabel);
        
        spawner.pathThenAttackEnabled = EditorGUILayout.Toggle(new GUIContent("Enable Attack Mode", "Chase player if detected, then return to path"), spawner.pathThenAttackEnabled);
        
        if (spawner.pathThenAttackEnabled)
        {
            EditorGUILayout.HelpBox("Detection uses Master Settings > Player Awareness configuration", MessageType.Info);
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Behavior Switch After Path", EditorStyles.miniBoldLabel);
        
        spawner.pathOneWayThenBehaviorSwitch = EditorGUILayout.Toggle(new GUIContent("Switch After Completion", "Switch to different AI mode after path completes"), spawner.pathOneWayThenBehaviorSwitch);
        
        if (spawner.pathOneWayThenBehaviorSwitch)
        {
            EditorGUI.indentLevel++;
            spawner.pathFinalBehaviorMode = (AIRagdollSpawner.PathFinalBehaviorMode)EditorGUILayout.EnumPopup(new GUIContent("Final Behavior", "AI mode after path completion"), spawner.pathFinalBehaviorMode);
            EditorGUI.indentLevel--;
        }
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawAttackDetectionSettings(string modeName)
    {
        EditorGUILayout.LabelField($"{modeName} Detection", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        
        if (modeName == "Explore Then Attack")
        {
            spawner.exploreThenAttackDetectionRadius = EditorGUILayout.FloatField(new GUIContent("Detection Radius", "Distance to detect player (proximity, no LOS check)"), spawner.exploreThenAttackDetectionRadius);
            spawner.exploreThenAttackLoseRadius = EditorGUILayout.FloatField(new GUIContent("Lose Radius", "Distance to lose player and resume exploring"), spawner.exploreThenAttackLoseRadius);
        }
        else if (modeName == "Roam Then Attack")
        {
            spawner.roamThenAttackDetectionRadius = EditorGUILayout.FloatField(new GUIContent("Detection Radius", "Distance to detect player"), spawner.roamThenAttackDetectionRadius);
            spawner.roamThenAttackLoseRadius = EditorGUILayout.FloatField(new GUIContent("Lose Radius", "Distance to lose player and resume roaming"), spawner.roamThenAttackLoseRadius);
        }
        else if (modeName == "Idle Then Attack")
        {
            spawner.idleThenAttackDetectionRange = EditorGUILayout.FloatField(new GUIContent("Detection Range", "Raycast-based detection range"), spawner.idleThenAttackDetectionRange);
            spawner.idleThenAttackReturnDistance = EditorGUILayout.FloatField(new GUIContent("Return Distance", "Distance to home point to consider returned"), spawner.idleThenAttackReturnDistance);
        }
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawAdvancedSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showAdvancedSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvancedSettings, "⚡ ADVANCED SETTINGS");
        
        if (showAdvancedSettings)
        {
            EditorGUILayout.Space(5);
            
            // ===== PERFORMANCE OPTIMIZATION =====
            EditorGUILayout.LabelField("⚡ Performance Optimization", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            spawner.optimizeColliders = EditorGUILayout.Toggle(
                new GUIContent("Optimize Colliders (EXPERIMENTAL)", 
                "Disable colliders on limbs (arms/legs) while keeping essential ones (head, torso, hands, feet). Reduces physics cost by ~70% but may cause visual glitches."),
                spawner.optimizeColliders
            );
            
            if (spawner.optimizeColliders)
            {
                EditorGUILayout.HelpBox("✅ ENABLED: Limb colliders will be disabled on spawn\n\n" +
                    "Keeps: Head, Torso, Hands, Feet\n" +
                    "Disables: Neck, Upper/Lower Arms, Upper/Lower Legs\n\n" +
                    "⚠️ May cause:\n" +
                    "• Limbs clipping through walls\n" +
                    "• Shots to arms/legs not registering\n" +
                    "• Slightly less stable physics", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("All colliders active (default). Enable for massive performance boost if you have many AI ragdolls.", MessageType.Info);
            }
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            
            // ===== JUMP SETTINGS =====
            EditorGUILayout.LabelField("Jump Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUI.BeginDisabledGroup(!spawner.canJump);
            spawner.gapCheckDistance = EditorGUILayout.FloatField(new GUIContent("Gap Check Distance", "How far ahead to check for gaps"), spawner.gapCheckDistance);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(10);
            
            // ===== BOUNDARY BOX INFO =====
            EditorGUILayout.LabelField("Boundary Management", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            bool roamMode = spawner.aiMode == ActiveRagdoll.RagdollAIController.AIMode.Roam || spawner.aiMode == ActiveRagdoll.RagdollAIController.AIMode.RoamThenAttack;
            // Boundary boxes are now handled by AIBoundaryBox GameObjects in the scene
            EditorGUILayout.HelpBox("📦 Boundary boxes have been moved to scene GameObjects!\n\nTo create a boundary:\n1. GameObject → Create Empty\n2. Add AIBoundaryBox component\n3. Position, rotate, and scale as needed\n\nAIs spawned inside will automatically register!", MessageType.Info);
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawGizmoSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showGizmoSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showGizmoSettings, "🎨  GIZMO SETTINGS");
        
        if (showGizmoSettings)
        {
            EditorGUILayout.Space(5);
            
            spawner.gizmoColor = EditorGUILayout.ColorField(new GUIContent("Gizmo Color", "Color of spawn point gizmo"), spawner.gizmoColor);
            spawner.gizmoSize = EditorGUILayout.FloatField(new GUIContent("Gizmo Size", "Size of spawn point gizmo"), spawner.gizmoSize);
        }
        
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.EndVertical();
    }
    
    private string GetModeDescription(ActiveRagdoll.RagdollAIController.AIMode mode)
    {
        switch (mode)
        {
            case ActiveRagdoll.RagdollAIController.AIMode.Idle:
                return "📍 Stands and paces in a small area around spawn point.";
                
            case ActiveRagdoll.RagdollAIController.AIMode.Roam:
                return "🚶 Wanders randomly within a defined boundary box.";
                
            case ActiveRagdoll.RagdollAIController.AIMode.Explore:
                return "🔍 Uses raycast pathfinding to explore the environment.";
                
            case ActiveRagdoll.RagdollAIController.AIMode.Homing:
                return "🎯 Constantly tracks and chases the target (player).";
                
            case ActiveRagdoll.RagdollAIController.AIMode.PathMovement:
                return "🛤️ Follows a predefined path through waypoints.";
                
            case ActiveRagdoll.RagdollAIController.AIMode.ExploreThenAttack:
                return "🔍➡️🎯 Explores until player detected (proximity), then chases. Returns to exploring when player lost.";
                
            case ActiveRagdoll.RagdollAIController.AIMode.RoamThenAttack:
                return "🚶➡️🎯 Roams until player detected, then chases. Returns to roaming when player lost.";
                
            case ActiveRagdoll.RagdollAIController.AIMode.IdleThenAttack:
                return "📍➡️🎯 Idles until player spotted (raycast vision), then chases. Returns home after grab or player lost.";
                
            case ActiveRagdoll.RagdollAIController.AIMode.RaceToFinish:
                return "🏁 Automatically finds and races to the level finish flag.";
                
            case ActiveRagdoll.RagdollAIController.AIMode.RedLightGreenLight:
                return "🚦 Squid Game mode - races to finish but stops during red lights.";
                
            default:
                return "";
        }
    }
    
    /// <summary>
    /// Draw costume selection dropdown
    /// </summary>
    private void DrawCostumeSelection()
    {
        if (spawner.aiRagdollPrefab == null)
        {
            EditorGUILayout.HelpBox("Assign an AI Ragdoll Prefab to select costumes", MessageType.Info);
            return;
        }
        
        // Get all costume hierarchies from the prefab
        List<string> costumeNames = new List<string>();
        List<GameObject> costumes = new List<GameObject>();
        
        foreach (Transform child in spawner.aiRagdollPrefab.transform)
        {
            // Skip camera and other systems
            if (child.name.Contains("Camera") || child.name.Contains("System"))
                continue;
            
            // Check if it has Animated/Physical structure (costume hierarchy)
            bool hasAnimated = child.Find("Animated") != null;
            bool hasPhysical = child.Find("Physical") != null;
            
            if (hasAnimated && hasPhysical)
            {
                costumes.Add(child.gameObject);
                costumeNames.Add(child.name);
            }
        }
        
        if (costumes.Count == 0)
        {
            EditorGUILayout.HelpBox("⚠️ No costumes found in prefab! Prefab should have costume hierarchies with Animated/Physical structure.", MessageType.Warning);
            return;
        }
        
        // Validate current selection
        if (spawner.selectedCostumeIndex >= costumes.Count)
        {
            spawner.selectedCostumeIndex = 0;
        }
        
        // Costume dropdown
        int newIndex = EditorGUILayout.Popup("Selected Costume", spawner.selectedCostumeIndex, costumeNames.ToArray());
        
        if (newIndex != spawner.selectedCostumeIndex)
        {
            Undo.RecordObject(spawner, "Change AI Costume");
            spawner.selectedCostumeIndex = newIndex;
            spawner.selectedCostumeName = costumeNames[newIndex];
            EditorUtility.SetDirty(spawner);
        }
        
        // Update name if it doesn't match
        if (spawner.selectedCostumeIndex < costumeNames.Count && 
            spawner.selectedCostumeName != costumeNames[spawner.selectedCostumeIndex])
        {
            spawner.selectedCostumeName = costumeNames[spawner.selectedCostumeIndex];
        }
        
        // Display info
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(" ");
        EditorGUILayout.LabelField($"✓ Spawning with: {spawner.selectedCostumeName}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        
        // Show costume count
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(" ");
        EditorGUILayout.LabelField($"({costumes.Count} costume{(costumes.Count != 1 ? "s" : "")} available)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
}

