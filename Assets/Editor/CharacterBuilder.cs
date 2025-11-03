using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

/// <summary>
/// Automatically builds a functional ragdoll character from a Blender FBX.
/// Creates EXACT replica of Default Character prefab structure.
/// </summary>
public class CharacterBuilder : EditorWindow
{
    [MenuItem("Tools/Character Builder")]
    public static void ShowWindow()
    {
        var window = GetWindow<CharacterBuilder>("Character Builder");
        window.minSize = new Vector2(500, 700);
        window.Show();
    }
    
    private GameObject sourceModel;
    private string characterName = "New Character";
    private Vector2 scrollPosition;
    private ValidationResult validation;
    private const int PHYSICS_LAYER = 8; // Layer for physics objects
    
    private class ValidationResult
    {
        public bool isValid = false;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> info = new List<string>();
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        GUILayout.Label("Character Builder - Exact Default Character Clone", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.HelpBox(
            "This tool converts your Blender FBX into a fully functional character " +
            "with IDENTICAL setup to Default Character: dual-body system, physics, " +
            "all modules, animations, and controls.",
            MessageType.Info);
        
        EditorGUILayout.Space();
        
        // Input fields
        sourceModel = EditorGUILayout.ObjectField("Source Model (FBX)", sourceModel, typeof(GameObject), false) as GameObject;
        characterName = EditorGUILayout.TextField("Character Name", characterName);
        
        EditorGUILayout.Space();
        
        // Validation
        if (sourceModel != null)
        {
            if (GUILayout.Button("🔍 Validate Model", GUILayout.Height(30)))
            {
                validation = ValidateModel(sourceModel);
            }
            
            if (validation != null)
            {
                EditorGUILayout.Space();
                DisplayValidation(validation);
            }
            
            EditorGUILayout.Space();
            
            GUI.enabled = validation != null && validation.isValid;
            if (GUILayout.Button("✨ BUILD CHARACTER ✨", GUILayout.Height(50)))
            {
                BuildCharacter();
            }
            GUI.enabled = true;
        }
        else
        {
            EditorGUILayout.HelpBox(
                "STEP 1: Export your character from Blender as FBX\n" +
                "STEP 2: Drag it into the 'Source Model' field above\n" +
                "STEP 3: Click 'Validate Model'\n" +
                "STEP 4: Fix any errors, then click 'Build Character'",
                MessageType.Info);
        }
        
        EditorGUILayout.Space();
        DrawRequirements();
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DisplayValidation(ValidationResult result)
    {
        if (result.errors.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("❌ ERRORS - Cannot Build", EditorStyles.boldLabel);
            foreach (var error in result.errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            EditorGUILayout.EndVertical();
        }
        
        if (result.warnings.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("⚠️ WARNINGS", EditorStyles.boldLabel);
            foreach (var warning in result.warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }
        
        if (result.isValid)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("✅ MODEL READY TO BUILD", EditorStyles.boldLabel);
            foreach (var info in result.info)
            {
                EditorGUILayout.LabelField("• " + info);
            }
            EditorGUILayout.EndVertical();
        }
    }
    
    private ValidationResult ValidateModel(GameObject model)
    {
        var result = new ValidationResult();
        
        // Check if it's an FBX
        string assetPath = AssetDatabase.GetAssetPath(model);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.ToLower().EndsWith(".fbx"))
        {
            result.errors.Add("Must be an FBX file from your Project window.");
            return result;
        }
        
        result.info.Add($"FBX: {System.IO.Path.GetFileName(assetPath)}");
        
        // Check for Animator/Humanoid
        Animator animator = model.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            result.errors.Add("Model must be Humanoid rig.");
            result.errors.Add("FIX: Select FBX → Inspector → Rig → Animation Type: Humanoid → Apply");
            return result;
        }
        
        result.info.Add("✓ Humanoid rig configured");
        
        // Check required bones
        var requiredBones = new[] {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
        };
        
        foreach (var bone in requiredBones)
        {
            if (animator.GetBoneTransform(bone) == null)
            {
                result.errors.Add($"Missing bone: {bone}");
                return result;
            }
        }
        
        result.info.Add($"✓ All {requiredBones.Length} bones mapped");
        
        // Check for mesh
        SkinnedMeshRenderer meshRenderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
        if (meshRenderer == null || meshRenderer.sharedMesh == null)
        {
            result.errors.Add("No skinned mesh found.");
            result.errors.Add("FIX: Export mesh with armature from Blender");
            return result;
        }
        
        result.info.Add($"✓ Mesh: {meshRenderer.sharedMesh.vertexCount} vertices");
        
        // Check UVs
        Vector2[] uvs = meshRenderer.sharedMesh.uv;
        if (uvs == null || uvs.Length == 0)
        {
            result.warnings.Add("No UV coordinates - you won't be able to texture this!");
        }
        else
        {
            result.info.Add($"✓ UVs: {uvs.Length} coordinates");
        }
        
        // Check bone weights
        if (meshRenderer.bones == null || meshRenderer.bones.Length == 0)
        {
            result.errors.Add("No bone weights - mesh not rigged!");
            result.errors.Add("FIX: In Blender, ensure vertex groups match bone names");
            return result;
        }
        
        result.info.Add($"✓ Rigged to {meshRenderer.bones.Length} bones");
        
        // CRITICAL: Verify bone weights are actually present in mesh data
        BoneWeight[] boneWeights = meshRenderer.sharedMesh.boneWeights;
        if (boneWeights == null || boneWeights.Length == 0)
        {
            result.errors.Add("CRITICAL: Mesh has NO bone weight data!");
            result.errors.Add("This means vertices won't deform with skeleton.");
            result.errors.Add("FIX: In Blender:");
            result.errors.Add("  1. Make sure you edited the EXISTING rigged mesh (DemoModel.fbx)");
            result.errors.Add("  2. Don't create new geometry - only modify UVs");
            result.errors.Add("  3. Vertex groups must exist and have painted weights");
            result.errors.Add("  4. Export with 'Armature Deform' enabled");
            return result;
        }
        
        result.info.Add($"✓ Bone weights present: {boneWeights.Length} weighted vertices");
        
        // Check if bone weights are reasonable
        int unweightedVerts = 0;
        foreach (var weight in boneWeights)
        {
            float totalWeight = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
            if (totalWeight < 0.01f)
            {
                unweightedVerts++;
            }
        }
        
        if (unweightedVerts > boneWeights.Length * 0.1f)
        {
            result.warnings.Add($"Warning: {unweightedVerts}/{boneWeights.Length} vertices have no bone weights!");
            result.warnings.Add("These vertices won't move with the skeleton.");
            result.warnings.Add("Check vertex group weights in Blender.");
        }
        else if (unweightedVerts > 0)
        {
            result.info.Add($"Note: {unweightedVerts} vertices have minimal weights (might be OK)");
        }
        else
        {
            result.info.Add("✓ All vertices properly weighted");
        }
        
        result.isValid = true;
        return result;
    }
    
    private void BuildCharacter()
    {
        if (sourceModel == null || validation == null || !validation.isValid)
        {
            EditorUtility.DisplayDialog("Error", "Validate the model first!", "OK");
            return;
        }
        
        try
        {
            Debug.Log("=== STARTING CHARACTER BUILD ===");
            
            EditorUtility.DisplayProgressBar("Building Character", "Creating structure...", 0.1f);
            
            // Create root
            Debug.Log("Creating root GameObject...");
            GameObject root = new GameObject(characterName);
            root.layer = PHYSICS_LAYER;
            Debug.Log($"✓ Root created: {root.name}");
            
            EditorUtility.DisplayProgressBar("Building Character", "Creating Animated body...", 0.2f);
            Debug.Log("Creating Animated body...");
            GameObject animated = CreateAnimatedBody(root.transform);
            if (animated == null)
            {
                throw new System.Exception("CreateAnimatedBody returned null!");
            }
            Debug.Log($"✓ Animated body created with {animated.transform.childCount} children");
            
            EditorUtility.DisplayProgressBar("Building Character", "Creating Physical body...", 0.3f);
            Debug.Log("Creating Physical body...");
            GameObject physical = CreatePhysicalBody(root.transform);
            if (physical == null)
            {
                throw new System.Exception("CreatePhysicalBody returned null!");
            }
            Debug.Log($"✓ Physical body created with {physical.transform.childCount} children");
            
            EditorUtility.DisplayProgressBar("Building Character", "Creating Camera...", 0.4f);
            Debug.Log("Creating Camera...");
            GameObject camera = CreateCameraModule(root.transform);
            if (camera == null)
            {
                throw new System.Exception("CreateCameraModule returned null!");
            }
            Debug.Log($"✓ Camera created: {camera.name}");
            
            EditorUtility.DisplayProgressBar("Building Character", "Adding physics...", 0.5f);
            Debug.Log("Adding physics to skeleton...");
            Transform playerSkeleton = physical.transform.Find("Player");
            if (playerSkeleton == null)
            {
                throw new System.Exception("Could not find 'Player' in Physical body! Check CreatePhysicalBody.");
            }
            Debug.Log($"✓ Found Player skeleton at: Physical/{playerSkeleton.name}");
            AddPhysicsToSkeleton(playerSkeleton);
            Debug.Log("✓ Physics added");
            
            EditorUtility.DisplayProgressBar("Building Character", "Configuring components...", 0.7f);
            Debug.Log("Configuring components...");
            ConfigureAllComponents(root, animated, physical, camera);
            Debug.Log("✓ Components configured");
            
            EditorUtility.DisplayProgressBar("Building Character", "Saving prefab...", 0.9f);
            string prefabPath = $"Assets/Prefabs/{characterName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            
            EditorUtility.ClearProgressBar();
            
            Debug.Log($"✅ Character '{characterName}' built successfully!");
            Debug.Log($"📁 Prefab: {prefabPath}");
            Debug.Log($"🎮 Ready to use - drag into scene and press Play!");
            
            EditorUtility.DisplayDialog("Success!", 
                $"Character '{characterName}' created!\n\n" +
                $"• Prefab saved to: {prefabPath}\n" +
                $"• Drag it into a scene to test\n" +
                $"• All controls and physics configured\n" +
                $"• Works exactly like Default Character!",
                "Awesome!");
            
            Selection.activeGameObject = root;
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"Build failed: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("Error", $"Build failed:\n\n{e.Message}", "OK");
        }
    }
    
    private GameObject CreateAnimatedBody(Transform parent)
    {
        GameObject animated = new GameObject("Animated");
        animated.transform.SetParent(parent);
        animated.transform.localPosition = Vector3.zero;
        animated.transform.localRotation = Quaternion.identity;
        
        // Find the source Animator and Avatar
        Animator sourceAnimator = sourceModel.GetComponentInChildren<Animator>();
        if (sourceAnimator == null)
        {
            Debug.LogError("No Animator found in source model!");
            return animated;
        }
        
        // CRITICAL: Find the GameObject with the bone hierarchy (Hips bone)
        Transform bonesRoot = sourceAnimator.GetBoneTransform(HumanBodyBones.Hips);
        if (bonesRoot == null)
        {
            Debug.LogError("No Hips bone found in Animator!");
            return animated;
        }
        
        Debug.Log($"Found Hips at: {bonesRoot.name}");
        Debug.Log($"Hips parent: {(bonesRoot.parent != null ? bonesRoot.parent.name : "NULL")}");
        Debug.Log($"Source model: {sourceModel.name}");
        
        // Find the armature root
        // If Hips has a parent, use it (typical case: Player/Torso)
        // If Hips IS the root of the model, use the sourceModel's first child named "Player"
        Transform armatureRoot = bonesRoot.parent;
        
        if (armatureRoot == null || armatureRoot == sourceModel.transform)
        {
            // Hips is at root level or parent is FBX root - find or create armature
            Debug.Log("Hips parent is root, searching for Player child...");
            armatureRoot = sourceModel.transform.Find("Player");
            
            if (armatureRoot == null)
            {
                Debug.LogError($"Could not find armature! Hips: {bonesRoot.name}, Parent: {(bonesRoot.parent != null ? bonesRoot.parent.name : "NULL")}");
                return animated;
            }
        }
        
        Debug.Log($"Using armature root: {armatureRoot.name}");
        
        // Use regular Instantiate (PrefabUtility.InstantiatePrefab instantiates the entire FBX root)
        GameObject skeleton = Instantiate(armatureRoot.gameObject, animated.transform);
        skeleton.name = "Player";
        Debug.Log($"Instantiated skeleton: {skeleton.name}, children: {skeleton.transform.childCount}");
        
        // Verify source Avatar is valid
        if (sourceAnimator.avatar == null)
        {
            Debug.LogError("Source Animator has no Avatar! Check FBX import settings - should be Humanoid rig.");
            return animated;
        }
        
        if (!sourceAnimator.avatar.isHuman)
        {
            Debug.LogError("Source Avatar is not Humanoid! Check FBX import settings.");
            return animated;
        }
        
        Debug.Log($"Source Avatar: {sourceAnimator.avatar.name} (isHuman: {sourceAnimator.avatar.isHuman})");
        
        // Add Animator to the skeleton if it doesn't have one
        Animator animator = skeleton.GetComponent<Animator>();
        if (animator == null)
        {
            animator = skeleton.AddComponent<Animator>();
            Debug.Log("Added new Animator to skeleton");
        }
        else
        {
            Debug.Log("Skeleton already has Animator");
        }
        
        animator.avatar = sourceAnimator.avatar;
        animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
        animator.applyRootMotion = false;
        
        Debug.Log($"Set Avatar on Animated/Player: {animator.avatar.name}");
        
        // CRITICAL: Verify the Avatar is actually working
        Transform testHips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (testHips == null)
        {
            Debug.LogError("PROBLEM: After assigning Avatar, Animator.GetBoneTransform(Hips) returns NULL!");
            Debug.LogError($"  Animator: {animator.name}");
            Debug.LogError($"  Avatar: {animator.avatar.name}");
            Debug.LogError($"  Avatar.isValid: {animator.avatar.isValid}");
            Debug.LogError($"  Avatar.isHuman: {animator.avatar.isHuman}");
            
            // Try to find Torso/Hips manually in the hierarchy
            Transform manualHips = skeleton.transform.Find("Torso");
            if (manualHips != null)
            {
                Debug.LogError($"  BUT I can find 'Torso' manually at: {manualHips.name}");
            }
            
            Debug.LogError("  This means the Avatar doesn't map to this instantiated skeleton!");
            Debug.LogError("  FIX: The Avatar might be looking for transforms by instance ID, not by name.");
        }
        else
        {
            Debug.Log($"✓ Avatar working! GetBoneTransform(Hips) = {testHips.name}");
        }
        
        // Remove all renderers (animated body is invisible)
        foreach (var renderer in skeleton.GetComponentsInChildren<Renderer>())
        {
            DestroyImmediate(renderer);
        }
        
        return animated;
    }
    
    private GameObject CreatePhysicalBody(Transform parent)
    {
        Debug.Log("[CreatePhysicalBody] Starting...");
        
        GameObject physical = new GameObject("Physical");
        physical.transform.SetParent(parent);
        physical.transform.localPosition = Vector3.zero;
        physical.transform.localRotation = Quaternion.identity;
        
        // Find mesh from source
        SkinnedMeshRenderer sourceMesh = sourceModel.GetComponentInChildren<SkinnedMeshRenderer>();
        Debug.Log($"[CreatePhysicalBody] Found mesh: {sourceMesh.gameObject.name}");
        
        // Copy mesh object using regular Instantiate
        GameObject meshObj = Instantiate(sourceMesh.gameObject, physical.transform);
        meshObj.name = sourceMesh.gameObject.name;
        Debug.Log($"[CreatePhysicalBody] Mesh instantiated as child: {meshObj.name}");
        
        // Find the source Animator and Avatar
        Animator sourceAnimator = sourceModel.GetComponentInChildren<Animator>();
        if (sourceAnimator == null)
        {
            Debug.LogError("[CreatePhysicalBody] No Animator found in source model!");
            return physical;
        }
        Debug.Log($"[CreatePhysicalBody] Found source Animator on: {sourceAnimator.gameObject.name}");
        
        // DIRECT APPROACH: Just find the "Player" armature by name in the FBX
        // (We can't use GetBoneTransform because Avatar mappings don't work until runtime)
        Transform armatureRoot = sourceModel.transform.Find("Player");
        
        if (armatureRoot == null)
        {
            Debug.LogError("[CreatePhysicalBody] Could not find 'Player' child in source FBX!");
            Debug.LogError($"[CreatePhysicalBody] Source FBX children:");
            foreach (Transform child in sourceModel.transform)
            {
                Debug.LogError($"  - {child.name}");
            }
            return physical;
        }
        
        Debug.Log($"[CreatePhysicalBody] Found armature: {armatureRoot.name}");
        
        // Use regular Instantiate (PrefabUtility.InstantiatePrefab instantiates the entire FBX root)
        GameObject skeleton = Instantiate(armatureRoot.gameObject, physical.transform);
        skeleton.name = "Player";
        Debug.Log($"[CreatePhysicalBody] Instantiated skeleton: {skeleton.name}, children: {skeleton.transform.childCount}");
        
        // Verify source Avatar is valid (should already be checked in CreateAnimatedBody, but double-check)
        if (sourceAnimator.avatar == null || !sourceAnimator.avatar.isHuman)
        {
            Debug.LogError("Source Avatar invalid in CreatePhysicalBody!");
            return physical;
        }
        
        // Add Animator to the skeleton if it doesn't have one
        Animator animator = skeleton.GetComponent<Animator>();
        if (animator == null)
        {
            animator = skeleton.AddComponent<Animator>();
        }
        
        animator.avatar = sourceAnimator.avatar;
        animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
        animator.applyRootMotion = false;
        
        Debug.Log($"[CreatePhysicalBody] Set Avatar on Physical/Player: {animator.avatar.name}");
        
        // Remove mesh from skeleton (we have it separate)
        foreach (var renderer in skeleton.GetComponentsInChildren<Renderer>())
        {
            DestroyImmediate(renderer);
        }
        
        // Relink mesh to physical skeleton
        SkinnedMeshRenderer physicalMeshRenderer = meshObj.GetComponent<SkinnedMeshRenderer>();
        RelinkMeshToSkeleton(physicalMeshRenderer, skeleton.transform);
        
        Debug.Log($"[CreatePhysicalBody] ✓ Complete! Physical has {physical.transform.childCount} children:");
        foreach (Transform child in physical.transform)
        {
            Debug.Log($"[CreatePhysicalBody]   - {child.name}");
        }
        
        return physical;
    }
    
    private void RelinkMeshToSkeleton(SkinnedMeshRenderer meshRenderer, Transform newSkeleton)
    {
        Transform[] oldBones = meshRenderer.bones;
        Transform[] newBones = new Transform[oldBones.Length];
        
        for (int i = 0; i < oldBones.Length; i++)
        {
            if (oldBones[i] != null)
            {
                newBones[i] = FindChildRecursive(newSkeleton, oldBones[i].name);
            }
        }
        
        meshRenderer.bones = newBones;
        
        if (meshRenderer.rootBone != null)
        {
            meshRenderer.rootBone = FindChildRecursive(newSkeleton, meshRenderer.rootBone.name);
        }
    }
    
    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        
        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        
        return null;
    }
    
    private GameObject CreateCameraModule(Transform parent)
    {
        GameObject cameraObj = new GameObject("Active Ragdoll Camera");
        cameraObj.transform.SetParent(parent);
        cameraObj.transform.localPosition = new Vector3(0, 1.3f, -2.27f); // Behind and above character
        cameraObj.transform.localRotation = Quaternion.identity;
        
        // Add camera with settings matching Default Character
        Camera cam = cameraObj.AddComponent<Camera>();
        cam.fieldOfView = 76; // Match Default Character
        cam.nearClipPlane = 0.3f; // Match Default Character
        cam.farClipPlane = 1000f;
        cam.clearFlags = CameraClearFlags.Skybox;
        
        // IMPORTANT: AudioListener goes on ROOT, not camera!
        // Camera should NOT have Rigidbody or AudioListener!
        // CameraModule script will handle all positioning
        
        return cameraObj;
    }
    
    private void AddPhysicsToSkeleton(Transform skeleton)
    {
        Debug.Log($"[AddPhysicsToSkeleton] Adding physics to: {skeleton.name}");
        
        Animator animator = skeleton.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[AddPhysicsToSkeleton] No Animator on skeleton!");
            return;
        }
        
        // NOTE: Can't check animator.isHuman because Avatar mappings don't work after Instantiate()
        // Instead, we'll find bones by name
        Debug.Log($"[AddPhysicsToSkeleton] Animator found, Avatar: {(animator.avatar != null ? animator.avatar.name : "NULL")}");
        
        // EXACT CONFIGURATION FROM DEFAULT CHARACTER PREFAB
        // Format: bone, mass, parent, size, center, low_x, high_x, y_limit, z_limit, spring, damper
        
        // Torso (Hips is root - no joint)
        AddBonePhysics(animator, HumanBodyBones.Hips, 17f, null, 
            new Vector3(0.28f,0.21f,0.2f), new Vector3(0f,0.1f,0f), 0,0,0,0, 0,0);
        AddBonePhysics(animator, HumanBodyBones.Spine, 14f, HumanBodyBones.Hips, 
            new Vector3(0.48f,0.28f,0.16f), new Vector3(0f,0.13f,0f), -29.19f,33.45f,27.83f,22.33f, 1000,5);
        AddBonePhysics(animator, HumanBodyBones.Neck, 2.5f, HumanBodyBones.Spine, 
            new Vector3(0.14f,0.08f,0.11f), new Vector3(0f,0.02f,0.005f), -19.79f,11.05f,17.03f,14.93f, 200,5);
        AddBonePhysics(animator, HumanBodyBones.Head, 3.2f, HumanBodyBones.Neck, 
            new Vector3(0.29f,0.29f,0.25f), new Vector3(0f,0.15f,0f), -34.62f,26.76f,47.71f,23.11f, 150,5);
        
        // Arms
        AddBonePhysics(animator, HumanBodyBones.LeftUpperArm, 3f, HumanBodyBones.Spine, 
            new Vector3(0.09f,0.31f,0.1f), new Vector3(0f,0.13f,0f), -57.24f,177f,114.28f,99.53f, 500,5);
        AddBonePhysics(animator, HumanBodyBones.LeftLowerArm, 1.5f, HumanBodyBones.LeftUpperArm, 
            new Vector3(0.08f,0.24f,0.09f), new Vector3(0f,0.13f,0f), -120f,0f,20f,75f, 300,5);
        AddBonePhysics(animator, HumanBodyBones.LeftHand, 0.5f, HumanBodyBones.LeftLowerArm, 
            new Vector3(0.1f,0.06f,0.11f), new Vector3(0f,0.03f,0f), -16.53f,28.41f,15.79f,16.37f, 120,5);
        
        AddBonePhysics(animator, HumanBodyBones.RightUpperArm, 3f, HumanBodyBones.Spine, 
            new Vector3(0.09f,0.31f,0.1f), new Vector3(0f,0.13f,0f), -57.24f,177f,114.28f,99.53f, 500,5);
        AddBonePhysics(animator, HumanBodyBones.RightLowerArm, 1.5f, HumanBodyBones.RightUpperArm, 
            new Vector3(0.08f,0.24f,0.09f), new Vector3(0f,0.13f,0f), -120f,0f,20f,75f, 300,5);
        AddBonePhysics(animator, HumanBodyBones.RightHand, 0.5f, HumanBodyBones.RightLowerArm, 
            new Vector3(0.1f,0.06f,0.11f), new Vector3(0f,0.03f,0f), -16.53f,28.41f,15.79f,16.37f, 120,5);
        
        // Legs
        AddBonePhysics(animator, HumanBodyBones.LeftUpperLeg, 7f, HumanBodyBones.Hips, 
            new Vector3(0.13f,0.25f,0.13f), new Vector3(0f,0.12f,0f), -77.70f,32f,15.67f,18.71f, 1500,10);
        AddBonePhysics(animator, HumanBodyBones.LeftLowerLeg, 3.3f, HumanBodyBones.LeftUpperLeg, 
            new Vector3(0.11f,0.25f,0.1f), new Vector3(0f,0.15f,0f), -120f,0f,0f,0f, 400,0);
        AddBonePhysics(animator, HumanBodyBones.LeftFoot, 1f, HumanBodyBones.LeftLowerLeg, 
            new Vector3(0.08f,0.15f,0.12f), new Vector3(0.015f,0.03f,0f), -21.93f,66.92f,21.62f,23.90f, 400,5);
        
        AddBonePhysics(animator, HumanBodyBones.RightUpperLeg, 7f, HumanBodyBones.Hips, 
            new Vector3(0.13f,0.25f,0.13f), new Vector3(0f,0.12f,0f), -77.70f,32f,15.67f,18.71f, 1500,10);
        AddBonePhysics(animator, HumanBodyBones.RightLowerLeg, 3.3f, HumanBodyBones.RightUpperLeg, 
            new Vector3(0.11f,0.25f,0.1f), new Vector3(0f,0.15f,0f), -120f,0f,0f,0f, 400,0);
        AddBonePhysics(animator, HumanBodyBones.RightFoot, 1f, HumanBodyBones.RightLowerLeg, 
            new Vector3(0.08f,0.15f,0.12f), new Vector3(-0.015f,0.03f,0f), -21.93f,66.92f,21.62f,23.90f, 400,5);
    }
    
    private enum ColliderType { Box, Sphere, Capsule }
    
    // Helper: Find bone by name (since GetBoneTransform doesn't work after Instantiate)
    private Transform FindBoneByName(Transform skeleton, HumanBodyBones bone)
    {
        // Map HumanBodyBones to actual bone names in the FBX
        string boneName = bone switch
        {
            HumanBodyBones.Hips => "Torso",
            HumanBodyBones.Spine => "Chest",
            HumanBodyBones.Head => "Head",
            HumanBodyBones.Neck => "Neck",
            HumanBodyBones.LeftUpperArm => "Arm.L",
            HumanBodyBones.LeftLowerArm => "Forearm.L",
            HumanBodyBones.LeftHand => "Hand.L",
            HumanBodyBones.RightUpperArm => "Arm.R",
            HumanBodyBones.RightLowerArm => "Forearm.R",
            HumanBodyBones.RightHand => "Hand.R",
            HumanBodyBones.LeftUpperLeg => "Thigh.L",
            HumanBodyBones.LeftLowerLeg => "Calve.L",
            HumanBodyBones.LeftFoot => "Foot.L",
            HumanBodyBones.RightUpperLeg => "Thigh.R",
            HumanBodyBones.RightLowerLeg => "Calve.R",
            HumanBodyBones.RightFoot => "Foot.R",
            _ => null
        };
        
        if (boneName == null) return null;
        
        // Search recursively
        Transform found = skeleton.Find(boneName);
        if (found != null) return found;
        
        // If not direct child, search deeper
        foreach (Transform child in skeleton.GetComponentsInChildren<Transform>())
        {
            if (child.name == boneName) return child;
        }
        
        return null;
    }
    
    private void AddBonePhysics(Animator animator, HumanBodyBones bone, float mass, 
                                 HumanBodyBones? connectedBone, Vector3 colliderSize, Vector3 colliderCenter,
                                 float lowAngularX, float highAngularX, float angularY, float angularZ,
                                 float spring, float damper)
    {
        // Use name-based lookup instead of GetBoneTransform
        Transform boneTransform = FindBoneByName(animator.transform, bone);
        if (boneTransform == null)
        {
            Debug.LogWarning($"[AddBonePhysics] Could not find bone: {bone}");
            return;
        }
        
        Debug.Log($"[AddBonePhysics] Adding physics to {bone} ({boneTransform.name}): mass={mass}kg");
        
        // Set layer
        boneTransform.gameObject.layer = PHYSICS_LAYER;
        
        // Add Rigidbody with proper settings to prevent spazzing
        Rigidbody rb = boneTransform.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0; // No linear drag
        rb.angularDamping = 0.05f; // Small angular drag
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.constraints = RigidbodyConstraints.None; // No constraints
        
        // CRITICAL: These settings prevent instability
        rb.solverIterations = 12; // Higher = more stable
        rb.solverVelocityIterations = 4;
        rb.maxAngularVelocity = 50;
        rb.maxDepenetrationVelocity = 10f; // Prevents explosions
        
        // Add Collider with EXACT size and center offset from Default Character
        BoxCollider boxCol = boneTransform.gameObject.AddComponent<BoxCollider>();
        boxCol.size = colliderSize;
        boxCol.center = colliderCenter; // CRITICAL: Center offset positions collider along bone!
        
        // Add ConfigurableJoint if connected to another bone
        if (connectedBone.HasValue)
        {
            // Use name-based lookup
            Transform connectedTransform = FindBoneByName(animator.transform, connectedBone.Value);
            if (connectedTransform != null)
            {
                Rigidbody connectedRb = connectedTransform.GetComponent<Rigidbody>();
                if (connectedRb != null)
                {
                    Debug.Log($"[AddBonePhysics]   Connecting to parent: {connectedTransform.name}");
                    ConfigurableJoint joint = boneTransform.gameObject.AddComponent<ConfigurableJoint>();
                    joint.connectedBody = connectedRb;
                    joint.autoConfigureConnectedAnchor = true;
                    
                    // Lock all position movement (only rotation allowed)
                    joint.xMotion = ConfigurableJointMotion.Locked;
                    joint.yMotion = ConfigurableJointMotion.Locked;
                    joint.zMotion = ConfigurableJointMotion.Locked;
                    
                    // Allow limited rotation (for ragdoll)
                    joint.angularXMotion = ConfigurableJointMotion.Limited;
                    joint.angularYMotion = ConfigurableJointMotion.Limited;
                    joint.angularZMotion = ConfigurableJointMotion.Limited;
                    
                    // CRITICAL: Use EXACT limits from Default Character for this specific bone
                    SoftJointLimit lowAngularXLimit = new SoftJointLimit();
                    lowAngularXLimit.limit = lowAngularX;
                    lowAngularXLimit.bounciness = 0f;
                    lowAngularXLimit.contactDistance = 10f;
                    joint.lowAngularXLimit = lowAngularXLimit;
                    
                    SoftJointLimit highAngularXLimit = new SoftJointLimit();
                    highAngularXLimit.limit = highAngularX;
                    highAngularXLimit.bounciness = 0f;
                    highAngularXLimit.contactDistance = 10f;
                    joint.highAngularXLimit = highAngularXLimit;
                    
                    SoftJointLimit angularYLimitStruct = new SoftJointLimit();
                    angularYLimitStruct.limit = angularY;
                    angularYLimitStruct.bounciness = 0f;
                    angularYLimitStruct.contactDistance = 10f;
                    joint.angularYLimit = angularYLimitStruct;
                    
                    SoftJointLimit angularZLimitStruct = new SoftJointLimit();
                    angularZLimitStruct.limit = angularZ;
                    angularZLimitStruct.bounciness = 0f;
                    angularZLimitStruct.contactDistance = 10f;
                    joint.angularZLimit = angularZLimitStruct;
                    
                    // CRITICAL: Use EXACT spring/damper from Default Character for this specific bone
                    var drive = new JointDrive();
                    drive.positionSpring = spring;
                    drive.positionDamper = damper;
                    drive.maximumForce = float.MaxValue;
                    drive.useAcceleration = false;
                    
                    joint.angularXDrive = drive;
                    joint.angularYZDrive = drive;
                    
                    // CRITICAL: Match Default Character projection settings
                    joint.projectionMode = JointProjectionMode.None;
                    joint.projectionDistance = 0.1f;
                    joint.projectionAngle = 180f;
                    
                    // CRITICAL: Disable collision between connected bodies (prevents fighting!)
                    joint.enableCollision = false;
                    
                    // CRITICAL: Disable preprocessing (match Default Character)
                    joint.enablePreprocessing = false;
                }
            }
        }
    }
    
    private void ConfigureAllComponents(GameObject root, GameObject animated, GameObject physical, GameObject cameraObj)
    {
        Debug.Log("Configuring all components...");
        
        // Ensure root is on Layer 8
        root.layer = PHYSICS_LAYER;
        
        // Get references with null checks
        Debug.Log("  Finding Animated/Player...");
        Transform animatedPlayer = animated.transform.Find("Player");
        if (animatedPlayer == null)
        {
            throw new System.Exception("Could not find 'Player' in Animated body!");
        }
        Debug.Log($"  ✓ Found: {animatedPlayer.name}");
        
        Debug.Log("  Getting Animated Animator...");
        Animator animatedAnimator = animatedPlayer.GetComponent<Animator>();
        if (animatedAnimator == null)
        {
            throw new System.Exception("Animated/Player has no Animator component!");
        }
        Debug.Log($"  ✓ Animator found, Avatar: {(animatedAnimator.avatar != null ? animatedAnimator.avatar.name : "NULL")}");
        
        // Find Hips bone by name (Avatar.GetBoneTransform doesn't work on instantiated objects)
        Debug.Log("  Finding Animated Hips bone by name...");
        Transform animatedTorso = animatedPlayer.Find("Torso");
        if (animatedTorso == null)
        {
            throw new System.Exception("Animated/Player has no 'Torso' child! Check FBX structure.");
        }
        Debug.Log($"  ✓ Animated Hips: {animatedTorso.name}");
        
        Debug.Log("  Finding Physical/Player...");
        Transform physicalPlayer = physical.transform.Find("Player");
        if (physicalPlayer == null)
        {
            throw new System.Exception("Could not find 'Player' in Physical body!");
        }
        Debug.Log($"  ✓ Found: {physicalPlayer.name}");
        
        Debug.Log("  Getting Physical Animator...");
        Animator physicalAnimator = physicalPlayer.GetComponent<Animator>();
        if (physicalAnimator == null)
        {
            throw new System.Exception("Physical/Player has no Animator component!");
        }
        Debug.Log($"  ✓ Animator found, Avatar: {(physicalAnimator.avatar != null ? physicalAnimator.avatar.name : "NULL")}");
        
        // Find Hips bone by name
        Debug.Log("  Finding Physical Hips bone by name...");
        Transform physicalHips = physicalPlayer.Find("Torso");
        if (physicalHips == null)
        {
            throw new System.Exception("Physical/Player has no 'Torso' child! Check FBX structure.");
        }
        Debug.Log($"  ✓ Physical Hips: {physicalHips.name}");
        
        Debug.Log("  Getting Physical Hips Rigidbody...");
        Rigidbody physicalTorso = physicalHips.GetComponent<Rigidbody>();
        if (physicalTorso == null)
        {
            throw new System.Exception("Physical Hips has no Rigidbody! Check AddPhysicsToSkeleton.");
        }
        Debug.Log($"  ✓ Physical Torso Rigidbody: mass={physicalTorso.mass}kg");
        
        // CRITICAL: Ensure ALL physics bones are on Layer 8
        SetLayerRecursive(physical.transform.Find("Player"), PHYSICS_LAYER);
        
        // CRITICAL: Add Rigidbody to ROOT (special settings - NO gravity!)
        Debug.Log("  Adding Rigidbody to root...");
        Rigidbody rootRb = root.AddComponent<Rigidbody>();
        rootRb.mass = 1;
        rootRb.linearDamping = 0; // No linear drag
        rootRb.angularDamping = 0.05f; // Match Default Character
        rootRb.useGravity = false; // CRITICAL: Root does NOT use gravity!
        rootRb.isKinematic = false;
        rootRb.interpolation = RigidbodyInterpolation.None;
        rootRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rootRb.constraints = RigidbodyConstraints.None;
        
        // CRITICAL: AudioListener on ROOT (not on camera!)
        Debug.Log("  Adding AudioListener to root...");
        root.AddComponent<AudioListener>();
        
        // Add ActiveRagdoll (REQUIRED - base component)
        Debug.Log("  Adding ActiveRagdoll...");
        var activeRagdoll = root.AddComponent<ActiveRagdoll.ActiveRagdoll>();
        // References will be auto-configured by ActiveRagdoll.OnValidate()
        
        // Add InputModule (REQUIRED) - ONLY ONCE!
        Debug.Log("  Adding InputModule...");
        if (root.GetComponent<ActiveRagdoll.InputModule>() == null)
        {
            root.AddComponent<ActiveRagdoll.InputModule>();
        }
        
        // Add Unity Input System (REQUIRED for controls)
        Debug.Log("  Adding PlayerInput...");
        var playerInput = root.AddComponent<PlayerInput>();
        var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/ActiveRagdollActions.inputactions");
        if (inputActions != null)
        {
            playerInput.actions = inputActions;
            playerInput.defaultActionMap = "Player";
            playerInput.neverAutoSwitchControlSchemes = false;
        }
        else
        {
            Debug.LogWarning("  ⚠ Input actions not found at Assets/Input/ActiveRagdollActions.inputactions");
        }
        
        // Add PhysicsModule (REQUIRED)
        Debug.Log("  Adding PhysicsModule...");
        root.AddComponent<ActiveRagdoll.PhysicsModule>();
        
        // Add CameraModule (REQUIRED)
        Debug.Log("  Adding CameraModule...");
        root.AddComponent<ActiveRagdoll.CameraModule>();
        
        // Add AnimationModule (REQUIRED)
        Debug.Log("  Adding AnimationModule...");
        root.AddComponent<ActiveRagdoll.AnimationModule>();
        
        // Add GripModule (REQUIRED)
        Debug.Log("  Adding GripModule...");
        root.AddComponent<ActiveRagdoll.GripModule>();
        
        // Add DefaultBehaviour (REQUIRED - main controller)
        Debug.Log("  Adding DefaultBehaviour...");
        root.AddComponent<DefaultBehaviour>();
        
        // Add TimeRewindController (for time rewind feature)
        Debug.Log("  Adding TimeRewindController...");
        var timeRewind = root.AddComponent<TimeRewindController>();
        // Set the target rigidbody
        timeRewind.GetType().GetField("targetRigidbody", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(timeRewind, physicalTorso);
        
        // Add player components
        Debug.Log("  Adding RespawnablePlayer...");
        root.AddComponent<RespawnablePlayer>();
        
        Debug.Log("  Adding RagdollPointsCollector...");
        root.AddComponent<RagdollPointsCollector>();
        
        // Add audio
        Debug.Log("  Adding audio components...");
        root.AddComponent<CharacterAudioController>();
        var audioSource = root.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound
        
        // Configure camera with post-processing effects
        Debug.Log("  Configuring camera effects...");
        ConfigureCameraEffects(cameraObj);
        
        Debug.Log("✓ All components configured!");
    }
    
    private void SetLayerRecursive(Transform obj, int layer)
    {
        if (obj == null) return;
        obj.gameObject.layer = layer;
        foreach (Transform child in obj)
        {
            SetLayerRecursive(child, layer);
        }
    }
    
    private void ConfigureCameraEffects(GameObject cameraObj)
    {
        Camera cam = cameraObj.GetComponent<Camera>();
        if (cam == null) return;
        
        // Try to add post-processing effects (optional - won't break if scripts don't exist)
        try
        {
            // Bloom
            var bloomType = System.Type.GetType("Kino.Bloom, Assembly-CSharp");
            if (bloomType != null)
            {
                var bloom = cameraObj.AddComponent(bloomType);
                bloomType.GetField("_threshold")?.SetValue(bloom, 1.16f);
                bloomType.GetField("_softKnee")?.SetValue(bloom, 0.5f);
                bloomType.GetField("_radius")?.SetValue(bloom, 3f);
                bloomType.GetField("_intensity")?.SetValue(bloom, 0.5f);
                Debug.Log("    ✓ Added Bloom");
            }
        }
        catch { }
        
        try
        {
            // Vignette
            var vignetteType = System.Type.GetType("Kino.Vignette, Assembly-CSharp");
            if (vignetteType != null)
            {
                var vignette = cameraObj.AddComponent(vignetteType);
                vignetteType.GetField("_falloff")?.SetValue(vignette, 0.314f);
                Debug.Log("    ✓ Added Vignette");
            }
        }
        catch { }
        
        // These are optional enhancements - character will work without them
        Debug.Log("  Camera effects configured (optional effects added if available)");
    }
    
    private void DrawRequirements()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("📋 Blender Export Requirements:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("✓ Humanoid armature (Torso, Chest, Neck, Head, Arms, Legs)");
        EditorGUILayout.LabelField("✓ Mesh with UV unwrapping");
        EditorGUILayout.LabelField("✓ Vertex groups match bone names");
        EditorGUILayout.LabelField("✓ Export: FBX with Armature + Mesh selected");
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("⚙️ Unity Import Settings (in Inspector):", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Rig Tab:");
        EditorGUILayout.LabelField("  • Animation Type = Humanoid");
        EditorGUILayout.LabelField("  • Avatar Definition = Create From This Model");
        EditorGUILayout.LabelField("  • Configure and map all bones");
        EditorGUILayout.LabelField("Model Tab:");
        EditorGUILayout.LabelField("  • Read/Write Enabled = ON (recommended)");
        EditorGUILayout.EndVertical();
    }
}
