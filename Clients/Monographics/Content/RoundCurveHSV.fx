// RoundCurve.fx
// By James R. Anderson
// Version 1.00, Sep 18 2015
//
// Based on RoundLine by Michael Anderson
//

#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0
#define PS_SHADERMODEL ps_4_0
#endif

// This shader draws one polyline at a time.
// Each control point occupies an entry in the control point array

#include "../../MonogameXNAGraphicsShared/Content/LineCurveCommon.fx"
#include "../../MonogameXNAGraphicsShared/Content/CurveVertexShader.fx"
#include "../../MonogameXNAGraphicsShared/Content/LineCurveHSVPixelShaders.fx"

technique Standard
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSStandardHSV();
	}
}

technique AlphaGradient
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAlphaGradientHSV();
	}
}


technique NoBlur
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSNoBlurHSV();
	}
}


technique AnimatedLinear
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedLinearHSV();
	}
}

technique AnimatedBidirectional
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedBidirectionalHSV();
	}
}


technique AnimatedRadial
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedRadialHSV();
	}
}


technique Ladder
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSLadderHSV();
	}
}


technique Dashed
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSDashedHSV();
	}
} 


technique Modern
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSModernHSV();
	}
}


technique Tubular
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;/*
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;*/
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSTubularHSV();
	}
}


technique HalfTube
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSHalfTubularHSV();
	}
}


technique Glow
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSGlowHSV();
	}
}


technique Textured
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		ZEnable = true;
		ZFunc = LessEqual;
		ZWriteEnable = true;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSTexturedHSV();
	}
}

