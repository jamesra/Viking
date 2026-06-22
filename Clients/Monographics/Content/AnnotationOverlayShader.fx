#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0
#define PS_SHADERMODEL ps_4_0
#endif

#include "../../MonogameXNAGraphicsShared/Content/HSLRGBLib.fx"
#include "../../MonogameXNAGraphicsShared/Content/OverlayShaderShared.fx"

uniform const float Radius; 

static const float radiusSquared = 0.5*0.5;

static const float borderStartRadius = 0.475; 
static const float borderStartSquared = 0.475 * 0.475;

static const float borderBlendStartRadius = 0.45;
static const float borderBlendStartSquared = 0.45 * 0.45;

//The convention for annotation textures is that they built from two 8-bit images, one image is loaded to the RGB coordinates of the texture.
//The other image is loaded into the alpha channel.
//The verticies contain an RGB color which is converted to HSL space. 

//The alpha channel of the texture indicates whether the pixel is part of the annotation or not.  The alpha value is only used for this purpose
//The RGB component of the texture indicates the saturation value of the pixel.
//The program determines Saturation via converting the RGB color attribute of the vertex.
//The program determines the hue via converting the RGB color attribute of the vertex.
//The alpha channel of vertex color indicates how much the texture value is blended with the background value.

struct VertexShaderInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
    float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
#if OPENGL
    float4 PositionCopy : TEXCOORD2;
#endif
};

struct CircleVertexShaderOutput
{
    float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 CenterDistance : TEXCOORD0;
#if OPENGL
    float4 PositionCopy : TEXCOORD1;
#endif
};

struct PixelShaderInput
{
#if OPENGL
    float4 Position : TEXCOORD2;
#else
    float4 Position : SV_Position;
#endif
	float4 HSLColor : COLOR0;
    float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
};

struct CirclePixelShaderInput
{
#if OPENGL
    float4 Position : TEXCOORD1;
#else
    float4 Position : SV_Position;
#endif
	float4 HSLColor : COLOR0;
	float2 CenterDistance : TEXCOORD0;
};

struct PixelShaderOutput
{
	float4 Color : COLOR0;
	float Depth : DEPTH0; 
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.TexCoord = input.TexCoord;
    output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = input.Color; //RGBToHCL(input.Color);
	output.CenterDistance = input.TexCoord.xy - 0.5;
#if OPENGL
	output.PositionCopy = output.Position;
#endif
    return output;
}

CircleVertexShaderOutput CircleVertexShaderFunction(VertexShaderInput input)
{
    CircleVertexShaderOutput output;
    output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = input.Color;  //output.HSLColor = RGBToHCL(input.Color);
	output.CenterDistance = input.TexCoord.xy - 0.5;
#if OPENGL
	output.PositionCopy = output.Position;
#endif
    return output;
}

float CenterDistanceSquared(float2 CenterDistance)
{
	float XDist = CenterDistance.x;
	float YDist = CenterDistance.y;
	return (XDist * XDist) + (YDist * YDist);
}



PixelShaderOutput RGBATextureOverBackgroundLumaPixelShaderFunction(PixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
    PixelShaderOutput output;
    output.Depth = CenterDistanceSquared(input.CenterDistance);
    
	float2 ScreenTexCoord = input.Position.xy / input.Position.w;

    float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord);
    //This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
    clip(RGBColor.a <= 0.0 ? -1.0 : 1.0);
    
	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((ScreenTexCoord.xy) / (RenderTargetSize.xy)));
    output.Color = BlendHCLColorOverBackground(input.HSLColor, RGBBackgroundColor, 1.0f - RGBColor.a);
    output.Color.a = RGBColor.r * input.HSLColor.a;

    return output;
}

PixelShaderOutput RGBCircleOverBackgroundLumaPixelShaderFunction(CirclePixelShaderInput input)
{
	//float OverlayLuma = mul(LumaWeights, ); 

	PixelShaderOutput output; 
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance);

    float2 ScreenTexCoord = input.Position.xy / input.Position.w;


	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle
	output.Depth = CenterDistSquared;

	float alphaBlend = 0;
	//float alphaMax = 0.33;
	float alphaMax = InputLumaAlpha;

	if(CenterDistSquared >= borderStartSquared)
		alphaBlend = alphaMax;
	else if(CenterDistSquared >= borderBlendStartSquared)
	{
		alphaBlend = (sqrt(CenterDistSquared) - borderBlendStartRadius) / (borderStartRadius - borderBlendStartRadius) * alphaMax;
	}

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((ScreenTexCoord.xy) / RenderTargetSize.xy));
	output.Color = BlendHCLColorOverBackground(input.HSLColor, RGBBackgroundColor, alphaBlend);
	output.Color.a = input.HSLColor.a;
    return output;
}

technique RGBTextureOverBackgroundValueOverlayEffect
{
    pass
    {
		VertexShader = compile VS_SHADERMODEL VertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL RGBATextureOverBackgroundLumaPixelShaderFunction();
    }

}

technique RGBCircleOverBackgroundValueOverlayEffect
{
    pass
    {
		VertexShader = compile VS_SHADERMODEL CircleVertexShaderFunction();
        PixelShader = compile PS_SHADERMODEL RGBCircleOverBackgroundLumaPixelShaderFunction();
    }
}

