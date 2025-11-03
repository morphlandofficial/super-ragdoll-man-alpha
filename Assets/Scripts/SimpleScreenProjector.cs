using UnityEngine;

public class SimpleScreenProjector : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int renderTextureSize = 512;
    
    private RenderTexture renderTexture;
    private Material screenMaterial;
    private Camera duplicateCamera;
    private Camera mainCamera;
    private MeshRenderer meshRenderer;
    
    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
// Debug.LogError("SimpleScreenProjector needs a MeshRenderer!");
            enabled = false;
            return;
        }
        
        SetupScreen();
    }
    
    void Update()
    {
        // Check if we need to find the main camera
        if (mainCamera == null || !mainCamera.gameObject.activeInHierarchy)
        {
            FindMainCamera();
        }
        
        // Update duplicate camera to match main camera
        if (mainCamera != null && duplicateCamera != null)
        {
            SyncCameras();
        }
    }
    
    void SetupScreen()
    {
        // Create render texture
        renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16);
        renderTexture.name = "TV_Screen_RT";
        
        // Create material for screen
        screenMaterial = new Material(Shader.Find("Standard"));
        screenMaterial.mainTexture = renderTexture;
        screenMaterial.EnableKeyword("_EMISSION");
        screenMaterial.SetTexture("_EmissionMap", renderTexture);
        screenMaterial.SetColor("_EmissionColor", Color.white * 0.4f);
        
        // Apply to screen
        meshRenderer.material = screenMaterial;
        
        FindMainCamera();
    }
    
    void FindMainCamera()
    {
        // Find main camera
        Camera newMainCamera = Camera.main;
        if (newMainCamera == null)
        {
            // Find any active camera
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera cam in cameras)
            {
                if (cam.gameObject.activeInHierarchy && cam.enabled)
                {
                    newMainCamera = cam;
                    break;
                }
            }
        }
        
        if (newMainCamera != null && newMainCamera != mainCamera)
        {
            mainCamera = newMainCamera;
            CreateDuplicateCamera();
        }
    }
    
    void CreateDuplicateCamera()
    {
        // Clean up old duplicate camera
        if (duplicateCamera != null)
        {
            if (duplicateCamera.targetTexture != null)
            {
                duplicateCamera.targetTexture = null;
            }
            
            if (Application.isPlaying)
            {
                Destroy(duplicateCamera.gameObject);
            }
            else
            {
                DestroyImmediate(duplicateCamera.gameObject);
            }
            duplicateCamera = null;
        }
        
        // Create new duplicate camera
        GameObject duplicateCamObj = new GameObject("TV_DuplicateCamera");
        duplicateCamObj.transform.SetParent(mainCamera.transform);
        duplicateCamObj.transform.localPosition = Vector3.zero;
        duplicateCamObj.transform.localRotation = Quaternion.identity;
        
        duplicateCamera = duplicateCamObj.AddComponent<Camera>();
        duplicateCamera.targetTexture = renderTexture;
        duplicateCamera.enabled = true;
        
        // Copy main camera settings
        SyncCameras();
    }
    
    void SyncCameras()
    {
        if (mainCamera == null || duplicateCamera == null) return;
        
        duplicateCamera.fieldOfView = mainCamera.fieldOfView;
        duplicateCamera.nearClipPlane = mainCamera.nearClipPlane;
        duplicateCamera.farClipPlane = mainCamera.farClipPlane;
        duplicateCamera.cullingMask = mainCamera.cullingMask;
        duplicateCamera.clearFlags = mainCamera.clearFlags;
        duplicateCamera.backgroundColor = mainCamera.backgroundColor;
    }
    
    void OnDisable()
    {
        // Clean up when disabled (costume deactivated)
        CleanupResources();
    }
    
    void OnDestroy()
    {
        // Clean up when destroyed
        CleanupResources();
    }
    
    private void CleanupResources()
    {
        // Clean up duplicate camera
        if (duplicateCamera != null)
        {
            // Check if it's still assigned as targetTexture before destroying
            if (duplicateCamera.targetTexture == renderTexture)
            {
                duplicateCamera.targetTexture = null;
            }
            
            if (Application.isPlaying)
            {
                Destroy(duplicateCamera.gameObject);
            }
            else
            {
                DestroyImmediate(duplicateCamera.gameObject);
            }
            duplicateCamera = null;
        }
        
        // Release and destroy render texture
        if (renderTexture != null)
        {
            renderTexture.Release();
            
            if (Application.isPlaying)
            {
                Destroy(renderTexture);
            }
            else
            {
                DestroyImmediate(renderTexture);
            }
            renderTexture = null;
        }
        
        // Destroy material
        if (screenMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(screenMaterial);
            }
            else
            {
                DestroyImmediate(screenMaterial);
            }
            screenMaterial = null;
        }
    }
}