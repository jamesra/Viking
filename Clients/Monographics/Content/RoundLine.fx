// RoundLine.fx
// By Michael D. Anderson
// Version 3.00, Mar 12 2009
//
// Note that there is a (rho, theta) pair, used in the VS, that tells how to 
// scale and rotate the entire line.  There is also a different (rho, theta) 
// pair, used within the PS, that indicates what part of the line each pixel 
// is on.

#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0
#define PS_SHADERMODEL ps_4_0
#endif

#include "../../MonogameXNAGraphicsShared/Content/LineCurveCommon.fx"
#include "../../MonogameXNAGraphicsShared/Content/LineVertexShader.fx"
#include "../../MonogameXNAGraphicsShared/Content/LineCurvePixelShaders.fx"
 
technique Standard
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		StencilEnable = true;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	} 
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZFunc = LessEqual;
		ZEnable = true;
		StencilFunc = LessEqual;
		StencilEnable = true;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSStandard();
	}
}

technique AlphaGradient
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAlphaGradient();
	}
}


technique NoBlur
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSNoBlur();
	}
}


technique AnimatedLinear
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedLinear();
	}
}

technique AnimatedBidirectional
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedBidirectional();
	}
}


technique AnimatedRadial
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedRadial();
	}
}


technique Ladder
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSLadder();
	}
}

technique Dashed
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSDashed();
	}
}


technique Tubular
{
	
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSTubular();
	}
}


technique HalfTube
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSHalfTubular();
	}
}


technique Glow
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSGlow();
	}
}


technique Textured
{
	pass ZWrite
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = Zero;
		DestBlend = One;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL DepthOnlyShader();
	}
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		StencilFunc = Equal;
		vertexShader = compile VS_SHADERMODEL LineVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSTextured();
	}
}

