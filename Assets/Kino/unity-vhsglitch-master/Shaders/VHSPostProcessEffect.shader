Shader "Hidden/VHSPostProcessEffect" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_VHSTex ("Base (RGB)", 2D) = "white" {}
	}

	SubShader {
		Pass {
			ZTest Always Cull Off ZWrite Off
			Fog { Mode off }
					
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest 
			#include "UnityCG.cginc"

			uniform sampler2D _MainTex;
			uniform sampler2D _VHSTex;
			
			// Scanline parameters
			float _yScanline;
			float _xScanline;
			
			// Configurable parameters
			float _VHSIntensity;        // Overall VHS effect intensity (0-1)
			float _NoiseAmount;         // Amount of noise/static (0-1)
			float _DistortionAmount;    // Amount of distortion (0-1)
			float _ColorBleed;          // Color bleeding intensity (0-1)
			float _ScanlineIntensity;   // Scanline visibility (0-1)
			float _Brightness;          // Brightness adjustment (-1 to 1)
			float _Contrast;            // Contrast adjustment (0-2)
			float _Saturation;          // Saturation adjustment (0-2)
			float _ChromaticAberration; // RGB shift amount (0-1)
			
			float rand(float3 co){
			     return frac(sin( dot(co.xyz ,float3(12.9898,78.233,45.5432) )) * 43758.5453);
			}
			
			// Apply brightness/contrast/saturation
			fixed3 ApplyColorGrading(fixed3 color, float brightness, float contrast, float saturation) {
				// Brightness
				color += brightness;
				
				// Contrast
				color = (color - 0.5) * contrast + 0.5;
				
				// Saturation
				float gray = dot(color, float3(0.299, 0.587, 0.114));
				color = lerp(float3(gray, gray, gray), color, saturation);
				
				return color;
			}
 
			fixed4 frag (v2f_img i) : COLOR{
				fixed4 vhs = tex2D (_VHSTex, i.uv);
				
				float dx = 1-abs(distance(i.uv.y, _xScanline));
				float dy = 1-abs(distance(i.uv.y, _yScanline));
				
				// Distortion based on parameter
				dy = ((int)(dy*15))/15.0;
				i.uv.x += dy * (0.025 * _DistortionAmount) + rand(float3(dy,dy,dy)).r * (_DistortionAmount/500.0);
				
				// Scanline intensity
				if(dx > (0.99 - _ScanlineIntensity * 0.1))
					i.uv.y = _xScanline;
				
				i.uv.x = i.uv.x % 1;
				i.uv.y = i.uv.y % 1;
				
				// Chromatic aberration
				float2 offset = float2(_ChromaticAberration * 0.005, 0);
				fixed4 c;
				c.r = tex2D (_MainTex, i.uv + offset).r;
				c.g = tex2D (_MainTex, i.uv).g;
				c.b = tex2D (_MainTex, i.uv - offset).b;
				c.a = 1.0;
				
				// Color bleeding
				float bleed = tex2D(_MainTex, i.uv + float2(0.01 * _ColorBleed, 0)).r;
				bleed += tex2D(_MainTex, i.uv + float2(0.02 * _ColorBleed, 0)).r;
				bleed += tex2D(_MainTex, i.uv + float2(0.01 * _ColorBleed, 0.01 * _ColorBleed)).r;
				bleed += tex2D(_MainTex, i.uv + float2(0.02 * _ColorBleed, 0.02 * _ColorBleed)).r;
				bleed /= 6;
				
				if(bleed > 0.1){
					vhs += fixed4(bleed * _xScanline * _ColorBleed, 0, 0, 0);
				}
				
				// Noise
				float x = ((int)(i.uv.x*320))/320.0;
				float y = ((int)(i.uv.y*240))/240.0;
				float noise = rand(float3(x, y, _xScanline)) * _NoiseAmount;
				c.rgb -= noise;
				
				// Mix VHS overlay based on intensity
				fixed4 result = c + (vhs * _VHSIntensity);
				
				// Apply color grading
				result.rgb = ApplyColorGrading(result.rgb, _Brightness, _Contrast, _Saturation);
				
				return result;
			}
			ENDCG
		}
	}
Fallback off
}