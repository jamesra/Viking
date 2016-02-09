// Data shared by all line and curves:
matrix viewProj;
float time;
float lineRadius;
float4 lineColor;
float blurThreshold = 0.95;
float texture_x_min = 0;
float texture_x_max = 1;

struct VS_OUTPUT
{
	float4 position : POSITION;
	float3 polar : TEXCOORD0;
	float2 posModelSpace : TEXCOORD1;
	float2 tex   : TEXCOORD2;
};

struct PS_Output
{
	float4 Color : COLOR;
	float Depth : DEPTH;
};

uniform const texture Texture;

uniform const sampler TextureSampler : register(s1) = sampler_state
{
	Texture = (Texture);
	MipFilter = LINEAR;
	MinFilter = LINEAR;
	MagFilter = POINT;
};

//Use linear interpolation to convert a value from zero to one to fall within min-max
float ClampToRange(float scalar, float min, float max)
{
	return (scalar * (max - min)) + min;
}

// Helper function used by several pixel shaders to blur the line edges
//rho in this context is not line length, it is distance from the line center
float BlurEdge(float rho)
{
	if (rho < blurThreshold)
	{
		return 1.0f;
	}
	else
	{
		float normrho = (rho - blurThreshold) * 1 / (1 - blurThreshold);
		return 1 - normrho;
	}
}


float4 MyPSStandard(float3 polar : TEXCOORD0) : COLOR0
{
	float4 finalColor;
finalColor.rgb = lineColor.rgb;
finalColor.a = lineColor.a * BlurEdge(polar.x);
return finalColor;
}

float4 MyPSAlphaGradient(float3 polar : TEXCOORD0) : COLOR0
{
	float4 finalColor;
finalColor.r = polar.x;
finalColor.rgb = lineColor.rgb;
//finalColor.a = lineColor.a * polar.z * BlurEdge( polar.x );

finalColor.a = lineColor.a *  ((polar.z * 2) > 1 ? ((1 - polar.z) * 2) : (polar.z * 2)) * BlurEdge(polar.x);

return finalColor;

}

PS_Output MyPSAlphaDepthGradient(float3 polar : TEXCOORD0)
{
	PS_Output output;

	float4 finalColor;
	finalColor.r = (polar.z * 2) > 1 ? ((1 - polar.z) * 2) : (polar.z * 2);
	finalColor.gb = lineColor.gb;
	//finalColor.gb = ;

	//finalColor.a = lineColor.a * polar.z * BlurEdge( polar.x );
	finalColor.a = 1;

	output.Color = finalColor;
	output.Depth = (polar.z * 2) > 1 ? 1 - ((1 - polar.z) * 2) : 1 - (polar.z * 2);
	output.Color.a = 1 - output.Depth;
	return output;
}

float4 MyPSNoBlur() : COLOR0
{
	float4 finalColor = lineColor;
	return finalColor;
}

PS_Output MyPSAnimatedBidirectional(float3 polar : TEXCOORD0, float2 posModelSpace : TEXCOORD1)
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
	finalColor.a = lineColor.a * BlurEdge(polar.x) * modulation + 0.5;
	output.Color = finalColor;
	float depth = (polar.z * 2) > 1 ? 1 - ((1 - polar.z) * 2) : 1 - (polar.z * 2);
	output.Depth = 0;//(polar.z * 2) > 1 ? 1-((1-polar.z)*2) : 1-(polar.z * 2);
	output.Color.a = 1 - depth;

	return output;
}

PS_Output MyPSAnimatedLinear(float3 polar : TEXCOORD0, float2 posModelSpace : TEXCOORD1)
{
	PS_Output output;
	float4 finalColor;
	float bandWidth = 100;
	float Hz = 2;
	float offset = (time * Hz);

	//offset += cos(abs(posModelSpace.y) / lineRadius) * 1.5; //Adds chevron arrow effect
	offset -= (abs(posModelSpace.y) / lineRadius) / 1.75; //Adds chevron arrow effect
	float modulation = sin(((-posModelSpace.x / bandWidth) + offset) * 3.14159);
	clip(modulation <= 0 ? -1 : 1); //Adds sharp boundary to arrows

	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * BlurEdge(polar.x) * modulation;

	output.Color = finalColor;
	float depth = (polar.z * 2) > 1 ? 1 - ((1 - polar.z) * 2) : 1 - (polar.z * 2);
	output.Depth = 0; //depth;

					  //output.Color.a = lineColor.a * (1-depth) * modulation * (1-polar.x);  //This version stops animation at line origin
	output.Color.a = lineColor.a * modulation *(1 - polar.x);
	return output;
}


float4 MyPSAnimatedRadial(float3 polar : TEXCOORD0) : COLOR0
{
	float4 finalColor;
float modulation = sin((-polar.x * 0.1 + time * 0.05) * 20 * 3.14159) * 0.5 + 0.5;
finalColor.rgb = lineColor.rgb * modulation;
finalColor.a = lineColor.a * BlurEdge(polar.x);
return finalColor;
}


float4 MyPSModern(float3 polar : TEXCOORD0) : COLOR0
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


float4 MyPSTubular(float3 polar : TEXCOORD0) : COLOR0
{
	float4 finalColor = lineColor;
	finalColor.a *= polar.x;
	finalColor.a = finalColor.a * BlurEdge(polar.x);
	return finalColor;
}


float4 MyPSGlow(float3 polar : TEXCOORD0) : COLOR0
{
	float4 finalColor = lineColor;
	finalColor.a *= 1 - polar.x;
	return finalColor;
}

float4 MyPSTextured(float3 tex : TEXCOORD2) : COLOR0
{
	return tex2D(TextureSampler, tex);
}
