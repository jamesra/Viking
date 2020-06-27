//Contains effects for rendering billboards.  Billboards are either a solid color or texture.  
//The input for a billboard is a square with corners at (-1,-1) & (1,1).


#include "HSLRGBLib.fx"
#include "OverlayShaderShared.fx"

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


//Input is always expected to be four corners at -1,-1 and 1,1
struct VertexShaderInput
{
	float4 Position : POSITION0;
};

//Used when we have a texture and want to pull color from an effect parameter or the texture itself
struct TextureVertexShaderInput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

//Pulls color from the vertex data
struct ColorVertexShaderInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
};

//I'm not sure if this is needed anymore.  It would be used when we want per-vertex color and a texture
struct ColorTextureVertexShaderInput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
	float2 TexCoord : TEXCOORD0;
};


struct SolidVertexShaderOutput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 CenterDistance : TEXCOORD0;
};
 
struct TextureVertexShaderOutput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
};


struct SolidColorPixelShaderInput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 ScreenTexCoord : SV_Position;
	float2 CenterDistance : TEXCOORD0;
};


struct TexturePixelShaderInput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
	float2 ScreenTexCoord : SV_Position;
};

struct PixelShaderOutput
{
	float4 Color : COLOR0;
	float Depth : DEPTH0;
};


SolidVertexShaderOutput EffectColorVertexShaderFunction(VertexShaderInput input)
{
	SolidVertexShaderOutput output;
	output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = AnnotationHSLColor;  //output.HSLColor = RGBToHCL(input.Color);
	output.CenterDistance = input.Position.xy;

	return output;
}


TexturePixelShaderInput EffectColorTextureVertexShaderFunction(TextureVertexShaderInput input)
{
	TextureVertexShaderOutput output;
	output.TexCoord = input.TexCoord;
	output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = AnnotationHSLColor; //RGBToHCL(input.Color); 
	output.CenterDistance = input.TexCoord.xy - 0.5;

	return output;
}


CircleVertexShaderOutput ColorVertexShaderFunction(ColorVertexShaderInput input)
{
	SolidVertexShaderOutput output;
	output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = input.Color;  //output.HSLColor = RGBToHCL(input.Color);
	output.CenterDistance = input.Position.xy;

	return output;
}


VertexShaderOutput TextureColorVertexShaderFunction(ColorTextureVertexShaderInput input)
{
	TextureVertexShaderOutput output;
	output.TexCoord = input.TexCoord;
	output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = input.Color; //RGBToHCL(input.Color); 
	output.CenterDistance = input.TexCoord.xy - 0.5;

	return output;
} 


float CenterDistanceSquared(float2 CenterDistance)
{
	float XDist = CenterDistance.x;
	float YDist = CenterDistance.y;
	return (XDist * XDist) + (YDist * YDist);
}


PixelShaderOutput SolidColorOverBackgroundLumaPixelShaderFunction(SolidColorPixelShaderInput input)
{ 
	PixelShaderOutput output; 

	output.Depth = input.CenterDistance.x + input.CenterDistance.y;

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / RenderTargetSize.xy));
	output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, InputLumaAlpha);
	output.Color.a = input.HSLColor.a;
	return output;
}

//Draws a texture on a billboard.  Textures are a greyscale+Alpha image
//Color is from the pixel input, Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
PixelShaderOutput RGBATextureOverBackgroundLumaPixelShaderFunction(TexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	output.Depth = input.CenterDistance.x + input.CenterDistance.y;

	float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord);  
	clip(RGBColor.a <= 0.0 ? -1.0 : 1.0); 

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy)));
	output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, 1.0f - RGBColor.a);
	output.Color.a = RGBColor.r * input.HSLColor.a;

	return output;
}

//Draws a solid color circle.  Any pixels outside the unit circle are clipped
PixelShaderOutput SolidColorCircleOverBackgroundLumaPixelShaderFunction(SolidColorPixelShaderInput input)
{ 
	PixelShaderOutput output;
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance);

	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle
	output.Depth = CenterDistSquared;

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / RenderTargetSize.xy));
	output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, InputLumaAlpha);
	output.Color.a = input.HSLColor.a;
	return output;
}


//Draws a texture on a billboard.  Any pixels outside the unit circle are clipped.  Textures are a greyscale+Alpha image
//Color is from the pixel input, Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
PixelShaderOutput CircleTextureOverBackgroundLumaPixelShaderFunction(TexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance)
	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle

	output.Depth = CenterDistSquared;
	
	float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord);
	clip(RGBColor.a <= 0.0 ? -1.0 : 1.0);

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy)));
	output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, 1.0f - RGBColor.a);
	output.Color.a = RGBColor.r * input.HSLColor.a;

	return output;
}



technique EffectColorOverBackgroundValueOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorOverBackgroundLumaPixelShaderFunction();
	}

}

technique EffectColorTextureOverBackgroundValueOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 RGBATextureOverBackgroundLumaPixelShaderFunction();
	}
}

technique VertexColorOverBackgroundValueOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 ColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorOverBackgroundLumaPixelShaderFunction();
	}
}

technique VertexColorTextureOverBackgroundValueOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 TextureColorVertexShaderFunction();
		PixelShader = compile ps_3_0 RGBATextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleEffectColorOverBackgroundValueOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorCircleOverBackgroundLumaPixelShaderFunction();
	}

}

technique CircleEffectColorTextureOverBackgroundValueOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 CircleTextureOverBackgroundLumaPixelShaderFunction();
	}
}

technique CircleVertexColorOverBackgroundValueOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 ColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorCircleOverBackgroundLumaPixelShaderFunction();
	}
}

technique CircleVertexColorTextureOverBackgroundValueOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 TextureColorVertexShaderFunction();
		PixelShader = compile ps_3_0 CircleTextureOverBackgroundLumaPixelShaderFunction();
	}
}