//Contains effects for rendering billboards.  Billboards are either a solid color or texture.  
//The input for a billboard is a square with corners at (-1,-1) & (1,1).


#include "../../MonogameXNAGraphicsShared/Content/HSLRGBLib.fx"
#include "../../MonogameXNAGraphicsShared/Content/OverlayShaderShared.fx"

uniform const float radiusSquared = 1;

uniform const float borderStartRadius = 0.475;
uniform const float borderStartSquared = 0.475 * 0.475;

uniform const float borderBlendStartRadius = 0.45;
uniform const float borderBlendStartSquared = 0.45 * 0.45;

//Ring effect (CircleRingOverlayEffect). Radii are fractions of the circle radius (0 = center, 1 = rim).
//The outer band [ringSolidStart, 1] is fully opaque; the fade band [ringFadeStart, ringSolidStart] ramps to clear;
//everything inside ringFadeStart is clipped so the image underneath stays visible.
uniform const float ringSolidStart = 0.75;
uniform const float ringFadeStart = 0.625;
//How far (in luma, 0..1) the ring sits from the average background luma: darker over bright tissue, lighter over dark.
uniform const float ringContrast = 0.30;
//Average background luma above which the ring goes darker than the background, otherwise lighter.
uniform const float ringAdaptMid = 0.5;
//Overall ring opacity (0..1), multiplied with the annotation color's alpha.
uniform const float ringOpacity = 0.85;

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
	float4 Position : SV_Position;
	float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
};

//Input for a pixel shader that pulls color from a vertex or the effect and renders a solid color
struct SolidColorPixelShaderInput
{
	float4 Position : SV_Position;
	float4 HSLColor : COLOR0;
	float2 CenterDistance : TEXCOORD0;
};

//Input for a pixel shader that pulls color from a vertex or the effect and adjusts blending using a grayscale texture
struct LumaTexturePixelShaderInput
{
	float4 Position : SV_Position;
	float4 HSLColor : COLOR0;
	float2 TexCoord : TEXCOORD0;
	float2 CenterDistance : TEXCOORD1;
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
	output.Depth = CenterDistSquared;
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


//Output of the ring vertex shader. ScreenCenterAxes holds the circle center and its half-radius
//x/y axes, in render-target pixels, so the pixel shader can sample the background under the whole circle.
struct RingVertexShaderOutput
{
	float4 Position : POSITION0;
	float4 HSLColor : COLOR0;
	float2 CenterDistance : TEXCOORD0;
	float4 ScreenCenterAxes : TEXCOORD1;
	float2 ScreenAxisY : TEXCOORD2;
};

//Converts a clip-space position to render-target pixels (y down), matching how the other luma shaders address the background.
float2 ClipToPixel(float4 clip)
{
	float2 ndc = clip.xy / clip.w;
	return float2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5) * RenderTargetSize;
}

RingVertexShaderOutput RingVertexShaderFunction(VertexShaderInput input)
{
	RingVertexShaderOutput output;
	output.CenterDistance = input.Position.xy;
	output.Position = mul(input.Position, mWorldViewProj);
	output.HSLColor = AnnotationHSLColor;

	float2 center = ClipToPixel(mul(float4(0, 0, 0, 1), mWorldViewProj));
	output.ScreenCenterAxes = float4(center, ClipToPixel(mul(float4(0.5, 0, 0, 1), mWorldViewProj)) - center);
	output.ScreenAxisY = ClipToPixel(mul(float4(0, 0.5, 0, 1), mWorldViewProj)) - center;
	return output;
}

float BackgroundLumaAtPixel(float2 pixel)
{
	return CalculatePerceptualLumaFromRGB(tex2D(BackgroundTextureSampler, pixel / RenderTargetSize));
}

//Draws a procedural ring whose luma adapts to the image underneath: ringContrast darker than bright tissue, ringContrast lighter than dark tissue.
//No texture. CenterDistance is in unit-circle space (-1..1) so the rim is at distance 1.
//The background is averaged at the center and at half radius along both axes (the area the ring encloses) so the
//ring color is constant around a circle instead of flickering with per-pixel texture noise.
//The rim is anti-aliased with the screen-space derivative so it stays crisp at any zoom.
//Uses the graphics device alpha blend settings
PixelShaderOutput CircleRingPixelShaderFunction(RingVertexShaderOutput input)
{
	PixelShaderOutput output;
	float dist = length(input.CenterDistance);
	float rimCoverage = saturate((1.0 - dist) / max(fwidth(dist), 0.0001));
	float ring = smoothstep(ringFadeStart, ringSolidStart, dist);
	float alpha = ring * rimCoverage;
	clip(alpha <= 0.002 ? -1 : 1);

	float2 center = input.ScreenCenterAxes.xy;
	float2 axisX = input.ScreenCenterAxes.zw;
	float2 axisY = input.ScreenAxisY;
	float backgroundLuma = (BackgroundLumaAtPixel(center)
		+ BackgroundLumaAtPixel(center + axisX) + BackgroundLumaAtPixel(center - axisX)
		+ BackgroundLumaAtPixel(center + axisY) + BackgroundLumaAtPixel(center - axisY)) * 0.2;

	float ringLuma = backgroundLuma > ringAdaptMid ? backgroundLuma - ringContrast : backgroundLuma + ringContrast;
	ringLuma = clamp(ringLuma, 0.05, 0.95);

	float chroma = input.HSLColor.g;
	if (chroma > 0.0)
	{
		chroma = min(chroma, GetMaxChromaAtLuma(input.HSLColor.r, ringLuma));
	}

	float4 hcl = { input.HSLColor.r, chroma, ringLuma, 1.0 };
	output.Color = HCLToRGB(hcl);
	output.Color.a = alpha * ringOpacity * input.HSLColor.a;
	//Deliberately the full 0..1 range (not the textured circles' 0..0.25): rings lose the depth test to the polygon fill and draw behind it.
	//Vertex clicks are hit-tested geometrically, so this does not affect selection.
	output.Depth = dist * dist;
	return output;
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////
//The pixel shaders below this point blend with the background texture by using my HSL blending technique

PixelShaderOutput SolidColorOverBackgroundLumaPixelShaderFunction(SolidColorPixelShaderInput input)
{
	PixelShaderOutput output;

	output.Depth = input.CenterDistance.x + input.CenterDistance.y;
	float2 ScreenTexCoord = input.Position.xy / input.Position.w;

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((ScreenTexCoord.xy) / RenderTargetSize.xy));
	output.Color = BlendHCLColorOverBackground(input.HSLColor, RGBBackgroundColor, InputLumaAlpha);
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

	float2 ScreenTexCoord = input.Position.xy / input.Position.w;

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((ScreenTexCoord.xy) / (RenderTargetSize.xy)));
	output.Color = BlendHCLColorOverBackground(HSLColor, RGBBackgroundColor, 1.0f - HSLColor.a);
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

	float2 ScreenTexCoord = input.Position.xy / input.Position.w;

	//This is a greyscale+Alpha image.  Greyscale indicates the degree of color, alpha indicates degree to which we use Overlay Luma or Background Luma
	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((ScreenTexCoord.xy) / (RenderTargetSize.xy)));
	output.Color = BlendHCLColorOverBackground(input.HSLColor, RGBBackgroundColor, 1.0f - RGBColor.a);
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
		VertexShader = compile vs_4_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_4_0 SolidColorPixelShaderFunction();
	}
}


technique VertexColorAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 ColorVertexShaderFunction();
		PixelShader = compile ps_4_0 SolidColorPixelShaderFunction();
	}
}


technique TextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 TextureShaderFunction();
		PixelShader = compile ps_4_0 TexturePixelShaderFunction();
	}
}


technique SingleColorTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 EffectColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_4_0 GrayscaleTexturePixelShaderFunction();
	}
}


technique VertexColorTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 VertexColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_4_0 GrayscaleTexturePixelShaderFunction();
	}
}

////////////////////////////////////////////
// Standard alpha blending circle effects
////////////////////////////////////////////


technique CircleSingleColorAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_4_0 SolidColorCirclePixelShaderFunction();
	}
}


technique CircleVertexColorAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 ColorVertexShaderFunction();
		PixelShader = compile ps_4_0 SolidColorCirclePixelShaderFunction();
	}
}


technique CircleTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 TextureShaderFunction();
		PixelShader = compile ps_4_0 CircleTexturePixelShaderFunction();
	}
}


technique CircleSingleColorTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 EffectColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_4_0 CircleGrayscaleTexturePixelShaderFunction();
	}
}


technique CircleVertexColorTextureAlphaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 VertexColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_4_0 CircleGrayscaleTexturePixelShaderFunction();
	}
}


////////////////////////////////////////
// Luma overlay effect definitions below
////////////////////////////////////////


technique SingleColorLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_4_0 SolidColorOverBackgroundLumaPixelShaderFunction();
	}
}


technique VertexColorLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 ColorVertexShaderFunction();
		PixelShader = compile ps_4_0 SolidColorOverBackgroundLumaPixelShaderFunction();
	}
}


technique TextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 TextureShaderFunction();
		PixelShader = compile ps_4_0 HSLATextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique SingleColorTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 EffectColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_4_0 GrayscaleTextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique VertexColorTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 VertexColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_4_0 GrayscaleTextureOverBackgroundLumaPixelShaderFunction();
	}
}



////////////////////////////////////////
// Circle Luma overlay effect definitions below
////////////////////////////////////////


technique CircleSingleColorLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 EffectColorVertexShaderFunction();
		PixelShader = compile ps_4_0 SolidColorCircleOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleVertexColorLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 ColorVertexShaderFunction();
		PixelShader = compile ps_4_0 SolidColorCircleOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 TextureShaderFunction();
		PixelShader = compile ps_4_0 CircleHSLATextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleSingleColorTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 EffectColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_4_0 CircleGrayscaleTextureOverBackgroundLumaPixelShaderFunction();
	}
}


technique CircleVertexColorTextureLumaOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 VertexColorLumaTextureVertexShaderFunction();
		PixelShader = compile ps_4_0 CircleGrayscaleTextureOverBackgroundLumaPixelShaderFunction();
	}
}


////////////////////////////////////////
// Ring circle effect
////////////////////////////////////////

technique CircleRingOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 RingVertexShaderFunction();
		PixelShader = compile ps_4_0 CircleRingPixelShaderFunction();
	}
}


////////////////////////////////////////
// Stripe effect (StripeOverlayEffect)
////////////////////////////////////////

//White diagonal stripes inside a clip mask, worked out per pixel so stripe and clip edges stay smooth at any zoom.
//Positions are measured in mask texels (TexCoord * stripeTexelCount), so the stripes are tied to the image and scale with zoom.
uniform const texture StripeClipTexture;

//Linear filtering makes the clip edge a smooth curve through the mask instead of texel-sized steps.
uniform const sampler StripeClipSampler : register(s2) = sampler_state
{
    Texture = (StripeClipTexture);
    MipFilter = POINT;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

//Mask texels across and down the quad. Every other stripe size below is in these texels.
uniform const float2 stripeTexelCount = float2(1, 1);
//The stripes run perpendicular to this direction, e.g. (1, 1) or (1, -1).
uniform const float2 stripeDirection = float2(1, 1);
//Distance along stripeDirection from one stripe to the next: a lit stripe and a clear gap of equal width.
uniform const float stripePeriod = 4;
//Optional band across x + y that limits the lit stripes: x is its centre, y its half width. y <= 0 lights them all.
uniform const float2 stripeBand = float2(0, 0);
//Clip texture alpha (0..1) at the edge of the clipped area.
uniform const float stripeClipLevel = 0.5;
//Alpha of a fully lit stripe.
uniform const float stripeAlpha = 1;

PixelShaderOutput StripePixelShaderFunction(TexturePixelShaderInput input)
{
	PixelShaderOutput output;
	output.Depth = saturate((input.CenterDistance.x + input.CenterDistance.y) * 0.5 + 0.5);

	float2 texel = input.TexCoord * stripeTexelCount;

	//Clip edge, faded over about one screen pixel.
	float clipValue = tex2D(StripeClipSampler, input.TexCoord).a - stripeClipLevel;
	float clipCoverage = saturate(clipValue / max(fwidth(clipValue), 1e-5) + 0.5);

	//Stripes: edge is the distance to the nearest stripe edge in periods, positive on a stripe and negative in a gap.
	float u = dot(texel, stripeDirection) / stripePeriod;
	float uWidth = max(fwidth(u), 1e-5);
	float edge = 0.25 - abs(frac(u + 0.25) - 0.5);
	float stripe = saturate(edge / uWidth + 0.5);
	//Stripes only a few pixels apart would alias, so fade them toward their average coverage.
	stripe = lerp(stripe, 0.5, saturate((uWidth * 4.0) - 1.0));

	float band = 1;
	if (stripeBand.y > 0)
	{
		band = 1 - saturate(abs(texel.x + texel.y - stripeBand.x) / stripeBand.y);
		band = band * band * (3 - (2 * band));
	}

	float alpha = stripeAlpha * clipCoverage * stripe * band;
	clip(alpha <= 0.0 ? -1.0 : 1.0);
	output.Color = float4(1, 1, 1, alpha);
	return output;
}

technique StripeOverlayEffect
{
	pass
	{
		VertexShader = compile vs_4_0 TextureShaderFunction();
		PixelShader = compile ps_4_0 StripePixelShaderFunction();
	}
}
