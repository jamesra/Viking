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
#include "../../MonogameXNAGraphicsShared/Content/LineCurvePixelShaders.fx"

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
		pixelShader = compile PS_SHADERMODEL MyPSStandard();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAlphaGradient();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSNoBlur();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedLinear();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedBidirectional();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSAnimatedRadial();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSLadder();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSDashed();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSModern();
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
		BlendOp = Add;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSTubular();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSHalfTubular();
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
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSGlow();
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
		StencilEnable = true;
		StencilFunc = GreaterEqual;
		vertexShader = compile VS_SHADERMODEL CurveVertexShader();
		pixelShader = compile PS_SHADERMODEL MyPSTextured();
	}
}

