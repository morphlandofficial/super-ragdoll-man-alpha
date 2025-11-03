using UnityEngine;
using UnityEngine.Video;

[ExecuteInEditMode]
[AddComponentMenu("Image Effects/VHS Glitch Effect")]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(VideoPlayer))]
public class VHSPostProcessEffect : MonoBehaviour
{
	[Header("Required Assets")]
	public Shader shader;
	public VideoClip VHSClip;
	
	[Header("VHS Effect Intensity")]
	[Range(0f, 1f)]
	[Tooltip("Overall intensity of the VHS overlay effect")]
	public float vhsIntensity = 0.5f;
	
	[Header("Distortion & Glitches")]
	[Range(0f, 1f)]
	[Tooltip("Amount of image distortion and warping")]
	public float distortionAmount = 0.5f;
	
	[Range(0f, 1f)]
	[Tooltip("Visibility of horizontal scanlines")]
	public float scanlineIntensity = 0.3f;
	
	[Range(0f, 1f)]
	[Tooltip("Amount of chromatic aberration (RGB color split)")]
	public float chromaticAberration = 0.3f;
	
	[Header("Noise & Artifacts")]
	[Range(0f, 1f)]
	[Tooltip("Amount of static noise/grain")]
	public float noiseAmount = 0.2f;
	
	[Range(0f, 1f)]
	[Tooltip("Intensity of red color bleeding")]
	public float colorBleed = 0.5f;
	
	[Header("Color Grading")]
	[Range(-1f, 1f)]
	[Tooltip("Brightness adjustment")]
	public float brightness = 0f;
	
	[Range(0f, 2f)]
	[Tooltip("Contrast adjustment")]
	public float contrast = 1f;
	
	[Range(0f, 2f)]
	[Tooltip("Saturation adjustment (0 = grayscale, 1 = normal, 2 = hyper-saturated)")]
	public float saturation = 0.9f;
	
	[Header("Animation Speed")]
	[Range(0f, 1f)]
	[Tooltip("Speed of vertical glitch movement")]
	public float verticalGlitchSpeed = 0.01f;
	
	[Range(0f, 1f)]
	[Tooltip("Speed of horizontal scanline movement")]
	public float horizontalGlitchSpeed = 0.1f;

	private float _yScanline;
	private float _xScanline;
	private Material _material = null;
	private VideoPlayer _player;

	void OnEnable()
	{
		if (shader == null)
		{
			Debug.LogWarning("[VHSPostProcessEffect] Shader is not assigned!");
			return;
		}
		
		_material = new Material(shader);
		_player = GetComponent<VideoPlayer>();
		
		if (_player == null)
		{
			Debug.LogWarning("[VHSPostProcessEffect] VideoPlayer component not found!");
			return;
		}
		
		_player.isLooping = true;
		_player.renderMode = VideoRenderMode.APIOnly;
		_player.audioOutputMode = VideoAudioOutputMode.None;
		_player.clip = VHSClip;
		
		if (VHSClip != null)
		{
			_player.Play();
		}
	}

	void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (_material == null || shader == null)
		{
			Graphics.Blit(source, destination);
			return;
		}
		
		// Set VHS texture if available
		if (_player != null && _player.texture != null)
		{
			_material.SetTexture("_VHSTex", _player.texture);
		}

		// Animate scanlines
		_yScanline += Time.deltaTime * verticalGlitchSpeed;
		_xScanline -= Time.deltaTime * horizontalGlitchSpeed;

		if (_yScanline >= 1)
		{
			_yScanline = Random.value;
		}
		if (_xScanline <= 0 || Random.value < 0.05)
		{
			_xScanline = Random.value;
		}
		
		// Set all shader parameters
		_material.SetFloat("_yScanline", _yScanline);
		_material.SetFloat("_xScanline", _xScanline);
		_material.SetFloat("_VHSIntensity", vhsIntensity);
		_material.SetFloat("_NoiseAmount", noiseAmount);
		_material.SetFloat("_DistortionAmount", distortionAmount);
		_material.SetFloat("_ColorBleed", colorBleed);
		_material.SetFloat("_ScanlineIntensity", scanlineIntensity);
		_material.SetFloat("_Brightness", brightness);
		_material.SetFloat("_Contrast", contrast);
		_material.SetFloat("_Saturation", saturation);
		_material.SetFloat("_ChromaticAberration", chromaticAberration);
		
		Graphics.Blit(source, destination, _material);
	}

	protected void OnDisable()
	{
		if (_material)
		{
			DestroyImmediate(_material);
		}
	}
}