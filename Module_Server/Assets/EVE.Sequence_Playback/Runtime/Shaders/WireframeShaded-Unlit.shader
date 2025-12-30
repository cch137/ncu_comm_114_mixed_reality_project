Shader "SuperSystems/Wireframe-Shaded-Unlit"
{
	Properties
	{
		_MainTex ("MainTex", 2D) = "white" {}
		_WireThickness ("Wire Thickness", RANGE(0, 800)) = 100
		_WireSmoothness ("Wire Smoothness", RANGE(0, 20)) = 3
		_WireColor ("Wire Color", Color) = (0.0, 1.0, 0.0, 1.0)
		_BaseColor ("Base Color", Color) = (0.0, 0.0, 0.0, 1.0)
	}

	SubShader
	{
		Tags {
			"RenderType"="Opaque"
		}
        LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			#include "UnityCG.cginc"

			uniform sampler2D _MainTex; uniform float4 _MainTex_ST;
			uniform float _WireThickness;
			uniform float _WireSmoothness;
			uniform float4 _WireColor; 
			uniform float4 _BaseColor;

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 worldSpacePosition : TEXCOORD1;
				float4 color : COLOR;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			struct g2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 worldSpacePosition : TEXCOORD1;
				float4 dist : TEXCOORD2;
				float4 color : COLOR;
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldSpacePosition = mul(unity_ObjectToWorld, v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				return o;
			}
			
			[maxvertexcount(3)]
			void geom(triangle v2f i[3], inout TriangleStream<g2f> triangleStream)
			{
				float2 p0 = i[0].vertex.xy / i[0].vertex.w;
				float2 p1 = i[1].vertex.xy / i[1].vertex.w;
				float2 p2 = i[2].vertex.xy / i[2].vertex.w;

				float2 edge0 = p2 - p1;
				float2 edge1 = p2 - p0;
				float2 edge2 = p1 - p0;

				// To find the distance to the opposite edge, we take the
				// formula for finding the area of a triangle Area = Base/2 * Height, 
				// and solve for the Height = (Area * 2)/Base.
				// We can get the area of a triangle by taking its cross product
				// divided by 2.  However we can avoid dividing our area/base by 2
				// since our cross product will already be double our area.
				float area = abs(edge1.x * edge2.y - edge1.y * edge2.x);
				float wireThickness = 800 - _WireThickness;

				g2f o;
				
				o.uv = i[0].uv;
				o.worldSpacePosition = i[0].worldSpacePosition;
				o.vertex = i[0].vertex;
				o.dist.xyz = float3( (area / length(edge0)), 0.0, 0.0) * o.vertex.w * wireThickness;
				o.dist.w = 1.0 / o.vertex.w;
				o.color = i[0].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[0], o);
				triangleStream.Append(o);

				o.uv = i[1].uv;
				o.worldSpacePosition = i[1].worldSpacePosition;
				o.vertex = i[1].vertex;
				o.dist.xyz = float3(0.0, (area / length(edge1)), 0.0) * o.vertex.w * wireThickness;
				o.dist.w = 1.0 / o.vertex.w;
				o.color = i[1].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[1], o);
				triangleStream.Append(o);

				o.uv = i[2].uv;
				o.worldSpacePosition = i[2].worldSpacePosition;
				o.vertex = i[2].vertex;
				o.dist.xyz = float3(0.0, 0.0, (area / length(edge2))) * o.vertex.w * wireThickness;
				o.dist.w = 1.0 / o.vertex.w;
				o.color = i[2].color;
				UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(i[2], o);
				triangleStream.Append(o);
			}
			
			float3 Unity_ColorspaceConversion_RGB_RGB_float(float3 In)
			{
				float3 linearRGBLo = In / 12.92;;
				float3 linearRGBHi = pow(max(abs((In + 0.055) / 1.055), 1.192092896e-07), float3(2.4, 2.4, 2.4));
				return float3(In <= 0.04045) ? linearRGBLo : linearRGBHi;
			}

			fixed4 frag (g2f i) : SV_Target
			{
				float minDistanceToEdge = min(i.dist[0], min(i.dist[1], i.dist[2])) * i.dist[3];

				float4 baseColor = i.color * _BaseColor * tex2D(_MainTex, i.uv);
				baseColor.rgb = Unity_ColorspaceConversion_RGB_RGB_float(baseColor.rgb);

				// Early out if we know we are not on a line segment.
				if(minDistanceToEdge > 0.9)
				{
					return fixed4(baseColor.rgb,0);
				}

				// Smooth our line out
				float t = exp2(_WireSmoothness * -1.0 * minDistanceToEdge * minDistanceToEdge);
				fixed4 finalColor = lerp(baseColor, _WireColor, t);
				finalColor.a = t;

				return finalColor;
			}
			ENDCG
		}
	}
}
