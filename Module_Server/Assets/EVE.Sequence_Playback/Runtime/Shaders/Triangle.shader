Shader "EF EVE/Triangle"
{
	Properties
	{
		_Divider("Scale", Range(0.1, 1000000.1)) = 1000.0
		_PointSize("PointSize", Range(0, 0.1)) = 0.0025
		_Center("Center",Vector) = (0,0,0,0)
		//_ObjectPos("ObjectPos",Vector) = (0,0,0,0) //should be transform
		_MinDist("MinDist",Range(0.001,1)) = 0.1
		_MaxDist("MaxDist",Range(1,100)) = 10.0
		_ThresholdMin("ThresholdMin",Range(0,1)) = 0.01
		_ThresholdMax("ThresholdMax",Range(0,1)) = 1
		//_MirrorX("MirrorX",Range(0,1)) = 0
		//_MirrorY("MirrorY",Range(0,1)) = 0
		_MirrorZ("MirrorZ",Range(0,1)) = 1.0

		//_LightingAffect("LightingAffect", Float) = 0.5
		_ScalePower("ScalePower",Range(0.01,4)) = 1.0
		_GreenTreshold("GreenTreshold",Range(0,1)) = 0.0
		_DarkGreenTreshold("DarkGreenTreshold",Range(0,1)) = 0.0

		_BlueTreshold("BlueTreshold",Range(0,1)) = 0.0
		_DarkBlueTreshold("DarkBlueTreshold",Range(0,1)) = 0.0

		_Contrast("Contrast", float) = 1
		_GreenScreenEnabled("GreenScreenEnabled",Range(0,1))=1.0
	}

	SubShader 
	{
		Pass 
		{
			//Tags{ "RenderType" = "Opaque","LightMode" = "ForwardBase" }
			//Tags { "LightMode" = "ForwardBase" }
			Tags { "Queue"="Transparent" "RenderType"="Transparent"}
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			LOD 200
			//ColorMask RGB
			//ZWrite On
			//Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma target 5.0
			
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry GS_Main
			
			#include "UnityCG.cginc"

			//with normals
//#include "AutoLight.cginc"
//#include "UnityLightingCommon.cginc"
			
		
			// Pixel shader input
			struct PS_INPUT
			{
				float4 position			: SV_POSITION;
				float4 color			: COLOR;
			};
			//geometry
			struct GS_INPUT
			{
				float4	position0		: POSITION0;
				float4	position1		: POSITION1;
				float4	position2		: POSITION2;
				float4	color0			: COLOR0;
				float4	color1			: COLOR1;
				float4	color2			: COLOR2;
			};	

			StructuredBuffer<float> particleBuffer;
			StructuredBuffer<float> colorBuffer;
			StructuredBuffer<int> meshBuffer;

			uniform float _Divider;

			uniform float4 _Center;

			uniform float _ThresholdMin;
			uniform float _ThresholdMax;
			uniform float _MirrorX;
			uniform float _MirrorY;
			uniform float _MirrorZ;

			uniform float _GreenTreshold;
			uniform float _DarkGreenTreshold;
			uniform float _BlueTreshold;
			uniform float _DarkBlueTreshold;
			uniform float _Contrast;
			uniform float _GreenScreenEnabled;
    
			float4x4 _ObjectTransform;

			float _PointSize;
			float _MinDist;
			float _MaxDist;

			float _ScalePower;

			inline float4 VertexColor(int VertexIdx)
			{
				float3 col_rgb = float3(
					colorBuffer[VertexIdx + 2] / 255.0f,
					colorBuffer[VertexIdx + 1] / 255.0f,
					colorBuffer[VertexIdx + 0] / 255.0f);

				if (!IsGammaSpace())
					col_rgb = GammaToLinearSpace(col_rgb);

				return float4(col_rgb, 1.0f);
			}

			inline float4 VertexPosition(int VertexIdx)
			{
				float4 relative_pos = float4(
					(particleBuffer[VertexIdx + 0] - _Center.x / 2) / _Divider,
					(particleBuffer[VertexIdx + 1] - _Center.y / 2) / _Divider,
					(particleBuffer[VertexIdx + 2] - _Center.z / 2) / _Divider,
					1.0f
					);

				if (_MirrorZ > 0.5f)
					relative_pos.z = -relative_pos.z;

				relative_pos = mul(_ObjectTransform, relative_pos);
				return mul(unity_ObjectToWorld, relative_pos);
			}

			// Vertex shader
			GS_INPUT vert(uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
			{
				GS_INPUT o = (GS_INPUT)0;

				uint meshPosition = instance_id * 3;

				o.color0 = VertexColor(3 * meshBuffer[meshPosition + 0]);
				o.color1 = VertexColor(3 * meshBuffer[meshPosition + 1]);
				o.color2 = VertexColor(3 * meshBuffer[meshPosition + 2]);

				o.position0 = VertexPosition(3 * meshBuffer[meshPosition + 0]);
				o.position1 = VertexPosition(3 * meshBuffer[meshPosition + 1]);
				o.position2 = VertexPosition(3 * meshBuffer[meshPosition + 2]);

				return o;
			}

			//Geometry shader
			[maxvertexcount(3)]
			void GS_Main(point GS_INPUT p[1], inout TriangleStream<PS_INPUT> triStream)
			{
				PS_INPUT pIn;

				pIn.position = UnityObjectToClipPos(p[0].position0);
				pIn.color = p[0].color0;
				triStream.Append(pIn);
				
				pIn.position = UnityObjectToClipPos(p[0].position1);
				pIn.color = p[0].color1;
				triStream.Append(pIn);
				
				pIn.position = UnityObjectToClipPos(p[0].position2);
				pIn.color = p[0].color2;
				triStream.Append(pIn);
			}

			// Pixel shader
			float4 frag(PS_INPUT i) : COLOR
			{
				//may need to discard black and white
				if (i.color.r < _ThresholdMin&&i.color.g < _ThresholdMin&&i.color.b < _ThresholdMin)
					discard;

				if (i.color.r > _ThresholdMax&&i.color.g > _ThresholdMax&&i.color.b > _ThresholdMax)
					discard;
				
				///GREEN SCREEN
				fixed4 fin_col = i.color;

				if (_GreenScreenEnabled > 0.5)
				{

					fin_col = (fin_col - 0.5)*_Contrast + 0.5;

					if (fin_col.g > _DarkGreenTreshold)
					{
						fixed4 val = ceil(saturate(fin_col.g - fin_col.r - _GreenTreshold)) * ceil(saturate(fin_col.g - fin_col.b - _GreenTreshold));

						fin_col = lerp(fin_col, fixed4(0., 0., 0., 0.), val);

						//if (fin_col.a < 0.005)
							//discard;
					}

					if (fin_col.b > _DarkBlueTreshold)
					{
						if (fin_col.b - fin_col.g < 0.0001)
							discard;

						fixed4 val = ceil(saturate(fin_col.b - fin_col.r - _BlueTreshold)) * ceil(saturate(fin_col.b - fin_col.g - _BlueTreshold));

						fin_col = lerp(fin_col, fixed4(0., 0., 0., 0.), val);

						//if (fin_col.a < 0.005)
							//discard;
					}
				}
				return fin_col;
			}
			ENDCG
		}
	}
	Fallback Off
}
