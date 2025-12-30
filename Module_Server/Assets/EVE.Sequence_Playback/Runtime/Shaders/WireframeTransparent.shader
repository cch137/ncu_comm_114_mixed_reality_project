Shader "SuperSystems/Wireframe-Transparent"
{
	Properties
	{
		_WireThickness ("Wire Thickness", RANGE(0, 800)) = 0
		_WireSmoothness ("Wire Smoothness", RANGE(0, 20)) = 0
		_WireColor ("Wire Color", Color) = (0.6117647, 0.682353, 0.7686275, 1.0)
		_BaseColor ("Base Color", Color) = (0.6117647, 0.682353, 0.7686275, 0.0)
		_MaxTriSize ("Max Tri Size", RANGE(0, 200)) = 200
	}

	SubShader
	{
		Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
			Cull Off

			// Wireframe shader based on the the following
			// http://developer.download.nvidia.com/SDK/10/direct3d/Source/SolidWireframe/Doc/SolidWireframe.pdf

			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "Wireframe.cginc"

			ENDCG
		}
	}
}
