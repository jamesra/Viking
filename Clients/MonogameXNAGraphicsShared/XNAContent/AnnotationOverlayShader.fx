#include "HSLRGBLib.fx"

float4x4 mWorldViewProj;

uniform const float2 RenderTargetSize; 

//OK, this should sample a texture
uniform const texture BackgroundTexture;
uniform const texture AnnotationTexture;

uniform const float Radius; 

uniform const float radiusSquared = 0.5*0.5;

uniform const float borderStartRadius = 0.475; 
uniform const float borderStartSquared = 0.475 * 0.475;

uniform const float borderBlendStartRadius = 0.45;
uniform const float borderBlendStartSquared = 0.45 * 0.45;

uniform const float InputLumaAlpha = 1.0f; //Defines how we weight blending the input and existing background luma values by default.

//The convention for annotation textures is that they built from two 8-bit images, one image is loaded to the RGB coordinates of the texture.
//The other image is loaded into the alpha channel.
//The verticies contain an RGB color which is converted to HSL space. 

//The alpha channel of the texture indicates whether the pixel is part of the annotation or not.  The alpha value is only used for this purpose
//The RGB component of the texture indicates the saturation value of the pixel.
//The program determines Saturation via converting the RGB color attribute of the vertex.
//The program determines the hue via converting the RGB color attribute of the vertex.
//The alpha channel of vertex color indicates how much the texture value is blended with the background value.


uniform const sampler BackgroundTextureSampler : register(s0) = sampler_state
{
	Texture = (BackgroundTexture);
	MipFilter = POINT;
	MinFilter = POINT;
	MagFilter = POINT;
};

uniform const sampler AnnotationTextureSampler : register(s1) = sampler_state
{
	Texture = (AnnotationTexture);
	MipFilter = POINT; 
	MinFilter = LINEAR; 
	MagFilter = POINT; 
};

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
};

struct CircleVertexShaderOutput
{
    float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 CenterDistance : TEXCOORD0;
};

struct PixelShaderInput
{
    float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
    float2 TexCoord : TEXCOORD0; 
	float2 CenterDistance : TEXCOORD1;
	float2 ScreenTexCoord : SV_Position;
};

struct CirclePixelShaderInput
{
    float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 ScreenTexCoord : SV_Position;
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

    return output;
}

CircleVertexShaderOutput CircleVertexShaderFunction(VertexShaderInput input)
{
    CircleVertexShaderOutput output;
    output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = input.Color;  //output.HSLColor = RGBToHCL(input.Color);
	output.CenterDistance = input.TexCoord.xy - 0.5;

    return output;
}

float CenterDistanceSquared(float2 CenterDistance)
{
	float XDist = CenterDistance.x;
	float YDist = CenterDistance.y;
	return (XDist * XDist) + (YDist * YDist);
}



PixelShaderOutput RGBOverBackgroundLumaPixelShaderFunction(PixelShaderInput input)
{
	PixelShaderOutput output; 
	output.Depth = CenterDistanceSquared(input.CenterDistance);

	float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord) ;
	//RGBColor.a = input.Color.a * RGBColor.a;
	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
	clip(all(RGBColor.rgb) <= 0 ? -1 : 1);

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy-1)));
	output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, InputLumaAlpha);
	output.Color.a = RGBColor.a;

    return output;
}

PixelShaderOutput RGBCircleTextureOverBackgroundLumaPixelShaderFunction(PixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output; 
	output.Depth = CenterDistanceSquared(input.CenterDistance);

	clip(output.Depth > radiusSquared ? -1 : 1); //remove pixels outside the circle

	float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord) ; 
	//clip(RGBColor.r <= 0.0 ? -1.0 : 1.0);
	
	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy)));
	output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, 0);
	output.Color.a = RGBColor.r * input.HSLColor.a;

    return output;
}

PixelShaderOutput RGBCircleOverBackgroundLumaPixelShaderFunction(CirclePixelShaderInput input)
{
	//float OverlayLuma = mul(LumaWeights, ); 

	PixelShaderOutput output; 
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance);

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

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / RenderTargetSize.xy));
	output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, alphaBlend);
	output.Color.a = input.HSLColor.a;
    return output;
}

technique RGBOverBackgroundValueOverlayEffect
{
    pass
    {
		VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 RGBCircleTextureOverBackgroundLumaPixelShaderFunction();
    }

}

technique RGBCircleOverBackgroundValueOverlayEffect
{
    pass
    {
		VertexShader = compile vs_3_0 CircleVertexShaderFunction();
        PixelShader = compile ps_3_0 RGBCircleOverBackgroundLumaPixelShaderFunction();
    }

}