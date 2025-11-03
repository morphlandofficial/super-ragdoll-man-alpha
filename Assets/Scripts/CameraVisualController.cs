using UnityEngine;
using Assets.Pixelation.Scripts;

/// <summary>
/// Controls visual effects on the Active Ragdoll Camera.
/// Attach this to your Level Systems prefab to customize camera visuals per level.
/// </summary>
public class CameraVisualController : MonoBehaviour
{
    [Header("Character Prefab Reference")]
    [Tooltip("Drag the Default Character prefab here")]
    public GameObject characterPrefab;
    
    [Header("Camera Settings")]
    public CameraSettings cameraSettings = new CameraSettings();
    
    [Header("Visual Effects")]
    public BloomSettings bloomSettings = new BloomSettings();
    public VignetteSettings vignetteSettings = new VignetteSettings();
    public DatamoshSettings datamoshSettings = new DatamoshSettings();
    public TubeSettings tubeSettings = new TubeSettings();
    public PixelationSettings pixelationSettings = new PixelationSettings();
    public BinarySettings binarySettings = new BinarySettings();
    public AnalogGlitchSettings analogGlitchSettings = new AnalogGlitchSettings();
    public BokehSettings bokehSettings = new BokehSettings();
    public ChunkySettings chunkySettings = new ChunkySettings();
    public VHSSettings vhsSettings = new VHSSettings();
    
    private Camera _activeRagdollCamera;
    private int _lastCameraInstanceID = -1;
    
    // MULTIPLAYER: Track which cameras have pixelation explicitly disabled (static so DisablePixelationOnCamera can set it)
    private static System.Collections.Generic.HashSet<int> _camerasWithPixelationDisabled = new System.Collections.Generic.HashSet<int>();
    
    // Static instance reference so RespawnablePlayer can check pixelation settings
    private static CameraVisualController _instance;
    
    // MULTIPLAYER: Track all cameras we've initialized (prevents re-applying settings every frame)
    private System.Collections.Generic.HashSet<int> _initializedCameraIDs = new System.Collections.Generic.HashSet<int>();
    
    private void Awake()
    {
        // Set static instance reference
        _instance = this;
    }
    
    private void Start()
    {
        // Start continuous monitoring for camera changes
        StartCoroutine(MonitorAndApplySettings());
    }
    
    private void OnDestroy()
    {
        // Clear static reference when destroyed
        if (_instance == this)
        {
            _instance = null;
        }
    }
    
    /// <summary>
    /// Check if pixelation should be enabled for the current level (from settings)
    /// </summary>
    public static bool ShouldPixelationBeEnabled()
    {
        if (_instance != null)
        {
            return _instance.pixelationSettings.enabled;
        }
        // If no CameraVisualController exists, default to no pixelation
        return false;
    }
    
    private System.Collections.IEnumerator MonitorAndApplySettings()
    {
        // Wait a frame to ensure the character has spawned
        yield return new WaitForEndOfFrame();
        
        while (true)
        {
            // MULTIPLAYER: Find ALL Active Ragdoll Cameras in the scene
            Camera[] allCameras = FindAllActiveRagdollCameras();
            
            if (allCameras.Length > 0)
            {
                bool isMultiplayer = (UnityEngine.InputSystem.Gamepad.all.Count >= 2);
                
                // Apply settings to each camera
                foreach (Camera cam in allCameras)
                {
                    if (cam == null) continue;
                    
                    int cameraInstanceID = cam.GetInstanceID();
                    
                    // Check if this is a new camera we haven't initialized yet
                    if (!_initializedCameraIDs.Contains(cameraInstanceID))
                    {
                        _initializedCameraIDs.Add(cameraInstanceID);
                        
                        // MULTIPLAYER: If in multiplayer mode, disable pixelation BEFORE applying settings
                        if (isMultiplayer)
                        {
                            Debug.Log($"<color=magenta>[CameraVisualController]</color> NEW CAMERA DETECTED in MULTIPLAYER - {cam.name} (Instance ID: {cameraInstanceID}) - disabling pixelation first");
                            DisablePixelationOnCamera(cam);
                        }
                        else
                        {
                            Debug.Log($"<color=magenta>[CameraVisualController]</color> NEW CAMERA DETECTED - Applying all settings to {cam.name} (Instance ID: {cameraInstanceID})");
                        }
                        
                        // Set as active camera and apply settings
                        _activeRagdollCamera = cam;
                        _lastCameraInstanceID = cameraInstanceID;
                        ApplyAllSettings();
                    }
                }
                
                // Clean up _initializedCameraIDs: remove IDs for cameras that no longer exist
                _initializedCameraIDs.RemoveWhere(id => 
                {
                    bool exists = false;
                    foreach (Camera cam in allCameras)
                    {
                        if (cam != null && cam.GetInstanceID() == id)
                        {
                            exists = true;
                            break;
                        }
                    }
                    return !exists; // Remove if doesn't exist
                });
            }
            
            // Check every half second
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    private Camera FindActiveRagdollCamera()
    {
        // Find all cameras in the scene
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        
        foreach (Camera cam in cameras)
        {
            if (cam.name == "Active Ragdoll Camera")
            {
                return cam;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// MULTIPLAYER: Find ALL Active Ragdoll Cameras in the scene (one per player)
    /// </summary>
    private Camera[] FindAllActiveRagdollCameras()
    {
        // Find all cameras in the scene
        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        System.Collections.Generic.List<Camera> ragdollCameras = new System.Collections.Generic.List<Camera>();
        
        foreach (Camera cam in allCameras)
        {
            if (cam.name == "Active Ragdoll Camera")
            {
                ragdollCameras.Add(cam);
            }
        }
        
        return ragdollCameras.ToArray();
    }
    
    private void ApplyAllSettings()
    {
        if (_activeRagdollCamera == null) return;
        
        // Apply Camera Settings
        ApplyCameraSettings();
        
        // Apply Visual Effects
        ApplyBloomSettings();
        ApplyVignetteSettings();
        ApplyDatamoshSettings();
        ApplyTubeSettings();
        ApplyPixelationSettings();
        ApplyBinarySettings();
        ApplyAnalogGlitchSettings();
        ApplyBokehSettings();
        ApplyChunkySettings();
        ApplyVHSSettings();
    }
    
    private void ApplyCameraSettings()
    {
        if (!cameraSettings.overrideSettings) return;
        
        _activeRagdollCamera.fieldOfView = cameraSettings.fieldOfView;
        _activeRagdollCamera.nearClipPlane = cameraSettings.nearClipPlane;
        _activeRagdollCamera.farClipPlane = cameraSettings.farClipPlane;
        _activeRagdollCamera.allowHDR = cameraSettings.allowHDR;
        _activeRagdollCamera.allowMSAA = cameraSettings.allowMSAA;
    }
    
    private void ApplyBloomSettings()
    {
        var bloom = _activeRagdollCamera.GetComponent<Kino.Bloom>();
        if (bloom == null) return;
        
        bloom.enabled = bloomSettings.enabled;
        if (bloomSettings.enabled)
        {
            bloom.thresholdGamma = bloomSettings.threshold;
            bloom.softKnee = bloomSettings.softKnee;
            bloom.radius = bloomSettings.radius;
            bloom.intensity = bloomSettings.intensity;
            bloom.highQuality = bloomSettings.highQuality;
            bloom.antiFlicker = bloomSettings.antiFlicker;
        }
    }
    
    private void ApplyVignetteSettings()
    {
        var vignette = _activeRagdollCamera.GetComponent<Kino.Vignette>();
        if (vignette == null) return;
        
        vignette.enabled = vignetteSettings.enabled;
        if (vignetteSettings.enabled)
        {
            vignette.intensity = vignetteSettings.intensity;
        }
    }
    
    private void ApplyDatamoshSettings()
    {
        var datamosh = _activeRagdollCamera.GetComponent<Kino.Datamosh>();
        if (datamosh == null) return;
        
        datamosh.enabled = datamoshSettings.enabled;
        if (datamoshSettings.enabled)
        {
            datamosh.blockSize = datamoshSettings.blockSize;
            datamosh.entropy = datamoshSettings.entropy;
            datamosh.noiseContrast = datamoshSettings.noiseContrast;
            datamosh.velocityScale = datamoshSettings.velocityScale;
            datamosh.diffusion = datamoshSettings.diffusion;
            datamosh.alwaysGlitch = datamoshSettings.alwaysGlitch;
        }
    }
    
    private void ApplyTubeSettings()
    {
        var tube = _activeRagdollCamera.GetComponent<Kino.Tube>();
        if (tube == null) return;
        
        tube.enabled = tubeSettings.enabled;
        // Tube doesn't expose public properties, it uses serialized fields
        // We'll need to use reflection or accept it as-is
    }
    
    private void ApplyPixelationSettings()
    {
        var pixelation = _activeRagdollCamera.GetComponent<Pixelationv2>();
        if (pixelation == null) return;
        
        // MULTIPLAYER: Don't re-enable pixelation if it was explicitly disabled for multiplayer
        int cameraID = _activeRagdollCamera.GetInstanceID();
        if (_camerasWithPixelationDisabled.Contains(cameraID))
        {
            Debug.Log($"<color=magenta>[CameraVisualController]</color> Skipping pixelation settings - Camera {cameraID} explicitly disabled for multiplayer");
            return;
        }
        
        bool wasEnabled = pixelation.enabled;
        float oldBlockCount = pixelation.BlockCount;
        
        pixelation.enabled = pixelationSettings.enabled;
        if (pixelationSettings.enabled)
        {
            pixelation.BlockCount = pixelationSettings.blockCount;
        }
        
        Debug.Log($"<color=magenta>[CameraVisualController]</color> ApplyPixelationSettings - Camera: {_activeRagdollCamera.name}, Settings.enabled: {pixelationSettings.enabled}, WasEnabled: {wasEnabled}, BlockCount: {oldBlockCount} → {pixelation.BlockCount}");
    }
    
    /// <summary>
    /// MULTIPLAYER: Disable pixelation effect on a specific camera.
    /// Called by MultiplayerManagerSimple to make UI text readable in split-screen.
    /// </summary>
    /// <param name="camera">The camera to disable pixelation on</param>
    public static void DisablePixelationOnCamera(Camera camera)
    {
        if (camera == null) return;
        
        var pixelation = camera.GetComponent<Pixelationv2>();
        if (pixelation != null)
        {
            // Track this camera REGARDLESS of current state
            int cameraID = camera.GetInstanceID();
            _camerasWithPixelationDisabled.Add(cameraID);
            
            if (pixelation.enabled)
            {
                pixelation.enabled = false;
                Debug.Log($"<color=yellow>[CameraVisualController]</color> Disabled pixelation on {camera.name} (ID: {cameraID}) for multiplayer - tracked for no re-enable");
            }
            else
            {
                Debug.Log($"<color=yellow>[CameraVisualController]</color> Pixelation already disabled on {camera.name} (ID: {cameraID}) - tracked for no re-enable");
            }
        }
    }
    
    private void ApplyBinarySettings()
    {
        var binary = _activeRagdollCamera.GetComponent<Kino.Binary>();
        if (binary == null) return;
        
        binary.enabled = binarySettings.enabled;
        if (binarySettings.enabled)
        {
            binary.ditherType = binarySettings.ditherType;
            binary.ditherScale = binarySettings.ditherScale;
            binary.color0 = binarySettings.color0;
            binary.color1 = binarySettings.color1;
            binary.Opacity = binarySettings.opacity;
        }
    }
    
    private void ApplyAnalogGlitchSettings()
    {
        var analogGlitch = _activeRagdollCamera.GetComponent<Kino.AnalogGlitch>();
        if (analogGlitch == null) return;
        
        analogGlitch.enabled = analogGlitchSettings.enabled;
        if (analogGlitchSettings.enabled)
        {
            analogGlitch.scanLineJitter = analogGlitchSettings.scanLineJitter;
            analogGlitch.verticalJump = analogGlitchSettings.verticalJump;
            analogGlitch.horizontalShake = analogGlitchSettings.horizontalShake;
            analogGlitch.colorDrift = analogGlitchSettings.colorDrift;
        }
    }
    
    private void ApplyBokehSettings()
    {
        var bokeh = _activeRagdollCamera.GetComponent<Kino.Bokeh>();
        if (bokeh == null) return;
        
        bokeh.enabled = bokehSettings.enabled;
        if (bokehSettings.enabled)
        {
            bokeh.focusDistance = bokehSettings.focusDistance;
            bokeh.fNumber = bokehSettings.fNumber;
            bokeh.useCameraFov = bokehSettings.useCameraFov;
            bokeh.focalLength = bokehSettings.focalLength;
            bokeh.kernelSize = bokehSettings.kernelSize;
        }
    }
    
    private void ApplyChunkySettings()
    {
        var chunky = _activeRagdollCamera.GetComponent<Chunkyv2>();
        if (chunky == null) return;
        
        chunky.enabled = chunkySettings.enabled;
        if (chunkySettings.enabled)
        {
            chunky.SprTex = chunkySettings.spriteTexture;
            chunky.Color = chunkySettings.color;
        }
    }
    
    private void ApplyVHSSettings()
    {
        var vhs = _activeRagdollCamera.GetComponent<VHSPostProcessEffect>();
        if (vhs == null) return;
        
        vhs.enabled = vhsSettings.enabled;
        if (vhsSettings.enabled)
        {
            vhs.shader = vhsSettings.shader;
            vhs.VHSClip = vhsSettings.vhsClip;
            vhs.vhsIntensity = vhsSettings.vhsIntensity;
            vhs.distortionAmount = vhsSettings.distortionAmount;
            vhs.scanlineIntensity = vhsSettings.scanlineIntensity;
            vhs.chromaticAberration = vhsSettings.chromaticAberration;
            vhs.noiseAmount = vhsSettings.noiseAmount;
            vhs.colorBleed = vhsSettings.colorBleed;
            vhs.brightness = vhsSettings.brightness;
            vhs.contrast = vhsSettings.contrast;
            vhs.saturation = vhsSettings.saturation;
            vhs.verticalGlitchSpeed = vhsSettings.verticalGlitchSpeed;
            vhs.horizontalGlitchSpeed = vhsSettings.horizontalGlitchSpeed;
        }
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// Copies settings from the character prefab's camera (Editor only)
    /// </summary>
    public void CopyFromPrefab()
    {
        if (characterPrefab == null)
        {
            Debug.LogWarning("No character prefab assigned!");
            return;
        }
        
        // Find the Active Ragdoll Camera in the prefab
        Transform cameraTransform = characterPrefab.transform.Find("Active Ragdoll Camera");
        if (cameraTransform == null)
        {
            Debug.LogWarning("Could not find 'Active Ragdoll Camera' in prefab!");
            return;
        }
        
        Camera prefabCamera = cameraTransform.GetComponent<Camera>();
        if (prefabCamera == null)
        {
            Debug.LogWarning("Camera component not found!");
            return;
        }
        
        // Copy Camera Settings
        cameraSettings.fieldOfView = prefabCamera.fieldOfView;
        cameraSettings.nearClipPlane = prefabCamera.nearClipPlane;
        cameraSettings.farClipPlane = prefabCamera.farClipPlane;
        cameraSettings.allowHDR = prefabCamera.allowHDR;
        cameraSettings.allowMSAA = prefabCamera.allowMSAA;
        
        // Copy Bloom
        var bloom = prefabCamera.GetComponent<Kino.Bloom>();
        if (bloom != null)
        {
            bloomSettings.enabled = bloom.enabled;
            bloomSettings.threshold = bloom.thresholdGamma;
            bloomSettings.softKnee = bloom.softKnee;
            bloomSettings.radius = bloom.radius;
            bloomSettings.intensity = bloom.intensity;
            bloomSettings.highQuality = bloom.highQuality;
            bloomSettings.antiFlicker = bloom.antiFlicker;
        }
        
        // Copy Vignette
        var vignette = prefabCamera.GetComponent<Kino.Vignette>();
        if (vignette != null)
        {
            vignetteSettings.enabled = vignette.enabled;
            vignetteSettings.intensity = vignette.intensity;
        }
        
        // Copy Datamosh
        var datamosh = prefabCamera.GetComponent<Kino.Datamosh>();
        if (datamosh != null)
        {
            datamoshSettings.enabled = datamosh.enabled;
            datamoshSettings.blockSize = datamosh.blockSize;
            datamoshSettings.entropy = datamosh.entropy;
            datamoshSettings.noiseContrast = datamosh.noiseContrast;
            datamosh.velocityScale = datamoshSettings.velocityScale;
            datamoshSettings.diffusion = datamosh.diffusion;
            datamoshSettings.alwaysGlitch = datamosh.alwaysGlitch;
        }
        
        // Copy Tube
        var tube = prefabCamera.GetComponent<Kino.Tube>();
        if (tube != null)
        {
            tubeSettings.enabled = tube.enabled;
        }
        
        // Copy Pixelation
        var pixelation = prefabCamera.GetComponent<Pixelationv2>();
        if (pixelation != null)
        {
            pixelationSettings.enabled = pixelation.enabled;
            pixelationSettings.blockCount = pixelation.BlockCount;
        }
        
        // Copy Binary
        var binary = prefabCamera.GetComponent<Kino.Binary>();
        if (binary != null)
        {
            binarySettings.enabled = binary.enabled;
            binarySettings.ditherType = binary.ditherType;
            binarySettings.ditherScale = binary.ditherScale;
            binarySettings.color0 = binary.color0;
            binarySettings.color1 = binary.color1;
            binarySettings.opacity = binary.Opacity;
        }
        
        // Copy AnalogGlitch
        var analogGlitch = prefabCamera.GetComponent<Kino.AnalogGlitch>();
        if (analogGlitch != null)
        {
            analogGlitchSettings.enabled = analogGlitch.enabled;
            analogGlitchSettings.scanLineJitter = analogGlitch.scanLineJitter;
            analogGlitchSettings.verticalJump = analogGlitch.verticalJump;
            analogGlitchSettings.horizontalShake = analogGlitch.horizontalShake;
            analogGlitchSettings.colorDrift = analogGlitch.colorDrift;
        }
        
        // Copy Bokeh
        var bokeh = prefabCamera.GetComponent<Kino.Bokeh>();
        if (bokeh != null)
        {
            bokehSettings.enabled = bokeh.enabled;
            bokehSettings.focusDistance = bokeh.focusDistance;
            bokehSettings.fNumber = bokeh.fNumber;
            bokehSettings.useCameraFov = bokeh.useCameraFov;
            bokehSettings.focalLength = bokeh.focalLength;
            bokehSettings.kernelSize = bokeh.kernelSize;
        }
        
        // Copy Chunky
        var chunky = prefabCamera.GetComponent<Chunkyv2>();
        if (chunky != null)
        {
            chunkySettings.enabled = chunky.enabled;
            chunkySettings.spriteTexture = chunky.SprTex;
            chunkySettings.color = chunky.Color;
        }
        
        // Copy VHS
        var vhs = prefabCamera.GetComponent<VHSPostProcessEffect>();
        if (vhs != null)
        {
            vhsSettings.enabled = vhs.enabled;
            vhsSettings.shader = vhs.shader;
            vhsSettings.vhsClip = vhs.VHSClip;
            vhsSettings.vhsIntensity = vhs.vhsIntensity;
            vhsSettings.distortionAmount = vhs.distortionAmount;
            vhsSettings.scanlineIntensity = vhs.scanlineIntensity;
            vhsSettings.chromaticAberration = vhs.chromaticAberration;
            vhsSettings.noiseAmount = vhs.noiseAmount;
            vhsSettings.colorBleed = vhs.colorBleed;
            vhsSettings.brightness = vhs.brightness;
            vhsSettings.contrast = vhs.contrast;
            vhsSettings.saturation = vhs.saturation;
            vhsSettings.verticalGlitchSpeed = vhs.verticalGlitchSpeed;
            vhsSettings.horizontalGlitchSpeed = vhs.horizontalGlitchSpeed;
        }
        
        Debug.Log("<color=green>[CameraVisualController]</color> Successfully copied settings from prefab!");
    }
    #endif
}

// ==================== SERIALIZABLE SETTINGS CLASSES ====================

[System.Serializable]
public class CameraSettings
{
    public bool overrideSettings = true;
    [Range(1f, 179f)] public float fieldOfView = 76f;
    [Range(0.01f, 10f)] public float nearClipPlane = 0.3f;
    [Range(10f, 10000f)] public float farClipPlane = 1000f;
    public bool allowHDR = true;
    public bool allowMSAA = true;
}

[System.Serializable]
public class BloomSettings
{
    public bool enabled = true;
    
    [Header("Bloom Parameters")]
    [Range(0f, 2f)] public float threshold = 0.8f;
    [Range(0f, 1f)] public float softKnee = 0.5f;
    [Range(1f, 7f)] public float radius = 2.5f;
    [Range(0f, 2f)] public float intensity = 0.8f;
    public bool highQuality = true;
    public bool antiFlicker = true;
}

[System.Serializable]
public class VignetteSettings
{
    public bool enabled = true;
    
    [Header("Vignette Parameters")]
    [Range(0f, 1f)] public float intensity = 0.5f;
}

[System.Serializable]
public class DatamoshSettings
{
    public bool enabled = true;
    
    [Header("Datamosh Parameters")]
    [Range(4, 32)] public int blockSize = 16;
    [Range(0f, 1f)] public float entropy = 0.5f;
    [Range(0.5f, 4f)] public float noiseContrast = 1f;
    [Range(0f, 2f)] public float velocityScale = 0.8f;
    [Range(0f, 2f)] public float diffusion = 0.4f;
    public bool alwaysGlitch = false;
}

[System.Serializable]
public class TubeSettings
{
    public bool enabled = true;
    
    [Header("Tube Parameters")]
    [Range(0f, 1f)] public float bleeding = 0.5f;
    [Range(0f, 1f)] public float fringing = 0.5f;
    [Range(0f, 1f)] public float scanline = 0.5f;
}

[System.Serializable]
public class PixelationSettings
{
    public bool enabled = true;
    
    [Header("Pixelation Parameters")]
    [Range(64f, 2048f)] public float blockCount = 128f;
}

[System.Serializable]
public class BinarySettings
{
    public bool enabled = false;
    
    [Header("Binary Parameters")]
    public Kino.Binary.DitherType ditherType = Kino.Binary.DitherType.Bayer4x4;
    [Range(1, 8)] public int ditherScale = 1;
    public Color color0 = Color.black;
    public Color color1 = Color.white;
    [Range(0f, 1f)] public float opacity = 1f;
}

[System.Serializable]
public class AnalogGlitchSettings
{
    public bool enabled = false;
    
    [Header("Analog Glitch Parameters")]
    [Range(0f, 1f)] public float scanLineJitter = 0f;
    [Range(0f, 1f)] public float verticalJump = 0f;
    [Range(0f, 1f)] public float horizontalShake = 0f;
    [Range(0f, 1f)] public float colorDrift = 0f;
}

[System.Serializable]
public class BokehSettings
{
    public bool enabled = false;
    
    [Header("Bokeh Parameters (Depth of Field)")]
    public float focusDistance = 10f;
    public float fNumber = 1.4f;
    public bool useCameraFov = true;
    public float focalLength = 0.05f;
    public Kino.Bokeh.KernelSize kernelSize = Kino.Bokeh.KernelSize.Medium;
}

[System.Serializable]
public class ChunkySettings
{
    public bool enabled = false;
    
    [Header("Chunky Parameters")]
    public Texture2D spriteTexture = null;
    public Color color = Color.white;
}

[System.Serializable]
public class VHSSettings
{
    public bool enabled = false;
    
    [Header("Required Assets")]
    public Shader shader = null;
    public UnityEngine.Video.VideoClip vhsClip = null;
    
    [Header("VHS Effect Intensity")]
    [Range(0f, 1f)] public float vhsIntensity = 0.5f;
    
    [Header("Distortion & Glitches")]
    [Range(0f, 1f)] public float distortionAmount = 0.5f;
    [Range(0f, 1f)] public float scanlineIntensity = 0.3f;
    [Range(0f, 1f)] public float chromaticAberration = 0.3f;
    
    [Header("Noise & Artifacts")]
    [Range(0f, 1f)] public float noiseAmount = 0.2f;
    [Range(0f, 1f)] public float colorBleed = 0.5f;
    
    [Header("Color Grading")]
    [Range(-1f, 1f)] public float brightness = 0f;
    [Range(0f, 2f)] public float contrast = 1f;
    [Range(0f, 2f)] public float saturation = 0.9f;
    
    [Header("Animation Speed")]
    [Range(0f, 1f)] public float verticalGlitchSpeed = 0.01f;
    [Range(0f, 1f)] public float horizontalGlitchSpeed = 0.1f;
}

