// RoundCurve.fx
// By James R. Anderson
// Version 1.00, Sep 18 2015
//
// Based on RoundLine by Michael Anderson
//

// This shader draws one polyline at a time.
// Each control point occupies an entry in the control point array

#include "LinePixelShaders.fx"

float curveTotalLength; //Total length of the curve
// Per-curve instance data:
float4 CurveSegmentData[200]; // (x0, y0, normalized_distance_to_origin, theta (tangent to curve))
 
struct VS_INPUT
{
	float4 pos : POSITION;
	float2 vertRhoTheta : NORMAL;
	float curvesegmentIndex : TEXCOORD0;
};

VS_OUTPUT MyVS( VS_INPUT In )
{
	VS_OUTPUT Out = (VS_OUTPUT)0;
	float4 pos = In.pos; //Position on the line, either along the center or the edge
	
	float x0 = CurveSegmentData[In.curvesegmentIndex].x; //Position of the control point in world space
	float y0 = CurveSegmentData[In.curvesegmentIndex].y;
	float distanceToOriginNormalized = CurveSegmentData[In.curvesegmentIndex].z; //Distance to the origin of the polyline in world space
	float tangent_theta = CurveSegmentData[In.curvesegmentIndex].w; //Tangent to the polyline at this control point
	float theta = tangent_theta;// +(3.14159 / 2.0); //Adjust the tangent 90 degrees so we rotate our verticies a lineradius distance away from the control point
	float vert_distance_from_center_normalized = In.vertRhoTheta.x;
	// Scale X by lineRadius, and translate X by rho, in worldspace
	// based on what part of the line we're on
	
	// Always scale Y by lineRadius regardless of what part of the line we're on
	pos.y *= lineRadius;

	// World matrix is rotate(theta) * translate(p0)
	matrix worldMatrix =
	{
		cos(theta), sin(theta), 0, 0,
		-sin(theta), cos(theta), 0, 0,
		0, 0, 1, 0,
		x0, y0, 0, 1
	};

	Out.position = mul(mul(pos, worldMatrix), viewProj);
		 
	Out.polar = float3(In.vertRhoTheta, distanceToOriginNormalized);

	Out.posModelSpace.xy = float2(curveTotalLength * distanceToOriginNormalized, pos.y);

	Out.tex = float2(ClampToRange(distanceToOriginNormalized, texture_x_min, texture_x_max),
				    (-In.pos.y + 1) / 2.0);

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
