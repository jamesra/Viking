// Data shared by all line and curves:

float time;
float4 lineColor;
float blurThreshold = 0.95;
float dashLength = 1.5;

#include "LineCurvePixelShaderShared.fx"




Color_Depth_Output MyPSStandard(float3 polar : TEXCOORD0) 
{
	Color_Depth_Output output;
	float4 finalColor;
	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * BlurEdge(polar.x, blurThreshold);

	output.Color = finalColor;
	output.Depth = polar.x;
	return output;
}

Color_Depth_Output MyPSAlphaGradient(float3 polar : TEXCOORD0) 
{
	Color_Depth_Output output;
	float4 finalColor;
	finalColor.rgb = lineColor.rgb;
	
	finalColor.a = lineColor.a *  ((polar.z * 2) > 1 ? ((1 - polar.z) * 2) : (polar.z * 2)) * BlurEdge(polar.x, blurThreshold);

	output.Color = finalColor;
	output.Depth = polar.x;
	return output;
}

Color_Depth_Output MyPSAlphaDepthGradient(float3 polar : TEXCOORD0)
{
	Color_Depth_Output output;

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

Color_Depth_Output MyPSNoBlur(float3 polar : TEXCOORD0)
{
	Color_Depth_Output output;
	output.Color = lineColor;
	output.Depth = polar.x;
	return output;
}

Color_Depth_Output MyPSAnimatedBidirectional(float3 polar : TEXCOORD0, float2 posModelSpace : TEXCOORD1)
{
	Color_Depth_Output output;
	float4 finalColor;
	float bandWidth = 100;
	float Hz = 1;
	float offset = (time * Hz);

	//	float modulation = sin( ( posModelSpace.x * 0.1 + time * 0.05 ) * 80 * 3.14159) * 0.5 + 0.5;
	//	float modulation = sin( ( posModelSpace.x * 100 + (time) ) * 80 * 3.14159) * 0.5 + 0.5;
	float modulation = sin(offset * 3.14159) / 2;
	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * BlurEdge(polar.x, blurThreshold) * modulation + 0.5;
	clip(finalColor.a);

	output.Color = finalColor;
	float depth = (polar.z * 2) > 1 ? 1 - ((1 - polar.z) * 2) : 1 - (polar.z * 2);
	output.Depth = polar.x;//(polar.z * 2) > 1 ? 1-((1-polar.z)*2) : 1-(polar.z * 2);
	output.Color.a = (1 - depth) * finalColor.a;
	clip(output.Color.a);

	return output;
}

Color_Depth_Output MyPSAnimatedLinear(float3 polar : TEXCOORD0, float2 posModelSpace : TEXCOORD1)
{
	Color_Depth_Output output;
	float4 finalColor;
	float bandWidth = 100;
	float Hz = 2;
	float offset = (time * Hz);

	//offset += cos(abs(posModelSpace.y) / lineRadius) * 1.5; //Adds chevron arrow effect
	offset -= (abs(posModelSpace.y) / lineRadius) / 1.75; //Adds chevron arrow effect
	float modulation = sin(((-posModelSpace.x / bandWidth) + offset) * 3.14159);
	clip(modulation <= 0 ? -1 : 1); //Adds sharp boundary to arrows

	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * BlurEdge(polar.x, blurThreshold) * modulation;

	output.Color = finalColor;
	float depth = (polar.z * 2) > 1 ? 1 - ((1 - polar.z) * 2) : 1 - (polar.z * 2);
	output.Depth = polar.x; //depth;

					  //output.Color.a = lineColor.a * (1-depth) * modulation * (1-polar.x);  //This version stops animation at line origin
	output.Color.a = lineColor.a * modulation *(1 - polar.x);
	return output;
}


Color_Depth_Output MyPSAnimatedRadial(float3 polar : TEXCOORD0)
{
	Color_Depth_Output output;
	float4 finalColor;
	float modulation = sin((-polar.x * 0.1 + time * 0.05) * 20 * 3.14159) * 0.5 + 0.5;
	finalColor.rgb = lineColor.rgb * modulation;
	finalColor.a = lineColor.a * BlurEdge(polar.x, blurThreshold);
	output.Color = finalColor;
	output.Depth = polar.x;
	return output;
}

Color_Depth_Output MyPSLadder(float3 polar : TEXCOORD0, float2 posModelSpace : TEXCOORD1)
{
	Color_Depth_Output output;
	float4 finalColor;

	float rho = polar.x;

	float modulation = sin(((-posModelSpace.x / dashLength)) * 3.14159);
	clip(modulation <= 0 ? -1 : 1); //Adds sharp boundary to arrows

	finalColor.rgb = lineColor.rgb;
	finalColor.a = lineColor.a * modulation;
	output.Color = finalColor;
	output.Depth = polar.x;
	return output;
}

/*Dashed doesn't work as expected because each line is independent.  We have no way of measuring length along a polyline*/
Color_Depth_Output MyPSDashed(float3 polar : TEXCOORD0, float2 posModelSpace : TEXCOORD1)
{
	Color_Depth_Output output; 
	float4 finalColor; 

	float rho = polar.x;

	float modulation = sin(((-posModelSpace.x / dashLength)) * 3.14159);
	clip(modulation <= 0 ? -1 : 1); //Adds sharp boundary to arrows
	 
	output.Color = lineColor;
	output.Depth = polar.x;
	return output;
}


Color_Depth_Output MyPSModern(float3 polar : TEXCOORD0)
{
	Color_Depth_Output output;
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

	output.Color = finalColor;
	output.Depth = polar.x;
	return output;
}


Color_Depth_Output MyPSTubular(float3 polar : TEXCOORD0) 
{
	Color_Depth_Output output;
	float4 finalColor = lineColor;
	finalColor.a *= polar.x;
	finalColor.a = finalColor.a * BlurEdge(polar.x, blurThreshold);
	output.Color = finalColor;
	output.Depth = polar.x;
	return output;
}

Color_Depth_Output MyPSHalfTubular(float3 polar : TEXCOORD0)
{
	Color_Depth_Output output;
	float4 finalColor = lineColor;
	//finalColor.a *= polar.x;

	//We need a signed distance from the midline, where one side is positive and the other is negative, so we use
	//the angle
	int NumRotations = polar.y / TAU; //Number of times we have rotated around the circle
	polar.y += -NumRotations * TAU;
	float line_side = polar.y > PI ? 1 : -1; //Draw the concave side of the line if the points are placed counter-clockwise
	clip(line_side > 0 ? -1 : 1);

	//float polarized_distance = 
	

	finalColor.a *= 1.0 - polar.x;
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
	output.Depth = polar.x;
	return output;
}


Color_Depth_Output MyPSGlow(float3 polar : TEXCOORD0)
{
	Color_Depth_Output output;
	output.Color = lineColor;
	output.Color.a *= 1 - polar.x;
	output.Depth = polar.x;
	return output;
}

Color_Depth_Output MyPSTextured(float3 polar : TEXCOORD0, float3 tex : TEXCOORD2)
{
	Color_Depth_Output output;
	output.Color = tex2D(ForegroundTextureSampler, tex);
	clip(output.Color.a <= 0 ? -1 : 1);
	output.Depth = polar.x;
	return output;
}
