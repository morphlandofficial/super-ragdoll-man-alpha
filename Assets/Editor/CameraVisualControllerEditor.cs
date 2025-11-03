using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom editor for CameraVisualController to provide better inspector workflow
/// </summary>
[CustomEditor(typeof(CameraVisualController))]
public class CameraVisualControllerEditor : Editor
{
    private bool showCameraSettings = true;
    private bool showBloomSettings = true;
    private bool showVignetteSettings = true;
    private bool showDatamoshSettings = true;
    private bool showTubeSettings = true;
    private bool showPixelationSettings = true;
    private bool showBinarySettings = false;
    private bool showAnalogGlitchSettings = false;
    private bool showBokehSettings = false;
    private bool showChunkySettings = false;
    private bool showVHSSettings = false;
    
    public override void OnInspectorGUI()
    {
        CameraVisualController controller = (CameraVisualController)target;
        
        // Title
        EditorGUILayout.Space(5);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 14;
        titleStyle.normal.textColor = new Color(0.3f, 0.7f, 1f);
        EditorGUILayout.LabelField("🎥 Camera Visual Controller", titleStyle);
        EditorGUILayout.Space(5);
        
        // Character Prefab Reference
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Character Prefab", EditorStyles.boldLabel);
        controller.characterPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Default Character Prefab", 
            controller.characterPrefab, 
            typeof(GameObject), 
            false
        );
        
        EditorGUILayout.Space(5);
        
        // Copy From Prefab Button
        GUI.enabled = controller.characterPrefab != null;
        if (GUILayout.Button("📋 Copy Settings From Prefab", GUILayout.Height(30)))
        {
            controller.CopyFromPrefab();
            EditorUtility.SetDirty(controller);
        }
        GUI.enabled = true;
        
        if (controller.characterPrefab == null)
        {
            EditorGUILayout.HelpBox("Drag the Default Character prefab above, then click 'Copy Settings From Prefab' to load its camera settings.", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
        
        // Camera Settings
        showCameraSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showCameraSettings, "📷 Camera Settings");
        if (showCameraSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawCameraSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Visual Effects Section
        EditorGUILayout.LabelField("Visual Effects", EditorStyles.boldLabel);
        
        // Bloom
        showBloomSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showBloomSettings, GetEffectLabel("✨ Bloom", controller.bloomSettings.enabled));
        if (showBloomSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawBloomSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Vignette
        showVignetteSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showVignetteSettings, GetEffectLabel("🌑 Vignette", controller.vignetteSettings.enabled));
        if (showVignetteSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawVignetteSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Datamosh
        showDatamoshSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showDatamoshSettings, GetEffectLabel("📺 Datamosh", controller.datamoshSettings.enabled));
        if (showDatamoshSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawDatamoshSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Tube
        showTubeSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showTubeSettings, GetEffectLabel("📼 Tube (CRT)", controller.tubeSettings.enabled));
        if (showTubeSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawTubeSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Pixelation
        showPixelationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showPixelationSettings, GetEffectLabel("🎮 Pixelation", controller.pixelationSettings.enabled));
        if (showPixelationSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawPixelationSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Binary
        showBinarySettings = EditorGUILayout.BeginFoldoutHeaderGroup(showBinarySettings, GetEffectLabel("⬛ Binary", controller.binarySettings.enabled));
        if (showBinarySettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawBinarySettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Analog Glitch
        showAnalogGlitchSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showAnalogGlitchSettings, GetEffectLabel("📡 Analog Glitch", controller.analogGlitchSettings.enabled));
        if (showAnalogGlitchSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawAnalogGlitchSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Bokeh
        showBokehSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showBokehSettings, GetEffectLabel("🌸 Bokeh (Depth of Field)", controller.bokehSettings.enabled));
        if (showBokehSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawBokehSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // Chunky
        showChunkySettings = EditorGUILayout.BeginFoldoutHeaderGroup(showChunkySettings, GetEffectLabel("🧱 Chunky", controller.chunkySettings.enabled));
        if (showChunkySettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawChunkySettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(5);
        
        // VHS
        showVHSSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showVHSSettings, GetEffectLabel("📹 VHS Glitch", controller.vhsSettings.enabled));
        if (showVHSSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawVHSSettings(controller);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        // Save changes
        if (GUI.changed)
        {
            EditorUtility.SetDirty(controller);
        }
    }
    
    private string GetEffectLabel(string label, bool enabled)
    {
        return enabled ? label + " ✓" : label;
    }
    
    private void DrawCameraSettings(CameraVisualController controller)
    {
        controller.cameraSettings.overrideSettings = EditorGUILayout.Toggle("Override Settings", controller.cameraSettings.overrideSettings);
        
        if (controller.cameraSettings.overrideSettings)
        {
            controller.cameraSettings.fieldOfView = EditorGUILayout.Slider("Field of View", controller.cameraSettings.fieldOfView, 1f, 179f);
            controller.cameraSettings.nearClipPlane = EditorGUILayout.Slider("Near Clip Plane", controller.cameraSettings.nearClipPlane, 0.01f, 10f);
            controller.cameraSettings.farClipPlane = EditorGUILayout.Slider("Far Clip Plane", controller.cameraSettings.farClipPlane, 10f, 10000f);
            controller.cameraSettings.allowHDR = EditorGUILayout.Toggle("Allow HDR", controller.cameraSettings.allowHDR);
            controller.cameraSettings.allowMSAA = EditorGUILayout.Toggle("Allow MSAA", controller.cameraSettings.allowMSAA);
        }
    }
    
    private void DrawBloomSettings(CameraVisualController controller)
    {
        controller.bloomSettings.enabled = EditorGUILayout.Toggle("Enabled", controller.bloomSettings.enabled);
        
        if (controller.bloomSettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.bloomSettings.threshold = EditorGUILayout.Slider("Threshold", controller.bloomSettings.threshold, 0f, 2f);
            controller.bloomSettings.softKnee = EditorGUILayout.Slider("Soft Knee", controller.bloomSettings.softKnee, 0f, 1f);
            controller.bloomSettings.radius = EditorGUILayout.Slider("Radius", controller.bloomSettings.radius, 1f, 7f);
            controller.bloomSettings.intensity = EditorGUILayout.Slider("Intensity", controller.bloomSettings.intensity, 0f, 2f);
            controller.bloomSettings.highQuality = EditorGUILayout.Toggle("High Quality", controller.bloomSettings.highQuality);
            controller.bloomSettings.antiFlicker = EditorGUILayout.Toggle("Anti Flicker", controller.bloomSettings.antiFlicker);
        }
    }
    
    private void DrawVignetteSettings(CameraVisualController controller)
    {
        controller.vignetteSettings.enabled = EditorGUILayout.Toggle("Enabled", controller.vignetteSettings.enabled);
        
        if (controller.vignetteSettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.vignetteSettings.intensity = EditorGUILayout.Slider("Intensity", controller.vignetteSettings.intensity, 0f, 1f);
        }
    }
    
    private void DrawDatamoshSettings(CameraVisualController controller)
    {
        controller.datamoshSettings.enabled = EditorGUILayout.Toggle("Enabled", controller.datamoshSettings.enabled);
        
        if (controller.datamoshSettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.datamoshSettings.blockSize = EditorGUILayout.IntSlider("Block Size", controller.datamoshSettings.blockSize, 4, 32);
            controller.datamoshSettings.entropy = EditorGUILayout.Slider("Entropy", controller.datamoshSettings.entropy, 0f, 1f);
            controller.datamoshSettings.noiseContrast = EditorGUILayout.Slider("Noise Contrast", controller.datamoshSettings.noiseContrast, 0.5f, 4f);
            controller.datamoshSettings.velocityScale = EditorGUILayout.Slider("Velocity Scale", controller.datamoshSettings.velocityScale, 0f, 2f);
            controller.datamoshSettings.diffusion = EditorGUILayout.Slider("Diffusion", controller.datamoshSettings.diffusion, 0f, 2f);
            controller.datamoshSettings.alwaysGlitch = EditorGUILayout.Toggle("Always Glitch", controller.datamoshSettings.alwaysGlitch);
        }
    }
    
    private void DrawTubeSettings(CameraVisualController controller)
    {
        controller.tubeSettings.enabled = EditorGUILayout.Toggle("Enabled", controller.tubeSettings.enabled);
        
        if (controller.tubeSettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.tubeSettings.bleeding = EditorGUILayout.Slider("Bleeding", controller.tubeSettings.bleeding, 0f, 1f);
            controller.tubeSettings.fringing = EditorGUILayout.Slider("Fringing", controller.tubeSettings.fringing, 0f, 1f);
            controller.tubeSettings.scanline = EditorGUILayout.Slider("Scanline", controller.tubeSettings.scanline, 0f, 1f);
        }
    }
    
    private void DrawPixelationSettings(CameraVisualController controller)
    {
        controller.pixelationSettings.enabled = EditorGUILayout.Toggle("Enabled", controller.pixelationSettings.enabled);
        
        if (controller.pixelationSettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.pixelationSettings.blockCount = EditorGUILayout.Slider("Block Count", controller.pixelationSettings.blockCount, 64f, 2048f);
        }
    }
    
    private void DrawBinarySettings(CameraVisualController controller)
    {
        controller.binarySettings.enabled = EditorGUILayout.Toggle("Enabled", controller.binarySettings.enabled);
        
        if (controller.binarySettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.binarySettings.ditherType = (Kino.Binary.DitherType)EditorGUILayout.EnumPopup("Dither Type", controller.binarySettings.ditherType);
            controller.binarySettings.ditherScale = EditorGUILayout.IntSlider("Dither Scale", controller.binarySettings.ditherScale, 1, 8);
            controller.binarySettings.color0 = EditorGUILayout.ColorField("Color 0 (Dark)", controller.binarySettings.color0);
            controller.binarySettings.color1 = EditorGUILayout.ColorField("Color 1 (Light)", controller.binarySettings.color1);
            controller.binarySettings.opacity = EditorGUILayout.Slider("Opacity", controller.binarySettings.opacity, 0f, 1f);
        }
    }
    
    private void DrawAnalogGlitchSettings(CameraVisualController controller)
    {
        controller.analogGlitchSettings.enabled = EditorGUILayout.Toggle("Enabled", controller.analogGlitchSettings.enabled);
        
        if (controller.analogGlitchSettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.analogGlitchSettings.scanLineJitter = EditorGUILayout.Slider("Scan Line Jitter", controller.analogGlitchSettings.scanLineJitter, 0f, 1f);
            controller.analogGlitchSettings.verticalJump = EditorGUILayout.Slider("Vertical Jump", controller.analogGlitchSettings.verticalJump, 0f, 1f);
            controller.analogGlitchSettings.horizontalShake = EditorGUILayout.Slider("Horizontal Shake", controller.analogGlitchSettings.horizontalShake, 0f, 1f);
            controller.analogGlitchSettings.colorDrift = EditorGUILayout.Slider("Color Drift", controller.analogGlitchSettings.colorDrift, 0f, 1f);
        }
    }
    
    private void DrawBokehSettings(CameraVisualController controller)
    {
        controller.bokehSettings.enabled = EditorGUILayout.Toggle("Enabled", controller.bokehSettings.enabled);
        
        if (controller.bokehSettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.bokehSettings.focusDistance = EditorGUILayout.FloatField("Focus Distance", controller.bokehSettings.focusDistance);
            controller.bokehSettings.fNumber = EditorGUILayout.FloatField("F Number", controller.bokehSettings.fNumber);
            controller.bokehSettings.useCameraFov = EditorGUILayout.Toggle("Use Camera FOV", controller.bokehSettings.useCameraFov);
            if (!controller.bokehSettings.useCameraFov)
            {
                controller.bokehSettings.focalLength = EditorGUILayout.FloatField("Focal Length", controller.bokehSettings.focalLength);
            }
            controller.bokehSettings.kernelSize = (Kino.Bokeh.KernelSize)EditorGUILayout.EnumPopup("Kernel Size", controller.bokehSettings.kernelSize);
        }
    }
    
    private void DrawChunkySettings(CameraVisualController controller)
    {
        controller.chunkySettings.enabled = EditorGUILayout.Toggle("Enabled", controller.chunkySettings.enabled);
        
        if (controller.chunkySettings.enabled)
        {
            EditorGUILayout.Space(5);
            controller.chunkySettings.spriteTexture = (Texture2D)EditorGUILayout.ObjectField("Sprite Texture", controller.chunkySettings.spriteTexture, typeof(Texture2D), false);
            controller.chunkySettings.color = EditorGUILayout.ColorField("Color", controller.chunkySettings.color);
        }
    }
    
    private void DrawVHSSettings(CameraVisualController controller)
    {
        controller.vhsSettings.enabled = EditorGUILayout.Toggle("Enabled", controller.vhsSettings.enabled);
        
        if (controller.vhsSettings.enabled)
        {
            EditorGUILayout.Space(5);
            
            // Required Assets
            EditorGUILayout.LabelField("Required Assets", EditorStyles.boldLabel);
            controller.vhsSettings.shader = (Shader)EditorGUILayout.ObjectField("VHS Shader", controller.vhsSettings.shader, typeof(Shader), false);
            controller.vhsSettings.vhsClip = (UnityEngine.Video.VideoClip)EditorGUILayout.ObjectField("VHS Video Clip", controller.vhsSettings.vhsClip, typeof(UnityEngine.Video.VideoClip), false);
            
            if (controller.vhsSettings.shader == null)
            {
                EditorGUILayout.HelpBox("Assign the VHSPostProcessEffect shader (usually at Assets/Kino/unity-vhsglitch-master/Shaders/VHSPostProcessEffect.shader)", MessageType.Warning);
            }
            if (controller.vhsSettings.vhsClip == null)
            {
                EditorGUILayout.HelpBox("Assign the glitch video clip (usually at Assets/Kino/unity-vhsglitch-master/MovieTexture/glitch.mp4)", MessageType.Warning);
            }
            
            EditorGUILayout.Space(10);
            
            // VHS Effect Intensity
            EditorGUILayout.LabelField("VHS Effect Intensity", EditorStyles.boldLabel);
            controller.vhsSettings.vhsIntensity = EditorGUILayout.Slider("Intensity", controller.vhsSettings.vhsIntensity, 0f, 1f);
            
            EditorGUILayout.Space(5);
            
            // Distortion & Glitches
            EditorGUILayout.LabelField("Distortion & Glitches", EditorStyles.boldLabel);
            controller.vhsSettings.distortionAmount = EditorGUILayout.Slider("Distortion Amount", controller.vhsSettings.distortionAmount, 0f, 1f);
            controller.vhsSettings.scanlineIntensity = EditorGUILayout.Slider("Scanline Intensity", controller.vhsSettings.scanlineIntensity, 0f, 1f);
            controller.vhsSettings.chromaticAberration = EditorGUILayout.Slider("Chromatic Aberration", controller.vhsSettings.chromaticAberration, 0f, 1f);
            
            EditorGUILayout.Space(5);
            
            // Noise & Artifacts
            EditorGUILayout.LabelField("Noise & Artifacts", EditorStyles.boldLabel);
            controller.vhsSettings.noiseAmount = EditorGUILayout.Slider("Noise Amount", controller.vhsSettings.noiseAmount, 0f, 1f);
            controller.vhsSettings.colorBleed = EditorGUILayout.Slider("Color Bleed", controller.vhsSettings.colorBleed, 0f, 1f);
            
            EditorGUILayout.Space(5);
            
            // Color Grading
            EditorGUILayout.LabelField("Color Grading", EditorStyles.boldLabel);
            controller.vhsSettings.brightness = EditorGUILayout.Slider("Brightness", controller.vhsSettings.brightness, -1f, 1f);
            controller.vhsSettings.contrast = EditorGUILayout.Slider("Contrast", controller.vhsSettings.contrast, 0f, 2f);
            controller.vhsSettings.saturation = EditorGUILayout.Slider("Saturation", controller.vhsSettings.saturation, 0f, 2f);
            
            EditorGUILayout.Space(5);
            
            // Animation Speed
            EditorGUILayout.LabelField("Animation Speed", EditorStyles.boldLabel);
            controller.vhsSettings.verticalGlitchSpeed = EditorGUILayout.Slider("Vertical Glitch Speed", controller.vhsSettings.verticalGlitchSpeed, 0f, 1f);
            controller.vhsSettings.horizontalGlitchSpeed = EditorGUILayout.Slider("Horizontal Glitch Speed", controller.vhsSettings.horizontalGlitchSpeed, 0f, 1f);
        }
    }
}

