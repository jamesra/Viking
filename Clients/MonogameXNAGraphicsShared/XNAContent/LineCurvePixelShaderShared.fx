//NOTE: THIS STRUCTURE MUST MATCH THE OUTPUT OF THE VERTEX SHADER EXACTLY WHEN USING MONOGAME
struct LINE_PS_INPUT
{
	float2 ScreenTexCoord : SV_Position;
	float3 polar : TEXCOORD0;
	float2 posModelSpace: TEXCOORD1;
	float2 tex   : TEXCOORD2;
};

struct Color_Depth_Output
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

/*Intended to only write to the Z-buffer so connected lines do not overlap in an ugly way*/
Color_Depth_Output DepthOnlyShader(LINE_PS_INPUT input)
{
	Color_Depth_Output output;
	output.Color.a = 0;
	output.Color.rgb = float3(0, 0, 0);
	output.Depth = input.polar.x;
	return output;
}