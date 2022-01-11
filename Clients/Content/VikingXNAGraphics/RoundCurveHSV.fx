// RoundCurve.fx
// By James R. Anderson
// Version 1.00, Sep 18 2015
//
// Based on RoundLine by Michael Anderson
//

// This shader draws one polyline at a time.
// Each control point occupies an entry in the control point array

#include "LineCurveCommon.fx"
#include "CurveVertexShader.fx"
#include "LineCurveHSVPixelShaders.fx"

technique Standard
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSStandardHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSAlphaGradientHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSNoBlurHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSAnimatedLinearHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSAnimatedBidirectionalHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSAnimatedRadialHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSLadderHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSDashedHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSModernHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSTubularHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSHalfTubularHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSGlowHSV();
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
		vertexShader = compile vs_3_0 CurveVertexShader();
		pixelShader = compile ps_3_0 MyPSTexturedHSV();
	}
}
