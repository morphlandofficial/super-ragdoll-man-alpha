Shader "Hidden/Custom/Chunky"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_SprTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Color", Color) = (1, 1, 1, 1)
	}

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			sampler2D _SprTex;
			float4 _Color;
			float2 BlockCount;
			float2 BlockSize;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				float2 blockPos = floor(i.uv * BlockCount);
				float2 blockCenter = blockPos * BlockSize + BlockSize * 0.5;

				// (2)
				float4 del = float4(1, 1, 1, 1) - _Color;

				// (3)
				float4 tex = tex2D(_MainTex, blockCenter) - del;
				float grayscale = dot(tex.rgb, float3(0.3, 0.59, 0.11));
				grayscale = clamp(grayscale, 0.0, 1.0);

				// (4)
				float dx = floor(grayscale * 16.0);

				// (5)
				float2 sprPos = i.uv;
				sprPos -= blockPos * BlockSize;
				sprPos.x /= 16;
				sprPos *= BlockCount;
				sprPos.x += 1.0 / 16.0 * dx;

				// (6)
				float4 tex2 = tex2D(_SprTex, sprPos);
				return tex2;
			}
			ENDCG
		}
	}
}
