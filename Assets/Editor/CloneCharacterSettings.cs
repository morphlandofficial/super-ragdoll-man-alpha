using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Clones ALL component settings from Default Character to a new character prefab.
/// Ensures hierarchy matches and copies every single property.
/// </summary>
public class CloneCharacterSettings : EditorWindow
{
    private GameObject sourceCharacter; // Default Character
    private GameObject targetCharacter; // Your new character
    
    [MenuItem("Tools/Clone Character Settings")]
    static void ShowWindow()
    {
        GetWindow<CloneCharacterSettings>("Clone Character Settings");
    }
    
    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("CLONE SETTINGS FROM DEFAULT CHARACTER", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This will copy EVERY component and setting from Default Character to your new character.", MessageType.Info);
        
        EditorGUILayout.Space();
        sourceCharacter = (GameObject)EditorGUILayout.ObjectField("Source (Default Character)", sourceCharacter, typeof(GameObject), true);
        targetCharacter = (GameObject)EditorGUILayout.ObjectField("Target (New Character)", targetCharacter, typeof(GameObject), true);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("🔍 VALIDATE HIERARCHIES", GUILayout.Height(40)))
        {
            ValidateHierarchies();
        }
        
        EditorGUILayout.Space();
        
        GUI.enabled = sourceCharacter != null && targetCharacter != null;
        if (GUILayout.Button("✨ CLONE ALL SETTINGS ✨", GUILayout.Height(60)))
        {
            CloneAllSettings();
        }
        GUI.enabled = true;
    }
    
    private void ValidateHierarchies()
    {
        if (sourceCharacter == null || targetCharacter == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign both source and target characters!", "OK");
            return;
        }
        
        Debug.Log("=== VALIDATING HIERARCHIES ===");
        
        // Get all child transforms
        Transform[] sourceChildren = sourceCharacter.GetComponentsInChildren<Transform>(true);
        Transform[] targetChildren = targetCharacter.GetComponentsInChildren<Transform>(true);
        
        Debug.Log($"Source has {sourceChildren.Length} transforms");
        Debug.Log($"Target has {targetChildren.Length} transforms");
        
        // Build name maps
        Dictionary<string, Transform> sourceMap = new Dictionary<string, Transform>();
        Dictionary<string, Transform> targetMap = new Dictionary<string, Transform>();
        
        foreach (var t in sourceChildren)
        {
            string path = GetTransformPath(t, sourceCharacter.transform);
            sourceMap[path] = t;
        }
        
        foreach (var t in targetChildren)
        {
            string path = GetTransformPath(t, targetCharacter.transform);
            targetMap[path] = t;
        }
        
        // Check for missing transforms
        List<string> missingInTarget = new List<string>();
        List<string> extraInTarget = new List<string>();
        
        foreach (var path in sourceMap.Keys)
        {
            if (!targetMap.ContainsKey(path))
                missingInTarget.Add(path);
        }
        
        foreach (var path in targetMap.Keys)
        {
            if (!sourceMap.ContainsKey(path))
                extraInTarget.Add(path);
        }
        
        if (missingInTarget.Count > 0)
        {
            Debug.LogWarning($"Missing in target: {string.Join(", ", missingInTarget)}");
        }
        
        if (extraInTarget.Count > 0)
        {
            Debug.Log($"Extra in target (OK): {string.Join(", ", extraInTarget)}");
        }
        
        if (missingInTarget.Count == 0)
        {
            Debug.Log("✓ Hierarchies are compatible!");
            EditorUtility.DisplayDialog("Success", "Hierarchies match! Ready to clone settings.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Warning", $"Some transforms are missing in target. See console for details.", "OK");
        }
    }
    
    private void CloneAllSettings()
    {
        if (sourceCharacter == null || targetCharacter == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign both characters!", "OK");
            return;
        }
        
        if (!EditorUtility.DisplayDialog("Confirm", 
            "This will OVERWRITE all components on the target character.\n\nAre you sure?", 
            "Yes, Clone Everything", "Cancel"))
        {
            return;
        }
        
        Debug.Log("=== CLONING ALL SETTINGS ===");
        
        // Build transform maps
        Transform[] sourceChildren = sourceCharacter.GetComponentsInChildren<Transform>(true);
        Transform[] targetChildren = targetCharacter.GetComponentsInChildren<Transform>(true);
        
        Dictionary<string, Transform> sourceMap = new Dictionary<string, Transform>();
        Dictionary<string, Transform> targetMap = new Dictionary<string, Transform>();
        
        foreach (var t in sourceChildren)
        {
            string path = GetTransformPath(t, sourceCharacter.transform);
            sourceMap[path] = t;
        }
        
        foreach (var t in targetChildren)
        {
            string path = GetTransformPath(t, targetCharacter.transform);
            targetMap[path] = t;
        }
        
        int componentsCloned = 0;
        
        // Clone ROOT components first
        componentsCloned += CloneComponentsFromTo(sourceCharacter, targetCharacter, sourceMap, targetMap);
        
        // Clone all matching child components
        foreach (var kvp in sourceMap)
        {
            string path = kvp.Key;
            Transform sourceTransform = kvp.Value;
            
            if (targetMap.ContainsKey(path))
            {
                Transform targetTransform = targetMap[path];
                componentsCloned += CloneComponentsFromTo(sourceTransform.gameObject, targetTransform.gameObject, sourceMap, targetMap);
            }
        }
        
        Debug.Log($"✓ Cloned {componentsCloned} components!");
        EditorUtility.DisplayDialog("Success", $"Cloned {componentsCloned} components from Default Character!", "OK");
        
        // Mark dirty
        EditorUtility.SetDirty(targetCharacter);
    }
    
    private int CloneComponentsFromTo(GameObject source, GameObject target, 
                                      Dictionary<string, Transform> sourceMap, 
                                      Dictionary<string, Transform> targetMap)
    {
        int count = 0;
        
        Component[] sourceComponents = source.GetComponents<Component>();
        
        foreach (Component sourceComp in sourceComponents)
        {
            if (sourceComp == null) continue;
            
            // Skip Transform (structure only)
            if (sourceComp is Transform) continue;
            
            System.Type componentType = sourceComp.GetType();
            
            // Find or add component on target
            Component targetComp = target.GetComponent(componentType);
            
            if (targetComp == null)
            {
                Debug.Log($"  Adding {componentType.Name} to {target.name}");
                targetComp = target.AddComponent(componentType);
            }
            
            // Copy all fields
            if (CopyComponentSettings(sourceComp, targetComp, source, target, sourceMap, targetMap))
            {
                count++;
                Debug.Log($"  ✓ Cloned {componentType.Name} on {target.name}");
            }
        }
        
        return count;
    }
    
    private bool CopyComponentSettings(Component source, Component target, 
                                       GameObject sourceGO, GameObject targetGO,
                                       Dictionary<string, Transform> sourceMap, 
                                       Dictionary<string, Transform> targetMap)
    {
        if (source == null || target == null) return false;
        
        // Use SerializedObject to copy all properties
        SerializedObject sourceObj = new SerializedObject(source);
        SerializedObject targetObj = new SerializedObject(target);
        
        SerializedProperty prop = sourceObj.GetIterator();
        bool enterChildren = true;
        
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            
            // Skip script reference
            if (prop.name == "m_Script") continue;
            
            SerializedProperty targetProp = targetObj.FindProperty(prop.name);
            if (targetProp != null && targetProp.propertyType == prop.propertyType)
            {
                // Handle object references specially (need to remap)
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Object refObj = prop.objectReferenceValue;
                    
                    if (refObj != null)
                    {
                        // Try to remap references from source hierarchy to target hierarchy
                        Object remappedObj = RemapReference(refObj, sourceGO, targetGO, sourceMap, targetMap);
                        targetProp.objectReferenceValue = remappedObj;
                    }
                    else
                    {
                        targetProp.objectReferenceValue = null;
                    }
                }
                else
                {
                    // Copy value directly
                    targetProp.boxedValue = prop.boxedValue;
                }
            }
        }
        
        targetObj.ApplyModifiedProperties();
        return true;
    }
    
    private Object RemapReference(Object refObj, GameObject sourceRoot, GameObject targetRoot,
                                  Dictionary<string, Transform> sourceMap, 
                                  Dictionary<string, Transform> targetMap)
    {
        if (refObj == null) return null;
        
        // If it's a GameObject
        if (refObj is GameObject go)
        {
            string path = GetTransformPath(go.transform, sourceRoot.transform);
            if (targetMap.ContainsKey(path))
            {
                return targetMap[path].gameObject;
            }
        }
        
        // If it's a Transform
        if (refObj is Transform trans)
        {
            string path = GetTransformPath(trans, sourceRoot.transform);
            if (targetMap.ContainsKey(path))
            {
                return targetMap[path];
            }
        }
        
        // If it's a Component on a GameObject in the hierarchy
        if (refObj is Component comp)
        {
            GameObject compGO = comp.gameObject;
            string path = GetTransformPath(compGO.transform, sourceRoot.transform);
            
            if (targetMap.ContainsKey(path))
            {
                // Find same component type on target
                Transform targetTransform = targetMap[path];
                Component targetComp = targetTransform.GetComponent(comp.GetType());
                if (targetComp != null)
                {
                    return targetComp;
                }
            }
        }
        
        // If it's an asset (material, mesh, etc.), keep the reference as-is
        if (AssetDatabase.Contains(refObj))
        {
            return refObj;
        }
        
        // Default: keep original reference
        return refObj;
    }
    
    private string GetTransformPath(Transform transform, Transform root)
    {
        if (transform == root) return "";
        
        List<string> path = new List<string>();
        Transform current = transform;
        
        while (current != root && current != null)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }
        
        return string.Join("/", path);
    }
}

