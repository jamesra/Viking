// RoundLine.fx
// By Michael D. Anderson
// Version 3.00, Mar 12 2009
//
// Note that there is a (rho, theta) pair, used in the VS, that tells how to 
// scale and rotate the entire line.  There is also a different (rho, theta) 
// pair, used within the PS, that indicates what part of the line each pixel 
// is on.


#include "LinePixelShaders.fx"

// Per-line instance data:
float4 instanceData[200]; // (x0, y0, rho, theta) 

 
struct VS_INPUT
{
	float4 pos : POSITION;
	float2 vertRhoTheta : NORMAL;
	float2 vertScaleTrans : TEXCOORD0;
	float instanceIndex : TEXCOORD1;
};
 
VS_OUTPUT MyVS( VS_INPUT In )
{
	VS_OUTPUT Out = (VS_OUTPUT)0;
	float4 pos = In.pos;

	float x0 = instanceData[In.instanceIndex].x;
	float y0 = instanceData[In.instanceIndex].y;
	float rho = instanceData[In.instanceIndex].z;
	float theta = instanceData[In.instanceIndex].w;

	// Scale X by lineRadius, and translate X by rho, in worldspace
	// based on what part of the line we're on
	float vertScale = In.vertScaleTrans.x;
	float vertTrans = In.vertScaleTrans.y;
	pos.x *= (vertScale * lineRadius);
	pos.x += (vertTrans * rho);

	// Always scale Y by lineRadius regardless of what part of the line we're on
	pos.y *= lineRadius;
	
	// Now the vertex is adjusted for the line length and radius, and is 
	// ready for the usual world/view/projection transformation.

	// World matrix is rotate(theta) * translate(p0)
	matrix worldMatrix = 
	{
		cos(theta), sin(theta), 0, 0,
		-sin(theta), cos(theta), 0, 0,
		0, 0, 1, 0,
		x0, y0, 0, 1 
	};
	
	Out.position = mul(mul(pos, worldMatrix), viewProj);
	
	Out.polar = float3(In.vertRhoTheta, vertTrans);

	Out.tex = float2(ClampToRange(In.vertScaleTrans.y,texture_x_min, texture_x_max),
					 (-In.pos[1] + 1) / 2.0);
	
	Out.posModelSpace.xy = pos.xy;

	return Out;
}




technique Standard
{
	pass P0
	{
		CullMode = CW;
		AlphaBlendEnable = true;
		SrcBlend = SrcAlpha;
		DestBlend = InvSrcAlpha;
		BlendOp = Add;
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSStandard();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSAlphaGradient();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSNoBlur();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSAnimatedLinear();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSAnimatedBidirectional();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSAnimatedRadial();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSModern();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSTubular();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSGlow();
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
		vertexShader = compile vs_1_1 MyVS();
		pixelShader = compile ps_2_0 MyPSTextured();
	}
}
