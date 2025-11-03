using UnityEngine;

public class CameraProjectionScreen : MonoBehaviour
{
    [Header("Render Texture Settings")]
    [SerializeField] private int textureWidth = 512;
    [SerializeField] private int textureHeight = 512;
    [SerializeField] private int textureDepth = 16;
    
    [Header("Material Settings")]
    [SerializeField] private bool makeEmissive = true;
    [SerializeField] private float emissionIntensity = 0.5f;
    
    private RenderTexture renderTexture;
    private Material screenMaterial;
    private Camera activeCamera;
    private MeshRenderer meshRenderer;
    
    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
// Debug.LogError("CameraProjectionScreen requires a MeshRenderer component!");
            return;
        }
        
        SetupRenderTexture();
        SetupMaterial();
        FindAndSetupCamera();
    }
    
    void Update()
    {
        // Check if we need to find a new active camera
        if (activeCamera == null || !activeCamera.gameObject.activeInHierarchy)
        {
            FindAndSetupCamera();
        }
    }
    
    void SetupRenderTexture()
    {
        renderTexture = new RenderTexture(textureWidth, textureHeight, textureDepth);
        renderTexture.name = gameObject.name + "_RenderTexture";
    }
    
    void SetupMaterial()
    {
        // Create a new material instance
        screenMaterial = new Material(Shader.Find("Standard"));
        screenMaterial.name = gameObject.name + "_ScreenMaterial";
        
        // Set the render texture as the main texture
        screenMaterial.mainTexture = renderTexture;
        
        if (makeEmissive)
        {
            // Make it emissive so it glows like a screen
            screenMaterial.EnableKeyword("_EMISSION");
            screenMaterial.SetTexture("_EmissionMap", renderTexture);
            screenMaterial.SetColor("_EmissionColor", Color.white * emissionIntensity);
        }
        
        // Apply the material to the mesh renderer
        meshRenderer.material = screenMaterial;
    }
    
    void FindAndSetupCamera()
    {
        // First try to find a camera tagged as MainCamera
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.gameObject.activeInHierarchy)
        {
            SetupCameraProjection(mainCamera);
            return;
        }
        
        // If no main camera, find any active camera in the scene
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera cam in cameras)
        {
            if (cam.gameObject.activeInHierarchy && cam.enabled)
            {
                SetupCameraProjection(cam);
                return;
            }
        }
        
// Debug.LogWarning("CameraProjectionScreen: No active camera found in scene!");
    }
    
    void SetupCameraProjection(Camera camera)
    {
        // If we had a previous camera, remove the render texture from it
        if (activeCamera != null && activeCamera != camera)
        {
            activeCamera.targetTexture = null;
        }
        
        activeCamera = camera;
        activeCamera.targetTexture = renderTexture;
        
    }
    
    void OnDestroy()
    {
        // Clean up: remove render texture from camera
        if (activeCamera != null)
        {
            activeCamera.targetTexture = null;
        }
        
        // Clean up render texture
        if (renderTexture != null)
        {
            renderTexture.Release();
            DestroyImmediate(renderTexture);
        }
        
        // Clean up material
        if (screenMaterial != null)
        {
            DestroyImmediate(screenMaterial);
        }
    }
    
    // Public method to manually set a specific camera
    public void SetCamera(Camera camera)
    {
        if (camera != null)
        {
            SetupCameraProjection(camera);
        }
    }
    
    // Public method to refresh the camera search
    public void RefreshCamera()
    {
        FindAndSetupCamera();
    }
}