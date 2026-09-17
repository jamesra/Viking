// RoundLine.fx
// By Michael D. Anderson
// Version 3.00, Mar 12 2009
//
// Note that there is a (rho, theta) pair, used in the VS, that tells how to 
// scale and rotate the entire line.  There is also a different (rho, theta) 
// pair, used within the PS, that indicates what part of the line each pixel 
// is on.


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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSStandard();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSAlphaGradient();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSNoBlur();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSAnimatedLinear();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSAnimatedBidirectional();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSAnimatedRadial();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSLadder();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSDashed();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSTubular();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSHalfTubular();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSGlow();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 DepthOnlyShader();
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
		vertexShader = compile vs_4_0 LineVertexShader();
		pixelShader = compile ps_4_0 MyPSTextured();
	}
}










