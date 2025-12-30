// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/GridShader"
{
 
    Properties {
 
      _GridThickness ("Grid Thickness", Float) = 0.01
 
      _GridSpacing ("Grid Spacing", Float) = 1.0
	  
      _Div ("Divisor at distance", Float) = 10.0
 
      _GridMainColour ("Grid Main Colour", Color) = (0.5, 1.0, 1.0, 1.0)
	  
	  _AntiAliasing ("Anti Aliasing", Float) = 0.1
	  
      _GridSecondaryColour ("Grid Secondary Colour", Color) = (0.5, 1.0, 1.0, 1.0)
 
      _BaseColour ("Base Colour", Color) = (0.0, 0.0, 0.0, 0.0)
 
    }
 
 
 
    SubShader {
 
      Tags { "Queue" = "Transparent" }
 
 
 
      Pass {
 
        ZWrite Off
 
        Blend SrcAlpha OneMinusSrcAlpha
 
 
 
        CGPROGRAM
 
 
 
        // Define the vertex and fragment shader functions
 
        #pragma vertex vert
 
        #pragma fragment frag
 
 
 
        // Access Shaderlab properties
 
        uniform float _GridThickness;
		
		uniform float _Div;
		
		uniform float _AntiAliasing;
 
        uniform float _GridSpacing;
 
        uniform float4 _GridMainColour;
		
        uniform float4 _GridSecondaryColour;
 
        uniform float4 _BaseColour;
 
 
 
        // Input into the vertex shader
 
        struct vertexInput {
 
            float4 vertex : POSITION;
 
        };
 
 
 
        // Output from vertex shader into fragment shader
 
        struct vertexOutput {
 
          float4 pos : SV_POSITION;
 
          float4 worldPos : TEXCOORD0;
 
        };
 
 
 
        // VERTEX SHADER
 
        vertexOutput vert(vertexInput input) {
 
          vertexOutput output;
 
          output.pos = UnityObjectToClipPos(input.vertex);
 
          // Calculate the world position coordinates to pass to the fragment shader
 
          output.worldPos = mul(unity_ObjectToWorld, input.vertex);
 
          return output;
 
        }
 
 
 
        // FRAGMENT SHADER
 
		float4 frag(vertexOutput input) : COLOR {
			float gt = _GridThickness + distance(input.worldPos, _WorldSpaceCameraPos)/_Div;
			
			_AntiAliasing*=gt;
			float2 st = float2(input.worldPos.x, input.worldPos.z);
			float2 g = smoothstep(1.-gt-_AntiAliasing,1.-gt,2.*abs(frac((st + gt/2.) / _GridSpacing)-.5));
			float f = g.x+g.y-g.x*g.y;
			
			float gs = (gt+_AntiAliasing)*_GridSpacing/2.;
			bool x = st.x < gs && st.x > -gs;
			bool y = st.y < gs && st.y > -gs;
			if(x&&y) return float4(_GridMainColour.rgb, f);
			else if(x){
				if(g.y==0) return float4(_GridMainColour.rgb, f);
				return float4(lerp(_GridMainColour, _GridSecondaryColour, smoothstep(0,0.75, g.y/g.x-g.x*g.y-.125)).rgb, f);
			}
			else if(y){
				if(g.x==0) return float4(_GridMainColour.rgb, f);
				return float4(lerp(_GridMainColour, _GridSecondaryColour, smoothstep(0,0.75, g.x/g.y-g.x*g.y-.125)).rgb, f);
		    }
			else return float4(_GridSecondaryColour.rgb, f);
		}
 
    ENDCG
 
    }
 
  }
 
}
