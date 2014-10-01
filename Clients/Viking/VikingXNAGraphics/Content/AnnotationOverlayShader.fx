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



PixelShaderOutput RGBOverBackgroundLumaPixelShaderFunction(PixelShaderInput input)
{
	PixelShaderOutput output; 

	float XDist = input.CenterDistance.x;
	float YDist = input.CenterDistance.y;

	output.Depth = (XDist * XDist) + (YDist * YDist);

	float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord) ;
	//RGBColor.a = input.Color.a * RGBColor.a;
	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
	clip(all(RGBColor.rgb) <= 0 ? -1 : 1);

	float4 LumaColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy-1)));

	float Hue = input.HSLColor.r;
	float Saturation = input.HSLColor.g * RGBColor.r;
	float BackgroundLuma = mul(LumaColor, LumaWeights);

	float Luma = (BackgroundLuma * (1-RGBColor.a)) + ((input.HSLColor.b * RGBColor.a));  //This should be a greyscale image, so any component will match the value

	float4 hsv = {Hue, Saturation, Luma, RGBColor.a};
	output.Color = HCLToRGB(hsv);
	output.Color.a = 1; 
	
    return output;
}

PixelShaderOutput RGBCircleTextureOverBackgroundLumaPixelShaderFunction(PixelShaderInput input)
{
	PixelShaderOutput output; 

	float XDist = input.CenterDistance.x;
	float YDist = input.CenterDistance.y;
	
	float CenterDistSquared = (XDist * XDist) + (YDist * YDist); 

	float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord) ;

	clip(RGBColor.a <= 0 ? -1 : 1);
	
	output.Depth = CenterDistSquared;
	
	float AlphaBlend = RGBColor.r * input.HSLColor.a;

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

	float4 LumaColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy-1)));

	float Hue = input.HSLColor.r;
	float Saturation = input.HSLColor.g * RGBColor.r;
	float BackgroundLuma = mul(LumaColor, LumaWeights);

	float Luma = (BackgroundLuma * (1-AlphaBlend)) + ((AlphaBlend * input.HSLColor.b));  //This should be a greyscale image, so any component will match the value

	float4 hsv = {Hue, Saturation, Luma, RGBColor.a};
	output.Color = HCLToRGB(hsv);
	output.Color.a = 1; 
	
    return output;
}

PixelShaderOutput RGBCircleOverBackgroundLumaPixelShaderFunction(CirclePixelShaderInput input)
{
	//float OverlayLuma = mul(LumaWeights, ); 

	PixelShaderOutput output; 

	float XDist = input.CenterDistance.x;
	float YDist = input.CenterDistance.y;

	float CenterDistSquared = (XDist * XDist) + (YDist * YDist); 
	output.Depth = CenterDistSquared;

	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle
	
	float alphaBlend = 0;
	//float alphaMax = 0.33;
	float alphaMax = input.HSLColor.a;

	if(CenterDistSquared >= borderStartSquared)
		alphaBlend = alphaMax;
	else if(CenterDistSquared >= borderBlendStartSquared)
	{
		alphaBlend = (sqrt(CenterDistSquared) - borderBlendStartRadius) / (borderStartRadius - borderBlendStartRadius) * alphaMax;
	}
	
	float4 LumaColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / RenderTargetSize.xy));

	float Hue = input.HSLColor.r;
	float Saturation = input.HSLColor.g;
	float BackgroundLuma = mul(LumaColor, LumaWeights);

	float Luma = (BackgroundLuma * (1-alphaBlend)) + ((input.HSLColor.b * alphaBlend));  //This should be a greyscale image, so any component will match the value

	float4 hsv = {Hue, Saturation, Luma, alphaBlend};
	output.Color = HCLToRGB(hsv);
	output.Color.a = 1; 
	
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