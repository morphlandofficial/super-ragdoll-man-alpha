using UnityEngine;
using UnityEditor;

public class SkyboxExtractor : EditorWindow
{
    [MenuItem("Tools/Extract Skybox from GLB")]
    public static void ShowWindow()
    {
        GetWindow<SkyboxExtractor>("Skybox Extractor");
    }

    void OnGUI()
    {
        GUILayout.Label("Bikini Bottom Skybox Extractor", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Extract and Create Skybox Material"))
        {
            ExtractBikiniBottomSkybox();
        }
    }

    static void ExtractBikiniBottomSkybox()
    {
        // Find the skybox objects in the scene
        GameObject skyboxRoot = GameObject.Find("season_1_spongebob_squarepants_skybox");
        if (skyboxRoot == null)
        {
            // Debug.LogError("Skybox object not found in scene. Please add the GLB to the scene first.");
            return;
        }

        // Get the container object
        Transform container = skyboxRoot.transform.Find("Cylinder (1).obj.cleaner.materialmerger.gles");
        if (container == null)
        {
            // Debug.LogError("Container object not found.");
            return;
        }

        // Create skybox material
        Material skyboxMaterial = new Material(Shader.Find("Skybox/6 Sided"));
        skyboxMaterial.name = "Bikini Bottom Skybox";

        // Get textures from each object and assign to skybox faces
        // Note: You may need to adjust these mappings based on how the textures align
        AssignTextureToSkyboxFace(container, "Object_2", skyboxMaterial, "_FrontTex");  // Front
        AssignTextureToSkyboxFace(container, "Object_3", skyboxMaterial, "_BackTex");   // Back  
        AssignTextureToSkyboxFace(container, "Object_4", skyboxMaterial, "_LeftTex");   // Left
        AssignTextureToSkyboxFace(container, "Object_5", skyboxMaterial, "_RightTex");  // Right
        
        // For top and bottom, we might need to use one of the existing textures or create solid colors
        // You can adjust this based on your skybox structure
        AssignTextureToSkyboxFace(container, "Object_2", skyboxMaterial, "_UpTex");     // Top
        AssignTextureToSkyboxFace(container, "Object_2", skyboxMaterial, "_DownTex");   // Bottom

        // Save the material
        string path = "Assets/Skybox/bikini bottom/Bikini Bottom Skybox.mat";
        AssetDatabase.CreateAsset(skyboxMaterial, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Debug.Log($"Skybox material created at: {path}");
        // Debug.Log("You can now drag this material into your SkyboxRandomizer component!");
        
        // Select the created material in the project window
        Selection.activeObject = skyboxMaterial;
        EditorGUIUtility.PingObject(skyboxMaterial);
    }

    static void AssignTextureToSkyboxFace(Transform container, string objectName, Material skyboxMaterial, string propertyName)
    {
        Transform obj = container.Find(objectName);
        if (obj != null)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                // Get the main texture from the material
                Texture mainTexture = renderer.sharedMaterial.mainTexture;
                if (mainTexture != null)
                {
                    skyboxMaterial.SetTexture(propertyName, mainTexture);
                }
            }
        }
    }
}