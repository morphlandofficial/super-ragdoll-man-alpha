using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SkyboxRandomizer))]
public class SkyboxRandomizerEditor : Editor
{
    private SerializedProperty skyboxMaterials;
    private SerializedProperty enableCommon;
    private SerializedProperty commonSkyboxes;
    private SerializedProperty enableUncommon;
    private SerializedProperty uncommonSkyboxes;
    private SerializedProperty enableRare;
    private SerializedProperty rareSkyboxes;
    private SerializedProperty commonWeight;
    private SerializedProperty uncommonWeight;
    private SerializedProperty rareWeight;
    private SerializedProperty allLevelsUnlockedSkybox;
    private SerializedProperty firstTimeSkybox;
    private SerializedProperty firstTimePrefKey;
    private SerializedProperty treatEditorPlayAsNewGame;
    private SerializedProperty randomizeOnStart;
    private SerializedProperty enableDebugControls;
    private SerializedProperty requireAllLevelsUnlockedForCycling;
    private SerializedProperty showWeightInfo;

    private void OnEnable()
    {
        skyboxMaterials = serializedObject.FindProperty("skyboxMaterials");
        enableCommon = serializedObject.FindProperty("enableCommon");
        commonSkyboxes = serializedObject.FindProperty("commonSkyboxes");
        enableUncommon = serializedObject.FindProperty("enableUncommon");
        uncommonSkyboxes = serializedObject.FindProperty("uncommonSkyboxes");
        enableRare = serializedObject.FindProperty("enableRare");
        rareSkyboxes = serializedObject.FindProperty("rareSkyboxes");
        commonWeight = serializedObject.FindProperty("commonWeight");
        uncommonWeight = serializedObject.FindProperty("uncommonWeight");
        rareWeight = serializedObject.FindProperty("rareWeight");
        allLevelsUnlockedSkybox = serializedObject.FindProperty("allLevelsUnlockedSkybox");
        firstTimeSkybox = serializedObject.FindProperty("firstTimeSkybox");
        firstTimePrefKey = serializedObject.FindProperty("firstTimePrefKey");
        treatEditorPlayAsNewGame = serializedObject.FindProperty("treatEditorPlayAsNewGame");
        randomizeOnStart = serializedObject.FindProperty("randomizeOnStart");
        enableDebugControls = serializedObject.FindProperty("enableDebugControls");
        requireAllLevelsUnlockedForCycling = serializedObject.FindProperty("requireAllLevelsUnlockedForCycling");
        showWeightInfo = serializedObject.FindProperty("showWeightInfo");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SkyboxRandomizer script = (SkyboxRandomizer)target;

        // Header style
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };

        // Show current skybox during play mode
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.Space(5);
            
            Material currentSkybox = script.GetCurrentSkybox();
            
            if (currentSkybox != null)
            {
                // Get rarity of current skybox
                string rarity = GetCurrentSkyboxRarity(script, currentSkybox);
                Color rarityColor = GetRarityColor(rarity);
                
                // Create styled box
                GUI.backgroundColor = rarityColor;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;
                
                GUIStyle currentSkyboxStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                
                EditorGUILayout.LabelField("🌌 CURRENT SKYBOX", currentSkyboxStyle);
                
                GUIStyle nameStyle = new GUIStyle(EditorStyles.largeLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                
                EditorGUILayout.LabelField(currentSkybox.name, nameStyle);
                
                if (!string.IsNullOrEmpty(rarity))
                {
                    GUIStyle rarityStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Italic
                    };
                    EditorGUILayout.LabelField(rarity, rarityStyle);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(10);
            }
            else
            {
                EditorGUILayout.HelpBox("No skybox currently active", MessageType.Warning);
                EditorGUILayout.Space(5);
            }
            
            // Force repaint to keep updating
            Repaint();
        }

        // Legacy Array Section
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Legacy Skybox Materials", headerStyle);
        EditorGUILayout.PropertyField(skyboxMaterials, new GUIContent("Skybox Materials (Old System)"));
        
        bool bucketsEmpty = (commonSkyboxes.arraySize == 0 && uncommonSkyboxes.arraySize == 0 && rareSkyboxes.arraySize == 0);
        
        // Migration button and status
        if (skyboxMaterials.arraySize > 0 && bucketsEmpty)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("↓ Populate Buckets From Array ↓", GUILayout.Height(30), GUILayout.Width(220)))
            {
                script.PopulateFromSimpleArray();
                EditorUtility.SetDirty(target);
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox("Auto-population will happen automatically! Or click the button to populate now.", MessageType.Info);
        }
        else if (!bucketsEmpty)
        {
            int totalCount = commonSkyboxes.arraySize + uncommonSkyboxes.arraySize + rareSkyboxes.arraySize;
            EditorGUILayout.HelpBox($"✓ {totalCount} skyboxes in buckets! Drag to reorder within each bucket.", MessageType.Info);
        }

        EditorGUILayout.Space(10);

        // Rarity Buckets Section
        EditorGUILayout.LabelField("Rarity Buckets System", headerStyle);
        EditorGUILayout.HelpBox("Toggle to enable/disable entire buckets. Only enabled buckets will be selected from.", MessageType.Info);
        
        // Common Bucket
        GUI.backgroundColor = enableCommon.boolValue ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.4f, 0.4f, 0.4f); // Green tint or gray
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("★★★ COMMON SKYBOXES", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.PropertyField(enableCommon, GUIContent.none, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.BeginDisabledGroup(!enableCommon.boolValue);
        EditorGUILayout.PropertyField(commonSkyboxes, new GUIContent($"Frequently Appearing ({commonSkyboxes.arraySize})"), true);
        EditorGUI.EndDisabledGroup();
        
        if (!enableCommon.boolValue)
        {
            EditorGUILayout.LabelField("DISABLED - Not in rotation", EditorStyles.centeredGreyMiniLabel);
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // Uncommon Bucket
        GUI.backgroundColor = enableUncommon.boolValue ? new Color(0.7f, 0.7f, 0.9f) : new Color(0.4f, 0.4f, 0.4f); // Blue tint or gray
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("★★ UNCOMMON SKYBOXES", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.PropertyField(enableUncommon, GUIContent.none, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.BeginDisabledGroup(!enableUncommon.boolValue);
        EditorGUILayout.PropertyField(uncommonSkyboxes, new GUIContent($"Occasionally Appearing ({uncommonSkyboxes.arraySize})"), true);
        EditorGUI.EndDisabledGroup();
        
        if (!enableUncommon.boolValue)
        {
            EditorGUILayout.LabelField("DISABLED - Not in rotation", EditorStyles.centeredGreyMiniLabel);
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // Rare Bucket
        GUI.backgroundColor = enableRare.boolValue ? new Color(0.95f, 0.8f, 0.4f) : new Color(0.4f, 0.4f, 0.4f); // Gold tint or gray
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("★ RARE SKYBOXES", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.PropertyField(enableRare, GUIContent.none, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.BeginDisabledGroup(!enableRare.boolValue);
        EditorGUILayout.PropertyField(rareSkyboxes, new GUIContent($"Rarely Appearing ({rareSkyboxes.arraySize})"), true);
        EditorGUI.EndDisabledGroup();
        
        if (!enableRare.boolValue)
        {
            EditorGUILayout.LabelField("DISABLED - Not in rotation", EditorStyles.centeredGreyMiniLabel);
        }
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Weight Settings
        EditorGUILayout.LabelField("Bucket Weight Multipliers", headerStyle);
        EditorGUILayout.HelpBox("Adjust how much more/less likely each bucket is. Higher = more common.", MessageType.Info);
        
        EditorGUILayout.PropertyField(commonWeight, new GUIContent("Common Weight (×)"));
        EditorGUILayout.PropertyField(uncommonWeight, new GUIContent("Uncommon Weight (×)"));
        EditorGUILayout.PropertyField(rareWeight, new GUIContent("Rare Weight (×)"));

        // Preview probabilities button
        EditorGUILayout.Space(5);
        int totalSkyboxes = commonSkyboxes.arraySize + uncommonSkyboxes.arraySize + rareSkyboxes.arraySize;
        if (totalSkyboxes > 0 && GUILayout.Button("Preview Probabilities", GUILayout.Height(25)))
        {
            PreviewProbabilities(script);
        }

        EditorGUILayout.Space(10);

        // Special Skybox Section - All Levels Unlocked
        EditorGUILayout.LabelField("⭐ Special Skybox (All Levels Unlocked)", headerStyle);
        EditorGUILayout.PropertyField(allLevelsUnlockedSkybox, new GUIContent("All Levels Unlocked Skybox"));
        EditorGUILayout.HelpBox(
            "This skybox will automatically display when ALL levels are unlocked.\n" +
            "Triggers on: Scene load (if all unlocked) or Cheat code activation.",
            MessageType.Info
        );

        EditorGUILayout.Space(10);

        // Special Skybox Section - First Time / New Game
        EditorGUILayout.LabelField("🎮 Special Skybox (First Time / New Game)", headerStyle);
        EditorGUILayout.PropertyField(firstTimeSkybox, new GUIContent("First Time Skybox"));
        EditorGUILayout.PropertyField(firstTimePrefKey, new GUIContent("PlayerPrefs Key"));
        EditorGUILayout.PropertyField(treatEditorPlayAsNewGame, new GUIContent("Treat Editor Play as New Game"));
        
        if (treatEditorPlayAsNewGame.boolValue)
        {
            EditorGUILayout.HelpBox(
                "✓ EDITOR MODE: First-time skybox will show EVERY TIME you press Play in Unity.\n" +
                "In builds, it will only show once (first time player launches the game).",
                MessageType.Info
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Editor will respect the PlayerPrefs flag (same behavior as builds).\n" +
                "Use the Reset button below to test first-time experience again.",
                MessageType.Info
            );
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Priority: First Time (once per session) > All Levels Unlocked > Random Selection",
            MessageType.None
        );
        
        // Show session status during play mode
        if (EditorApplication.isPlaying)
        {
            bool hasShownThisSession = script.HasShownFirstTimeSkyboxThisSession();
            string sessionStatus = hasShownThisSession 
                ? "✓ First-time skybox already shown this session" 
                : "⚠ First-time skybox has not been shown yet this session";
            MessageType sessionType = hasShownThisSession ? MessageType.Info : MessageType.Warning;
            EditorGUILayout.HelpBox(sessionStatus, sessionType);
        }
        
        // Testing buttons
        EditorGUILayout.BeginHorizontal();
        
        // Check current status
        bool hasLaunched = PlayerPrefs.HasKey(firstTimePrefKey.stringValue) && 
                          PlayerPrefs.GetInt(firstTimePrefKey.stringValue, 0) == 1;
        
        string statusText = hasLaunched ? "✓ Game has been launched before" : "⚠ First launch will trigger special skybox";
        MessageType statusType = hasLaunched ? MessageType.Info : MessageType.Warning;
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(statusText, statusType);
        
        // Reset buttons (for testing)
        EditorGUILayout.BeginHorizontal();
        
        // Reset session flag button
        GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
        if (GUILayout.Button("🔄 Reset Session Flag", GUILayout.Height(30)))
        {
            script.ResetSessionFlag();
            EditorUtility.DisplayDialog("Session Reset", "Session flag has been reset. Next title screen load will show first-time skybox.", "OK");
        }
        
        // Reset first launch flag button
        GUI.backgroundColor = new Color(1f, 0.6f, 0.3f);
        if (GUILayout.Button("🔄 Reset First Launch Flag", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Reset First Launch Flag?",
                "This will mark the game as 'not launched yet' AND reset the session flag.\n\nAre you sure?",
                "Yes, Reset",
                "Cancel"))
            {
                script.ResetFirstLaunchFlag();
                EditorUtility.DisplayDialog("Reset Complete", "First launch flag has been reset. The next launch will be treated as first time.", "OK");
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.HelpBox(
            "Session Flag: Resets when you stop Play mode. Use to test returning to title screen.\n" +
            "First Launch Flag: Persists across Play sessions. Use to test true first-time experience.",
            MessageType.None
        );

        EditorGUILayout.Space(10);

        // Settings Section
        EditorGUILayout.LabelField("Settings", headerStyle);
        EditorGUILayout.PropertyField(randomizeOnStart);

        EditorGUILayout.Space(10);

        // Debug Section
        EditorGUILayout.LabelField("Debug Controls", headerStyle);
        EditorGUILayout.PropertyField(enableDebugControls);
        EditorGUILayout.PropertyField(requireAllLevelsUnlockedForCycling, new GUIContent("Require All Levels for C Key"));
        EditorGUILayout.PropertyField(showWeightInfo);

        if (enableDebugControls.boolValue)
        {
            if (requireAllLevelsUnlockedForCycling.boolValue)
            {
                EditorGUILayout.HelpBox("Press 'C' key or Right Stick Click to cycle skyboxes (ONLY when all levels unlocked).", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Press 'C' key or Right Stick Click to randomly select a new skybox during play (respects rarity weights).", MessageType.Info);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private string GetCurrentSkyboxRarity(SkyboxRandomizer script, Material currentSkybox)
    {
        if (currentSkybox == null)
            return "";

        // Check common bucket
        if (script.enableCommon && script.commonSkyboxes != null)
        {
            foreach (Material mat in script.commonSkyboxes)
            {
                if (mat == currentSkybox)
                    return "★★★ COMMON";
            }
        }

        // Check uncommon bucket
        if (script.enableUncommon && script.uncommonSkyboxes != null)
        {
            foreach (Material mat in script.uncommonSkyboxes)
            {
                if (mat == currentSkybox)
                    return "★★ UNCOMMON";
            }
        }

        // Check rare bucket
        if (script.enableRare && script.rareSkyboxes != null)
        {
            foreach (Material mat in script.rareSkyboxes)
            {
                if (mat == currentSkybox)
                    return "★ RARE";
            }
        }

        // Check disabled buckets
        if (!script.enableCommon && script.commonSkyboxes != null)
        {
            foreach (Material mat in script.commonSkyboxes)
            {
                if (mat == currentSkybox)
                    return "★★★ COMMON (Disabled)";
            }
        }

        if (!script.enableUncommon && script.uncommonSkyboxes != null)
        {
            foreach (Material mat in script.uncommonSkyboxes)
            {
                if (mat == currentSkybox)
                    return "★★ UNCOMMON (Disabled)";
            }
        }

        if (!script.enableRare && script.rareSkyboxes != null)
        {
            foreach (Material mat in script.rareSkyboxes)
            {
                if (mat == currentSkybox)
                    return "★ RARE (Disabled)";
            }
        }

        return "Not in any bucket";
    }

    private Color GetRarityColor(string rarity)
    {
        if (rarity.Contains("COMMON"))
            return new Color(0.5f, 0.8f, 0.5f); // Green
        else if (rarity.Contains("UNCOMMON"))
            return new Color(0.7f, 0.7f, 0.9f); // Blue
        else if (rarity.Contains("RARE"))
            return new Color(0.95f, 0.8f, 0.4f); // Gold
        else
            return new Color(0.6f, 0.6f, 0.6f); // Gray
    }

    private void PreviewProbabilities(SkyboxRandomizer script)
    {
        // Force show weight info temporarily
        bool originalShowWeight = script.showWeightInfo;
        script.showWeightInfo = true;
        
        // Get the private method via reflection
        var method = typeof(SkyboxRandomizer).GetMethod("LogWeightDistribution", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method != null)
        {
            method.Invoke(script, null);
        }
        else
        {
// Debug.LogWarning("Could not preview probabilities - method not found.");
        }
        
        script.showWeightInfo = originalShowWeight;
    }
}
