using UnityEngine;
using UnityEditor;

/// <summary>
/// Debug tool to inspect FBX structure
/// </summary>
public class DebugFBXStructure : EditorWindow
{
    private GameObject fbxModel;
    
    [MenuItem("Tools/Debug FBX Structure")]
    static void ShowWindow()
    {
        GetWindow<DebugFBXStructure>("Debug FBX");
    }
    
    void OnGUI()
    {
        EditorGUILayout.LabelField("FBX Structure Inspector", EditorStyles.boldLabel);
        fbxModel = (GameObject)EditorGUILayout.ObjectField("FBX Model", fbxModel, typeof(GameObject), false);
        
        if (GUILayout.Button("Show Structure"))
        {
            if (fbxModel != null)
            {
                Debug.Log("=== FBX STRUCTURE ===");
                Debug.Log($"Root: {fbxModel.name}");
                PrintHierarchy(fbxModel.transform, 0);
                
                // Check for Animator
                Animator[] animators = fbxModel.GetComponentsInChildren<Animator>();
                Debug.Log($"\nAnimators found: {animators.Length}");
                foreach (var anim in animators)
                {
                    Debug.Log($"  Animator on: {GetPath(anim.transform, fbxModel.transform)}");
                }
                
                // Check for SkinnedMeshRenderer
                SkinnedMeshRenderer[] meshes = fbxModel.GetComponentsInChildren<SkinnedMeshRenderer>();
                Debug.Log($"\nSkinnedMeshRenderers found: {meshes.Length}");
                foreach (var mesh in meshes)
                {
                    Debug.Log($"  Mesh on: {GetPath(mesh.transform, fbxModel.transform)}");
                }
            }
        }
    }
    
    void PrintHierarchy(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}{t.name}");
        
        foreach (Transform child in t)
        {
            PrintHierarchy(child, depth + 1);
        }
    }
    
    string GetPath(Transform t, Transform root)
    {
        if (t == root) return root.name;
        return GetPath(t.parent, root) + "/" + t.name;
    }
}

