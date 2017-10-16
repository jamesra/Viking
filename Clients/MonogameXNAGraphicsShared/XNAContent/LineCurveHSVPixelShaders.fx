// Data shared by all line and curves:

#include "HSLRGBLib.fx"

float time;
float4 lineColor;
float blurThreshold = 0.95;

struct PS_Input
{
	float2 ScreenTexCoord : SV_Position;
	float3 polar : TEXCOORD0;
	float2 posModelSpace: TEXCOORD1;
	float2 tex   : TEXCOORD2;
};

struct PS_Output
{
	float4 Color : COLOR;
	float Depth : DEPTH;
};

uniform const texture Texture;

uniform const sampler ForegroundTextureSampler : register(s1) = sampler_state
{
	Texture = (Texture);
	MipFilter = LINEAR;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
	AddressW = CLAMP;
};


uniform const float2 RenderTargetSize;

uniform const texture BackgroundTexture;

uniform const sampler BackgroundTextureSampler : register(s0) = sampler_state
{
	Texture = (BackgroundTexture);
	MipFilter = POINT;
	MinFilter = POINT;
	MagFilter = POINT;
};


float4 MyPSStandardHSV(PS_Input input) : COLOR0
{
	float4 finalColor;
	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * BlurEdge(input.polar.x, blurThreshold);

	clip(finalColor.a);
	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	finalColor =  BlendHSLColorOverBackground(lineColor, RGBBackgroundColor, 1.0f - lineColor.a);
    finalColor.a = lineColor.a; 
    return finalColor;
}

float4 MyPSAlphaGradientHSV(PS_Input input) : COLOR0
{
	float4 finalColor;
	float3 polar = input.polar;

	finalColor.rgb = lineColor.rgb;
	//finalColor.a = lineColor.a * polar.z * BlurEdge( polar.x );
	finalColor.a = lineColor.a *  ((polar.z * 2) > 1 ? ((1 - polar.z) * 2) : (polar.z * 2)) * BlurEdge(polar.x, blurThreshold);

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	return BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, finalColor.a);
}

PS_Output MyPSAlphaDepthGradientHSV(PS_Input input)
{
	PS_Output output;
	float3 polar = input.polar;

	float4 finalColor;
	finalColor.r = (polar.z * 2) > 1 ? ((1 - polar.z) * 2) : (polar.z * 2);
	finalColor.gb = lineColor.gb;
	finalColor.a = 1;

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	finalColor = BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, finalColor.a);

	output.Color = finalColor;
	output.Depth = (polar.z * 2) > 1 ? 1 - ((1 - polar.z) * 2) : 1 - (polar.z * 2);
	output.Color.a = 1 - output.Depth;
	return output;
}

float4 MyPSNoBlurHSV() : COLOR0
{
	float4 finalColor = lineColor;
	return finalColor;
}

PS_Output MyPSAnimatedBidirectionalHSV(PS_Input input)
{
	PS_Output output;
	float4 finalColor;
	float bandWidth = 100;
	float Hz = 1;
	float offset = (time * Hz);

	//	float modulation = sin( ( posModelSpace.x * 0.1 + time * 0.05 ) * 80 * 3.14159) * 0.5 + 0.5;
	//	float modulation = sin( ( posModelSpace.x * 100 + (time) ) * 80 * 3.14159) * 0.5 + 0.5;

	float modulation = sin(offset * 3.14159) / 2;
	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * BlurEdge(input.polar.x, blurThreshold) * (modulation + 0.5);
    //finalColor.a = lineColor.a * (modulation + 0.5);
	//clip(finalColor.a);
    finalColor.a = clamp(finalColor.a, 0, 1);

	output.Color = finalColor;
	float depth = (input.polar.z * 2) > 1 ? 1 - ((1 - input.polar.z) * 2) : 1 - (input.polar.z * 2);
	output.Depth = 0;//(polar.z * 2) > 1 ? 1-((1-polar.z)*2) : 1-(polar.z * 2);
	finalColor.a = (1 - depth) * finalColor.a;

    clip(finalColor.a);

	float AlphaBlend = lineColor.a * lineColor.r;

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

    //TODO: The gap junctions are not as transparent as they should be, but the code below causes strange flickers from white to black.
	//float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	//output.Color = BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, 1);
	//output.Color.a = (1 - depth) * finalColor.a;

	return output;
}

PS_Output MyPSAnimatedLinearHSV(PS_Input input)
{
	PS_Output output;
	float4 lineColorHSV;
	float bandWidth = 100;
	float Hz = 2;
	float offset = (time * Hz);

	offset -= (abs(input.posModelSpace.y) / lineRadius) / 1.75; //Adds chevron arrow effect
	float modulation = sin(((-input.posModelSpace.x / bandWidth) + offset) * 3.14159);
	clip(modulation <= 0 ? -1 : 1);

	lineColorHSV.rgb = lineColor.rgb;
	lineColorHSV.a = lineColor.a * BlurEdge(input.polar.x, blurThreshold) * modulation;
	clip(lineColorHSV.a);

	output.Color.rgb = lineColorHSV;
	float depth = (input.polar.z * 2) > 1 ? 1 - ((1 - input.polar.z) * 2) : 1 - (input.polar.z * 2);
	output.Depth = 0; //depth;
					  //output.Color.a = lineColorHSV.a * (1 - depth) * modulation *(1 - input.polar.x);
	output.Color.a = lineColorHSV.a * modulation *(1 - input.polar.x);
	clip(output.Color.a);

	float AlphaBlend = lineColorHSV.a;

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	output.Color = BlendHSLColorOverBackground(lineColorHSV, RGBBackgroundColor, AlphaBlend);

	return output;
}


float4 MyPSAnimatedRadialHSV(PS_Input input) : COLOR0
{
	float4 finalColor;
	float3 polar = input.polar;
	float modulation = sin((-polar.x * 0.1 + time * 0.05) * 20 * 3.14159) * 0.5 + 0.5;
	finalColor.rgb = lineColor.rgb * modulation;
	finalColor.a = lineColor.a * BlurEdge(polar.x, blurThreshold);
	clip(finalColor.a);

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	return BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, finalColor.a);
}


float4 MyPSModernHSV(PS_Input input) : COLOR0
{
	float3 polar = input.polar;
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

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	return BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, finalColor.a);
}

float4 MyPSLadderHSV(PS_Input input) : COLOR0
{
	float3 polar = input.polar;

	float bandWidth = 1.5;
	float4 finalColor;
	float4 output;
	finalColor.rgb = lineColor.rgb;

	float rho = polar.x;

	float modulation = sin(((-input.posModelSpace.x / bandWidth)) * 3.14159);
	clip(modulation <= 0 ? -1 : 1); //Adds sharp boundary to arrows

	finalColor.a = lineColor.a * modulation;

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	return BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, finalColor.a);
}


float4 MyPSTubularHSV(PS_Input input) : COLOR0
{
	float4 finalColor = lineColor;
	float3 polar = input.polar;

	finalColor.a *= polar.x;
	finalColor.a = finalColor.a * BlurEdge(polar.x, blurThreshold);

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	return BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, finalColor.a);
}


PS_Output MyPSHalfTubularHSV(PS_Input input)
{
	PS_Output output;
	float4 finalColor = lineColor;
	float3 polar = input.polar;

	int NumRotations = polar.y / TAU; //Number of times we have rotated around the circle
	polar.y += -NumRotations * TAU;
	float line_side = polar.y > PI ? 1 : -1; //Draw the concave side of the line if the points are placed counter-clockwise
	clip(line_side > 0 ? -1 : 1);

	//float polarized_distance = 


	finalColor.a *= 1.0 - polar.x;

	/*
	float line_side = polar.y > PI ? -1 : 1;
	float polarized_distance = polar.x * line_side;

	finalColor.a *= (polarized_distance + 1) / 2;
	*/
	/*
	 
	finalColor.a *= polar.x;
	int NumRotations = polar.y / TAU; //Number of times we have rotated around the circle
	polar.y += -NumRotations * TAU;
	clip((polar.y >= 0 && polar.y < HALF_PI * 3.0f) ||
		(polar.y > HALF_PI * 3.0f) ? 1 : -1);

	float blurAngle = (polar.y - HALF_PI * 3.0f); //Remove the half of the circle we render normally
	float AngleAlpha = abs(blurAngle) > HALF_PI ? 1.0f : abs(blurAngle) / HALF_PI;
	AngleAlpha *= AngleAlpha;

	*/

	//finalColor.a = finalColor.a * BlurEdge(polar.x, blurThreshold) * AngleAlpha;
	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	output.Color = BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, finalColor.a);
	output.Depth = polar.x;
	return output;
}


float4 MyPSGlowHSV(PS_Input input) : COLOR0
{
	float4 finalColor = lineColor;
	float3 polar = input.polar;
	finalColor.a *= 1 - polar.x;

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	return BlendHSLColorOverBackground(finalColor, RGBBackgroundColor, finalColor.a);
}

float4 MyPSTexturedHSV(PS_Input input) : COLOR0
{ 
	float4 foregroundColor = tex2D(ForegroundTextureSampler, input.tex);
	clip(foregroundColor.a <= 0 ? -1 : 1);
	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
	float4 outColor = BlendHSLColorOverBackground(foregroundColor, RGBBackgroundColor, 0);
	outColor = foregroundColor.a;
	return outColor;
}