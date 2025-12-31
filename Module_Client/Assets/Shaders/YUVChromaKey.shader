Shader "Unlit/YUVChromaKey"

{
	Properties
	{
		[Toggle(MIRROR)] _Mirror("Horizontal Mirror", Float) = 0
		[HideInEditor][NoScaleOffset] _YPlane("Y plane", 2D) = "black" {}
		[HideInEditor][NoScaleOffset] _UPlane("U plane", 2D) = "gray" {}
		[HideInEditor][NoScaleOffset] _VPlane("V plane", 2D) = "gray" {}
		_MaskCol("Mask Color", Color) = (1.0, 0.0, 0.0, 1.0)
		_Sensitivity("Threshold Sensitivity", Range(0,1)) = 0.5
		_Smooth("Smoothing", Range(0,1)) = 0.5
		_Cutoff("Alpha Cutoff",Range(0,1)) = 0.5

	}
		SubShader
		{
			// No culling or depth
			Tags { "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout"}
			LOD 100
			ZTest Always Cull Back ZWrite On Lighting Off Fog { Mode off }
			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing
				#pragma multi_compile __ MIRROR

				#include "UnityCG.cginc"

				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float4 vertex : SV_POSITION;
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert(appdata v)
				{
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_INITIALIZE_OUTPUT(v2f, o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;
					// Flip texture coordinates vertically.
					// Texture2D.LoadRawTextureData() always expects a bottom-up image, but the MediaPlayer
					// upload code always get a top-down frame from WebRTC. The most efficient is to upload
					// as is (inverted) and revert here.
					o.uv.y = 1 - v.uv.y;

	#ifdef MIRROR
					// Optional left-right mirroring (horizontal flipping)
					o.uv.x = 1 - v.uv.x;
	#endif
					return o;
				}

				sampler2D _YPlane;
				sampler2D _UPlane;
				sampler2D _VPlane;
				float3 _MaskCol;
				float _Sensitivity;
				float _Smooth;
				float _Cutoff;


				half4 yuv2rgb(half3 yuv)
				{
					// The YUV to RBA conversion, please refer to: http://en.wikipedia.org/wiki/YUV
					// Y'UV420p (I420) to RGB888 conversion section.
					half y_value = yuv[0];
					half u_value = yuv[1];
					half v_value = yuv[2];
					half r = y_value + 1.4022 * (v_value - 0.5);
					half g = y_value - 0.7145 * (v_value - 0.5) - (0.345 * (u_value - 0.5));
					half b = y_value + 1.771 * (u_value - 0.5);
					return half4(r, g, b,1);
				}

				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 col;
					col.x = tex2D(_YPlane, i.uv).r;
					col.y = tex2D(_UPlane, i.uv).r;
					col.z = tex2D(_VPlane, i.uv).r;
					fixed4 c = yuv2rgb(col);

					float maskY = 0.2989 * _MaskCol.r + 0.5866 * _MaskCol.g + 0.1145 * _MaskCol.b;
					float maskCr = 0.7132 * (_MaskCol.r - maskY);
					float maskCb = 0.5647 * (_MaskCol.b - maskY);

					float Y = 0.2989 * c.r + 0.5866 * c.g + 0.1145 * c.b;
					float Cr = 0.7132 * (c.r - Y);
					float Cb = 0.5647 * (c.b - Y);

					float blendValue = smoothstep(_Sensitivity, _Sensitivity + _Smooth, distance(float2(Cr, Cb), float2(maskCr, maskCb)));
					//將背景減掉，製作出透明效果
					clip(c.a* blendValue - _Cutoff);

					return c * blendValue;


				}
				ENDCG
			}
		}
}