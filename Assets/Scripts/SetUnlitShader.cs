using UnityEngine;
using UnityEditor;

public class SetUnlitShader
{
    public static void Execute()
    {
        // Load the material
        Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Test Dummy Unlit.mat");
        
        if (material != null)
        {
            // Find and assign the Unlit/Texture shader
            Shader unlitShader = Shader.Find("Unlit/Texture");
            
            if (unlitShader != null)
            {
                material.shader = unlitShader;
                
                // Save the changes
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                
            }
            else
            {
                // Debug.LogError("Unlit/Texture shader not found");
            }
        }
        else
        {
            // Debug.LogError("Material not found at Assets/Materials/Test Dummy Unlit.mat");
        }
    }
}