// Data shared by all line and curves:
float time;
float4 lineColor;
float blurThreshold = 0.95;

struct PS_Output
{
	float4 Color : COLOR;
	float Depth : DEPTH;
};


//NOTE: THIS STRUCTURE MUST MATCH THE OUTPUT OF THE VERTEX SHADER EXACTLY WHEN USING MONOGAME
struct LINE_PS_INPUT
{
    float4 position : POSITION;
    float3 polar : TEXCOORD0;
    float2 posModelSpace : TEXCOORD1;
    float2 tex : TEXCOORD2;
};

uniform const texture Texture;

uniform const sampler TextureSampler : register(s1) = sampler_state
{
	Texture = (Texture);
	MipFilter = LINEAR;
	MinFilter = LINEAR;
	MagFilter = LINEAR;
	AddressU = CLAMP;
	AddressV = CLAMP;
	AddressW = CLAMP;
}; 

float4 MyPSStandard(LINE_PS_INPUT input) : COLOR0
{
	float4 finalColor;
	finalColor.rgb = lineColor.rgb;
    finalColor.a = lineColor.a * BlurEdge(input.polar.x, blurThreshold);
	return finalColor; 
}

float4 MyPSAlphaGradient(LINE_PS_INPUT input) : COLOR0
{
	float4 finalColor;
	
	finalColor.rgb = lineColor.rgb;
	//finalColor.a = lineColor.a * polar.z * BlurEdge( polar.x );

    finalColor.a = lineColor.a * ((input.polar.z * 2.0) > 1.0 ? ((1.0 - input.polar.z) * 2.0) : (input.polar.z * 2.0)) * BlurEdge(input.polar.x, blurThreshold);

	return finalColor;
}

PS_Output MyPSAlphaDepthGradient(LINE_PS_INPUT input)
{
	PS_Output output; 
    float3 polar = input.polar;

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

PS_Output MyPSAnimatedBidirectional(LINE_PS_INPUT input)
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
    finalColor.a = lineColor.a * BlurEdge(input.polar.x, blurThreshold) * modulation + 0.5;
	clip(finalColor.a);

	output.Color = finalColor;
    float depth = (input.polar.z * 2) > 1 ? 1 - ((1 - input.polar.z) * 2) : 1 - (input.polar.z * 2);
	output.Depth = 0;//(polar.z * 2) > 1 ? 1-((1-polar.z)*2) : 1-(polar.z * 2);
	output.Color.a = (1 - depth) * finalColor.a;
	clip(output.Color.a);

	return output;
}

PS_Output MyPSAnimatedLinear(LINE_PS_INPUT input)
{
	PS_Output output;
	float4 finalColor;
	float bandWidth = 100;
	float Hz = 2;
	float offset = (time * Hz);

	//offset += cos(abs(posModelSpace.y) / lineRadius) * 1.5; //Adds chevron arrow effect
    offset -= (abs(input.posModelSpace.y) / lineRadius) / 1.75; //Adds chevron arrow effect
    float modulation = sin(((-input.posModelSpace.x / bandWidth) + offset) * 3.14159);
	clip(modulation <= 0 ? -1 : 1); //Adds sharp boundary to arrows

	finalColor.rgb = lineColor.rgb;
    finalColor.a = lineColor.a * BlurEdge(input.polar.x, blurThreshold) * modulation;

	output.Color = finalColor;
    float depth = (input.polar.z * 2) > 1 ? 1 - ((1 - input.polar.z) * 2) : 1 - (input.polar.z * 2);
	output.Depth = 0; //depth;

					  //output.Color.a = lineColor.a * (1-depth) * modulation * (1-polar.x);  //This version stops animation at line origin
    output.Color.a = lineColor.a * modulation * (1 - input.polar.x);
	return output;
}


float4 MyPSAnimatedRadial(LINE_PS_INPUT input) : COLOR0
{
	float4 finalColor;
    float modulation = sin((-input.polar.x * 0.1 + time * 0.05) * 20 * 3.14159) * 0.5 + 0.5;
	finalColor.rgb = lineColor.rgb * modulation;
    finalColor.a = lineColor.a * BlurEdge(input.polar.x, blurThreshold);
	return finalColor;
}

float4 MyPSLadder(LINE_PS_INPUT input) : COLOR0
{
	float bandWidth = 1.5;
	float4 finalColor;
	finalColor.rgb = lineColor.rgb;

    float rho = input.polar.x;

    float modulation = sin(((-input.posModelSpace.x / bandWidth)) * 3.14159);
	clip(modulation <= 0 ? -1 : 1); //Adds sharp boundary to arrows
	  
	finalColor.a = lineColor.a * modulation;

	return finalColor;
}

float4 MyPSModern(LINE_PS_INPUT input) : COLOR0
{
	float4 finalColor;
	finalColor.rgb = lineColor.rgb;

    float rho = input.polar.x;

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


float4 MyPSTubular(LINE_PS_INPUT input) : COLOR0
{
	float4 finalColor = lineColor;
    finalColor.a *= input.polar.x;
    finalColor.a = finalColor.a * BlurEdge(input.polar.x, blurThreshold);
	return finalColor;
}

PS_Output MyPSHalfTubular(LINE_PS_INPUT input)
{
	PS_Output output;
	float4 finalColor = lineColor;
	//finalColor.a *= polar.x;

	//We need a signed distance from the midline, where one side is positive and the other is negative, so we use
	//the angle
	int NumRotations = input.polar.y / TAU; //Number of times we have rotated around the circle
    input.polar.y += -NumRotations * TAU;
    float line_side = input.polar.y > PI ? 1 : -1; //Draw the concave side of the line if the points are placed counter-clockwise
	clip(line_side > 0 ? -1 : 1);

	//float polarized_distance = 
	

	finalColor.a *= 1.0 - input.polar.x;
	/*

	int NumRotations = polar.y / TAU; //Number of times we have rotated around the circle
	polar.y += -NumRotations * TAU;

	clip((polar.y >= 0  && polar.y < HALF_PI * 3.0f) ||
		 (polar.y > HALF_PI * 3.0f) ? 1 : -1);


			
	float blurAngle = abs(polar.y - (HALF_PI * 3.0f)); //Remove the half of the circle we render normally
	float AngleAlpha = blurAngle >= HALF_PI ? 1.0f : blurAngle / HALF_PI;
	AngleAlpha = AngleAlpha < 0 ? 0 : AngleAlpha;
	AngleAlpha *= AngleAlpha;

	finalColor.a = finalColor.a * AngleAlpha; // * BlurEdge(polar.x, blurThreshold)
	*/
	output.Color = finalColor;
    output.Depth = input.polar.x;
	return output;
}


float4 MyPSGlow(LINE_PS_INPUT input) : COLOR0
{
	float4 finalColor = lineColor;
    finalColor.a *= 1 - input.polar.x;
	return finalColor;
}

float4 MyPSTextured(LINE_PS_INPUT input) : COLOR0
{
	float4 foregroundColor = tex2D(TextureSampler, input.tex.xy);
	clip(foregroundColor.a <= 0 ? -1 : 1);
	return foregroundColor;
}
