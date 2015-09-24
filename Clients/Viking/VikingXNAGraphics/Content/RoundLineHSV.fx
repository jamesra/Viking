// RoundLine.fx
// By Michael D. Anderson
// Version 3.00, Mar 12 2009
//
// Note that there is a (rho, theta) pair, used in the VS, that tells how to 
// scale and rotate the entire line.  There is also a different (rho, theta) 
// pair, used within the PS, that indicates what part of the line each pixel 
// is on.

#include "HSLRGBLib.fx"

// Data shared by all lines:
matrix viewProj;
float time;
float lineRadius;
float4 lineColor;
float blurThreshold = 0.95;
uniform const float2 RenderTargetSize;

uniform const texture BackgroundTexture;

// Per-line instance data:
float4 instanceData[200]; // (x0, y0, rho, theta)

uniform const sampler BackgroundTextureSampler : register(s0) = sampler_state
{
	Texture = (BackgroundTexture);
	MipFilter = POINT;
	MinFilter = POINT;
	MagFilter = POINT;
};

struct VS_INPUT
{
	float4 pos : POSITION;
	float2 vertRhoTheta : NORMAL;
	float2 vertScaleTrans : TEXCOORD0;
	float instanceIndex : TEXCOORD1;
};


struct VS_OUTPUT
{
	float4 position : POSITION;
	float3 polar : TEXCOORD0;
	float2 posModelSpace : TEXCOORD1;
};

struct PS_Input
{
	float3 polar : TEXCOORD0;
	float2 posModelSpace: TEXCOORD1;
	float2 ScreenTexCoord : SV_Position;
};
  
struct PS_Output
{
	float4 Color : COLOR; 
	float Depth : DEPTH;
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

	Out.posModelSpace.xy = pos.xy;
	 

	return Out;
}


// Helper function used by several pixel shaders to blur the line edges
float BlurEdge( float rho )
{
	if( rho < blurThreshold )
	{
		return 1.0f;
	}
	else
	{
		float normrho = (rho - blurThreshold) * 1 / (1 - blurThreshold);
		return 1 - normrho;
	}
}


float4 MyPSStandard(PS_Input input) : COLOR0
{
	float4 finalColor;
	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * BlurEdge( input.polar.x );

	float AlphaBlend = lineColor.a * lineColor.r;

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

	float4 LumaColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));

	float Hue = lineColor.r;
	float Saturation = lineColor.g;
	float BackgroundLuma = mul(LumaColor, LumaWeights);

	float Luma = (BackgroundLuma * (1 - AlphaBlend)) + ((AlphaBlend * lineColor.b));  //This should be a greyscale image, so any component will match the value

	float4 hsv = { Hue, Saturation, Luma, lineColor.a };
	finalColor.rgb = HCLToRGB(hsv);
	finalColor.a = lineColor.a;

	return finalColor;
}

float4 MyPSAlphaGradient( float3 polar : TEXCOORD0) : COLOR0
{
	float4 finalColor;
	finalColor.r = polar.x; 
	finalColor.rgb = lineColor.rgb;
	//finalColor.a = lineColor.a * polar.z * BlurEdge( polar.x );

	finalColor.a = lineColor.a *  ((polar.z * 2) > 1 ? ((1-polar.z)*2) : (polar.z * 2)) * BlurEdge( polar.x );

	return finalColor;

}

PS_Output MyPSAlphaDepthGradient( float3 polar : TEXCOORD0)
{
	PS_Output output; 

	float4 finalColor;
	finalColor.r = (polar.z * 2) > 1 ? ((1-polar.z)*2) : (polar.z * 2);
	finalColor.gb = lineColor.gb;
	//finalColor.gb = ;
	
	//finalColor.a = lineColor.a * polar.z * BlurEdge( polar.x );
	finalColor.a = 1; 

	output.Color = finalColor; 
	output.Depth = (polar.z * 2) > 1 ? 1-((1-polar.z)*2) : 1-(polar.z * 2);
	output.Color.a = 1-output.Depth;
	return output;
}

float4 MyPSNoBlur() : COLOR0
{
	float4 finalColor = lineColor;
	return finalColor;
}

PS_Output MyPSAnimatedBidirectional(PS_Input input)
{
	PS_Output output; 
	float4 finalColor;
	float bandWidth = 100; 
	float Hz = 1;
	float offset = (time * Hz);
	
//	float modulation = sin( ( posModelSpace.x * 0.1 + time * 0.05 ) * 80 * 3.14159) * 0.5 + 0.5;
//	float modulation = sin( ( posModelSpace.x * 100 + (time) ) * 80 * 3.14159) * 0.5 + 0.5;
	float modulation = sin( offset * 3.14159 ) / 2;   
	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * BlurEdge( input.polar.x ) * modulation + 0.5;
	output.Color = finalColor; 
	float depth = (input.polar.z * 2) > 1 ? 1-((1- input.polar.z)*2) : 1-(input.polar.z * 2);
	output.Depth = 0;//(polar.z * 2) > 1 ? 1-((1-polar.z)*2) : 1-(polar.z * 2);
	

	float AlphaBlend = lineColor.a * lineColor.r;

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

	float4 LumaColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));

	float Hue = lineColor.r;
	float Saturation = lineColor.g;
	float BackgroundLuma = mul(LumaColor, LumaWeights);

	float Luma = (BackgroundLuma * (1 - AlphaBlend)) + ((AlphaBlend * lineColor.b));  //This should be a greyscale image, so any component will match the value

	float4 hsv = { Hue, Saturation, Luma, lineColor.a };
	output.Color.rgb = HCLToRGB(hsv);
	output.Color.a = 1 - depth;

	return output;
}

PS_Output MyPSAnimatedLinear(PS_Input input)
{
	PS_Output output; 
	float4 lineColorHSV;
	float bandWidth = 100; 
	float Hz = 2;
	float offset = (time * Hz);
	 
	float modulation = sin( ( (-input.posModelSpace.x  / bandWidth) + offset) * 3.14159 );
	clip(modulation <= 0 ? -1 : 1);

	lineColorHSV.rgb = lineColor.rgb;
	lineColorHSV.a = lineColor.a * BlurEdge(input.polar.x ) * modulation;
	
	output.Color.rgb = lineColorHSV;
	float depth = (input.polar.z * 2) > 1 ? 1-((1- input.polar.z)*2) : 1-(input.polar.z * 2);
	output.Depth = 0; //depth;
	//output.Color.a = lineColorHSV.a * (1 - depth) * modulation *(1 - input.polar.x);
	output.Color.a = lineColorHSV.a * modulation *(1 - input.polar.x);

	float AlphaBlend = lineColorHSV.a * lineColorHSV.r;

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

	float4 LumaColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));

	float Hue = lineColor.r;
	float Saturation = lineColor.g;
	float BackgroundLuma = mul(LumaColor, LumaWeights);

	float Luma = (BackgroundLuma * (1 - AlphaBlend)) + ((AlphaBlend * lineColor.b));  //This should be a greyscale image, so any component will match the value

	float4 hsv = { Hue, Saturation, Luma, lineColorHSV.a };
	output.Color.rgb = HCLToRGB(hsv);
	//output.Color.a = 1;

	return output;
}


float4 MyPSAnimatedRadial( float3 polar : TEXCOORD0 ) : COLOR0
{
	float4 finalColor;
	float modulation = sin( ( -polar.x * 0.1 + time * 0.05 ) * 20 * 3.14159) * 0.5 + 0.5;
	finalColor.rgb = lineColor.rgb * modulation;
	finalColor.a = lineColor.a * BlurEdge( polar.x );
	return finalColor;
}


float4 MyPSModern( float3 polar : TEXCOORD0 ) : COLOR0
{
	float4 finalColor;
	finalColor.rgb = lineColor.rgb;

	float rho = polar.x;

	float a;
	float blurThreshold = 0.15;
	/*
	if( rho < blurThreshold )
	{
		a = 1.0f;
	}
	else
	{*/
	float normrho = (rho - blurThreshold) * 1 / (1 - blurThreshold);
	a = normrho;
	//}
	
	finalColor.a = lineColor.a * a;

	return finalColor;
}


float4 MyPSTubular( float3 polar : TEXCOORD0 ) : COLOR0
{
	float4 finalColor = lineColor;
	finalColor.a *= polar.x;
	finalColor.a = finalColor.a * BlurEdge( polar.x );
	return finalColor;
}


float4 MyPSGlow( float3 polar : TEXCOORD0 ) : COLOR0
{
	float4 finalColor = lineColor;
	finalColor.a *= 1 - polar.x;
	return finalColor;
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
		vertexShader = compile vs_3_0 MyVS();
		pixelShader = compile ps_3_0 MyPSStandard();
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
		vertexShader = compile vs_3_0 MyVS();
		pixelShader = compile ps_3_0 MyPSAnimatedLinear();
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
		vertexShader = compile vs_3_0 MyVS();
		pixelShader = compile ps_3_0 MyPSAnimatedBidirectional();
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
