//Contains effects for rendering billboards.  Billboards are either a solid color or texture.  
//The input for a billboard is a square with corners at (-1,-1) & (1,1).


#include "HSLRGBLib.fx"
#include "OverlayShaderShared.fx"

uniform const float radiusSquared = 1;

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


/////////////////////////////////////////////////////
// Vertex Shader Input
/////////////////////////////////////////////////////

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


/////////////////////////////////////////////////////
// Vertex Shader Output
/////////////////////////////////////////////////////

//Output for vertex shader when color is set by vertex or effect
struct SolidVertexShaderOutput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 CenterDistance : TEXCOORD0;
};

//Output for vertex shader when color will be pulled directly from the texture
struct TextureVertexShaderOutput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
};

//Output for vertex shader when color is set by vertex or effect but blended depending on 
//the grayscale texture luminance
struct ColorizedGrayscaleTextureVertexShaderOutput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
};


/////////////////////////////////////////////////////
// Pixel Shader Input
/////////////////////////////////////////////////////

//Input for a pixel shader that pulls color from a texture
struct TexturePixelShaderInput
{
	float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
};

//Input for a pixel shader that pulls color from a vertex or the effect and renders a solid color
struct SolidColorPixelShaderInput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 ScreenTexCoord : SV_Position;
	float2 CenterDistance : TEXCOORD0;
};

//Input for a pixel shader that pulls color from a vertex or the effect and adjusts blending using a grayscale texture
struct LumaTexturePixelShaderInput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
	float2 ScreenTexCoord : SV_Position;
};


/////////////////////////////////////////////////////
// Pixel Shader Output
/////////////////////////////////////////////////////

struct PixelShaderOutput
{
	float4 Color : COLOR0;
	float Depth : DEPTH0;
};


////////////////
//Vertex Shaders
////////////////


//Color is taken from effect parameter
SolidVertexShaderOutput EffectColorVertexShaderFunction(VertexShaderInput input)
{
	SolidVertexShaderOutput output;
	output.CenterDistance = input.Position.xy;
	output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = AnnotationHSLColor;  //output.HSLColor = RGBToHCL(input.Color);

	return output;
}

//Color is taken from vertex
SolidVertexShaderOutput ColorVertexShaderFunction(ColorVertexShaderInput input)
{
	SolidVertexShaderOutput output;
	output.CenterDistance = input.Position.xy;
	output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = input.Color;  //output.HSLColor = RGBToHCL(input.Color);

	return output;
}


//Color is taken from texture
TextureVertexShaderOutput TextureShaderFunction(TextureVertexShaderInput input)
{
	TextureVertexShaderOutput output;
	output.TexCoord = input.TexCoord;
	output.Position = mul(input.Position, mWorldViewProj);
	output.CenterDistance = input.TexCoord.xy - 0.5;
	return output;
}



//Color is taken from effect parameter
//Texture is present
ColorizedGrayscaleTextureVertexShaderOutput EffectColorLumaTextureVertexShaderFunction(TextureVertexShaderInput input)
{
	ColorizedGrayscaleTextureVertexShaderOutput output;
	output.TexCoord = input.TexCoord;
	output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = AnnotationHSLColor; //RGBToHCL(input.Color); 
	output.CenterDistance = input.TexCoord.xy - 0.5;

	return output;
}


//Color is taken from vertex
//Texture is present
ColorizedGrayscaleTextureVertexShaderOutput VertexColorLumaTextureVertexShaderFunction(ColorTextureVertexShaderInput input)
{
	ColorizedGrayscaleTextureVertexShaderOutput output;
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


///////////////
//Pixel Shaders
///////////////

//Renders the billboard verts as a solid color and uses the graphics device alpha blend settings
PixelShaderOutput SolidColorPixelShaderFunction(SolidColorPixelShaderInput input)
{
	PixelShaderOutput output;

	output.Depth = input.CenterDistance.x + input.CenterDistance.y;
	output.Color = HCLToRGB(input.HSLColor);
	output.Color.a = input.HSLColor.a;
	return output;
}

//Renders the billboard verts as a solid color restricted to a unit circle and uses the graphics device alpha blend settings
PixelShaderOutput SolidColorCirclePixelShaderFunction(SolidColorPixelShaderInput input)
{
	PixelShaderOutput output;
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance);
	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle

	output = SolidColorPixelShaderFunction(input);
	return output;
}

//Draws a texture on a billboard.  Directly maps textures RGBA values to output.
//Uses the graphics device blend settings
PixelShaderOutput TexturePixelShaderFunction(TexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	output.Depth = input.CenterDistance.x + input.CenterDistance.y;

	float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord);
	clip(RGBColor.a <= 0.0 ? -1.0 : 1.0);
	output.Color = RGBColor;

	return output;
}

//Draws a texture on a billboard.  Directly maps textures RGBA values to output.  Any pixels outside the unit circle are clipped.
//Uses the graphics device alpha blend settings
PixelShaderOutput CircleTexturePixelShaderFunction(TexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance);
	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle
	output = TexturePixelShaderFunction(input);
	output.Depth = CenterDistSquared;

	return output;
}

//Draws a grayscale texture on a billboard.  Multiplies input.Color by Grayscale value to determine output color
//Uses the graphics device blend settings
PixelShaderOutput GrayscaleTexturePixelShaderFunction(LumaTexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	output.Depth = input.CenterDistance.x + input.CenterDistance.y;

	float4 RGBColor = tex2D(AnnotationTextureSampler, input.TexCoord);
	clip(RGBColor.a <= 0.0 ? -1.0 : 1.0);
	output.Color = RGBColor.r *  HCLToRGB(input.HSLColor);
	output.Color.a = RGBColor.a;

	return output;
}

//Draws a grayscale texture on a billboard.  Multiplies input.Color by Grayscale value to determine output color
//Any pixels outside the unit circle are clipped.
//Uses the graphics device alpha blend settings
PixelShaderOutput CircleGrayscaleTexturePixelShaderFunction(LumaTexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance);
	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle

	output = GrayscaleTexturePixelShaderFunction(input);
	output.Depth = CenterDistSquared; 

	return output;
}


//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//The pixel shaders below this point blend with the background texture by using my HSL blending technique

PixelShaderOutput SolidColorOverBackgroundLumaPixelShaderFunction(SolidColorPixelShaderInput input)
{
	PixelShaderOutput output;

	output.Depth = input.CenterDistance.x + input.CenterDistance.y;

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / RenderTargetSize.xy));
	output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, InputLumaAlpha);
	output.Color.a = input.HSLColor.a;
	return output;
}


//Draws a solid color circle.  Any pixels outside the unit circle are clipped
PixelShaderOutput SolidColorCircleOverBackgroundLumaPixelShaderFunction(SolidColorPixelShaderInput input)
{
	PixelShaderOutput output;
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance); 
	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle
	output = SolidColorOverBackgroundLumaPixelShaderFunction(input);
	output.Depth = CenterDistSquared;
	return output;
}


//Draws a texture on a billboard.  Textures are a greyscale+Alpha image
//Color is from the pixel input, Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
//To use this one should render a RGB texture to an HSL version.  Then pass the HSL texture to this shader
PixelShaderOutput HSLATextureOverBackgroundLumaPixelShaderFunction(LumaTexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	output.Depth = input.CenterDistance.x + input.CenterDistance.y;

	float4 HSLColor = tex2D(AnnotationTextureSampler, input.TexCoord);
	clip(HSLColor.a <= 0.0 ? -1.0 : 1.0);

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy)));
	output.Color = BlendHSLColorOverBackground(HSLColor, RGBBackgroundColor, 1.0f - HSLColor.a);
	output.Color.a = 1.0;

	return output;
}



//Draws a texture on a billboard.  Any pixels outside the unit circle are clipped.  Textures are a greyscale+Alpha image
//Color is from the pixel input, Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
PixelShaderOutput CircleHSLATextureOverBackgroundLumaPixelShaderFunction(LumaTexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance);
	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle
	output = HSLATextureOverBackgroundLumaPixelShaderFunction(input);
	output.Depth = CenterDistSquared;

	return output;
}


//Draws a texture on a billboard.  Textures are a greyscale+Alpha image
//Color is from the pixel input, Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
PixelShaderOutput GrayscaleTextureOverBackgroundLumaPixelShaderFunction(LumaTexturePixelShaderInput input)
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


//Draws a texture on a billboard.  Any pixels outside the unit circle are clipped.  Textures are a greyscale+Alpha image
//Color is from the pixel input, Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
PixelShaderOutput CircleGrayscaleTextureOverBackgroundLumaPixelShaderFunction(LumaTexturePixelShaderInput input)
{
	//Blends a greyscale texture, where the grey value indicates luma.
	PixelShaderOutput output;
	float CenterDistSquared = CenterDistanceSquared(input.CenterDistance);
	clip(CenterDistSquared > radiusSquared ? -1 : 1); //remove pixels outside the circle
	output = GrayscaleTextureOverBackgroundLumaPixelShaderFunction(input);
	output.Depth = CenterDistSquared;

	return output;
}



////////////////////////////////////////////
// Standard alpha blending billboard effects
////////////////////////////////////////////

technique SingleColorAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorPixelShaderFunction();
	}
}


technique VertexColorAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 ColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorPixelShaderFunction();
	}
}


technique TextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 TextureShaderFunction();
		PixelShader = compile ps_3_0 TexturePixelShaderFunction();
	}
}


technique SingleColorTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 GrayscaleTexturePixelShaderFunction();
	}
}


technique VertexColorTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 VertexColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 GrayscaleTexturePixelShaderFunction();
	}
}

////////////////////////////////////////////
// Standard alpha blending circle effects
////////////////////////////////////////////


technique CircleSingleColorAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorCirclePixelShaderFunction();
	}
}


technique CircleVertexColorAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 ColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorCirclePixelShaderFunction();
	}
}


technique CircleTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 TextureShaderFunction();
		PixelShader = compile ps_3_0 CircleTexturePixelShaderFunction();
	}
}


technique CircleSingleColorTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 CircleGrayscaleTexturePixelShaderFunction();
	}
}


technique CircleVertexColorTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 VertexColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 CircleGrayscaleTexturePixelShaderFunction();
	}
}


////////////////////////////////////////
// Luma overlay effect definitions below
////////////////////////////////////////


technique SingleColorLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorOverBackgroundLumaPixelShaderFunction();
	}
}


technique VertexColorLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 ColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorOverBackgroundLumaPixelShaderFunction();
	}
}


technique TextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 TextureShaderFunction();
		PixelShader = compile ps_3_0 HSLATextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique SingleColorTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 GrayscaleTextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique VertexColorTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 VertexColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 GrayscaleTextureOverBackgroundLumaPixelShaderFunction();
	}
}



////////////////////////////////////////
// Circle Luma overlay effect definitions below
////////////////////////////////////////


technique CircleSingleColorLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorCircleOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleVertexColorLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 ColorVertexShaderFunction();
		PixelShader = compile ps_3_0 SolidColorCircleOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 TextureShaderFunction();
		PixelShader = compile ps_3_0 CircleHSLATextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleSingleColorTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 EffectColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 CircleGrayscaleTextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleVertexColorTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_3_0 VertexColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_3_0 CircleGrayscaleTextureOverBackgroundLumaPixelShaderFunction();
	}
}