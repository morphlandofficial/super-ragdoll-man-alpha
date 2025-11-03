using UnityEngine;

public class ScreenCameraProjector : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int renderTextureSize = 512;
    
    private RenderTexture renderTexture;
    private Material screenMaterial;
    private Camera targetCamera;
    private MeshRenderer meshRenderer;
    private Camera originalTargetTexture; // Store original state
    
    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
// Debug.LogError("ScreenCameraProjector needs a MeshRenderer!");
            enabled = false;
            return;
        }
        
        SetupProjection();
    }
    
    void Update()
    {
        // Check if we need to find a camera
        if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
        {
            FindActiveCamera();
        }
    }
    
    void SetupProjection()
    {
        // Create render texture
        renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16);
        renderTexture.name = "ScreenProjection_RT";
        
        // Create material
        screenMaterial = new Material(Shader.Find("Standard"));
        screenMaterial.mainTexture = renderTexture;
        screenMaterial.EnableKeyword("_EMISSION");
        screenMaterial.SetTexture("_EmissionMap", renderTexture);
        screenMaterial.SetColor("_EmissionColor", Color.white * 0.3f);
        
        // Apply to screen
        meshRenderer.material = screenMaterial;
        
        // Find camera
        FindActiveCamera();
    }
    
    void FindActiveCamera()
    {
        // Look for MainCamera first
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.gameObject.activeInHierarchy)
        {
            SetCamera(mainCam);
            return;
        }
        
        // Find any active camera
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera cam in cameras)
        {
            if (cam.gameObject.activeInHierarchy && cam.enabled)
            {
                SetCamera(cam);
                return;
            }
        }
    }
    
    void SetCamera(Camera cam)
    {
        if (targetCamera == cam) return;
        
        // Store original state of new camera
        originalTargetTexture = cam;
        
        targetCamera = cam;
        targetCamera.targetTexture = renderTexture;
        
    }
    
    void OnDestroy()
    {
        // Restore camera's original state
        if (targetCamera != null)
        {
            targetCamera.targetTexture = null;
        }
        
        // Clean up
        if (renderTexture != null)
        {
            renderTexture.Release();
            DestroyImmediate(renderTexture);
        }
        
        if (screenMaterial != null)
        {
            DestroyImmediate(screenMaterial);
        }
    }
}