uniform const float4x4 mWorldViewProj;

//These textures are in HSV space, except that Hue is indicated by two components, alpha and beta.  The only value we need from the textures is saturation.  Hue is constant for each channel
//Layout is as follows:
//  r = Hue
//  g = Value
//	b = Saturation

//Only have variables above three because you shouldn't run this if you don't have two channels to blendfs

#include "../../MonogameXNAGraphicsShared/Content/HSLRGBLib.fx"

uniform const float ChannelHueAlpha;   //The hue of the HSV channel, used to determine the hue of the overlay texture
uniform const float ChannelHueBeta;    // The saturation of the HSV channel, used to determine the hue of the overlay texture

uniform const float4 OverlayColor;  // The RGB color the overlay texture should produce on the base texture
uniform const float4 OverlayColorScalar; //This is the scalar used to multiply the overlay color by. For the sum function it is the OverlayColor / TotalColor.  This prevents the summed output from exceeding 1.0 which is the monogame limit.
uniform const float4 OverlayChannelTotals; //When we perform a sum operation on multiple textures, this is the total of the colors of each channel summed.

uniform const texture BackgroundTexture; 
uniform const texture OverlayTexture;

static const float TAU = 6.28318530718; // 2 * PI, used for converting hue to radians

sampler2D BaseSampler = sampler_state
{
	Texture = <BackgroundTexture>;
	MinFilter = Point;
	MagFilter = Linear;
	MipFilter = NONE;
	AddressU = Clamp;
	AddressV = Clamp;
};

sampler2D ChannelSampler = sampler_state
{
	Texture = <OverlayTexture>;
	MinFilter = Point;
	MagFilter = Linear;
	MipFilter = NONE;
	AddressU = Clamp;
	AddressV = Clamp;
};

// My shader requires a texture and verticies
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Normal   : NORMAL;

    // TODO: add input channels such as texture
    // coordinates and vertex colors here.
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0; 

    // TODO: add vertex shader outputs such as colors and texture
    // coordinates here. These values will automatically be interpolated
    // over the triangle, and provided as input to your pixel shader.
};

struct PixelShaderOutput
{
	float4 Color : COLOR0;
};

struct PixelSumShaderOutput
{
	float4 Color : COLOR0;
	float1 Depth : DEPTH0; //Used to store the accumulated weight of the pixel
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
	
    VertexShaderOutput output; 
    output.TexCoord = input.TexCoord; 
    output.Position = mul(input.Position, mWorldViewProj); 
    return output;
}

PixelShaderOutput HCLToRGBPixelShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output;
	float4 ChannelColor = tex2D(ChannelSampler, input.TexCoord.xy);
	output.Color.rgb = HCLToRGB(ChannelColor);
	output.Color.a = ChannelColor.a; // Use original alpha for blending
	return output;
}

/// <summary>
/// This function takes an HCL overlay image and adds it to the render target. 
/// After all images have been added, a final pass converts the accumulated values
/// to HCL.  The HCL can then be converted to RGB for display.
/// </summary>
/// <param name="input"></param>
/// <returns></returns>
PixelSumShaderOutput SumHCLImagesShaderFunction(VertexShaderOutput input, float currentDepth : DEPTH0) 
{
	PixelSumShaderOutput output;
	float4 targetSample = tex2D(BaseSampler, input.TexCoord.xy);
	float4 channelSample = tex2D(ChannelSampler, input.TexCoord.xy);
	float hue = channelSample.r * TAU; // Convert hue to radians
	float chroma = channelSample.g;
	float luma = channelSample.b;

	float weight = channelSample.a;

	output.Color.r = targetSample.r + (cos(hue) * chroma * luma * weight);
	output.Color.g = targetSample.g + (sin(hue) * chroma * luma * weight);
	output.Color.b = targetSample.b + (chroma * weight);
	output.Color.a = targetSample.a + (weight * luma); // Accumulate luma in the alpha channel based on weight and luma 
	output.Depth = currentDepth + weight; // Accumulate weight in the depth texture for normalization later
	return output;
}

/*
/// <summary>
/// This function merges two HCL images together.
/// </summary>
/// <param name="input"></param>
/// <returns></returns>
PixelSumShaderOutput MergeHCLImagesShaderFunction(VertexShaderOutput input, float currentDepth : DEPTH0)
{
	PixelSumShaderOutput output;
	float4 targetSample = tex2D(BaseSampler, input.TexCoord.xy);
	float4 channelSample = tex2D(ChannelSampler, input.TexCoord.xy);
	float hue = channelSample.r * TAU; // Convert hue to radians
	float chroma = channelSample.g;
	float luma = channelSample.b;

	float weight = channelSample.a;

	output.Color.r = targetSample.r + (cos(hue) * chroma * luma * weight);
	output.Color.g = targetSample.g + (sin(hue) * chroma * luma * weight);
	output.Color.b = targetSample.b + (chroma * weight);
	output.Color.a = targetSample.a + (weight * luma); // Accumulate luma in the alpha channel based on weight and luma 
	output.Depth = currentDepth + weight; // Accumulate weight in the depth texture for normalization later
	return output;
}
*/

PixelShaderOutput RGBToHCLPixelShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output;
	float4 ChannelColor = tex2D(ChannelSampler, input.TexCoord.xy);
	output.Color.rgb = RGBToHCL(ChannelColor); 
	output.Color.a = ChannelColor.a; // Use original alpha for blending
	return output;
}

// Single HSV processing function that uses CurrentTextureIndex
PixelShaderOutput HSVMergePixelShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output;
	float4 sample = tex2D(ChannelSampler, input.TexCoord.xy);

	float3 HS = {ChannelHueAlpha * sample.g,
				 ChannelHueBeta * sample.g,
				 sample.b};
	
	output.Color.r = saturate((atan2(HS[1], HS[0]) / 6.28318) + 0.5);
	output.Color.g = length(float2(HS[0], HS[1])) / sample.g;
	output.Color.b = HS[2]; 
	output.Color.a = sample.g; // Use saturation as alpha for blending 
	return output;
}

// Single RGB processing function that uses CurrentTextureIndex
PixelShaderOutput SumRGBPixelShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output;
	float4 accumulatorSample = tex2D(BaseSampler, input.TexCoord.xy);
	float4 inputSample = tex2D(ChannelSampler, input.TexCoord.xy); 
	output.Color = accumulatorSample + (inputSample * OverlayColorScalar);;  // Add the RGB values 
	return output;
}

/// <summary>
///  Divide every channel by OverlayChannelTotals parameter
/// </summary>
/// <param name="input"></param>
/// <returns></returns>
PixelShaderOutput NormalizeShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output; 
	float4 inputSample = tex2D(ChannelSampler, input.TexCoord.xy);
	output.Color.rgb  = inputSample.rgb / OverlayChannelTotals.xyz; // Normalize each channel by the total of that channel
	output.Color.a = inputSample.a;
	return output;
}

// Final composition shader for HSV
PixelShaderOutput HSVFinalComposition(VertexShaderOutput input)
{
	PixelShaderOutput output;
	
	// This pass will receive the accumulated result from previous passes
	// The blending will have already combined the values
	output.Color = float4(0, 0, 0, 1); // This will be overridden by the blend state
	
	return output;
}

// Final composition shader for RGB
PixelShaderOutput RGBFinalComposition(VertexShaderOutput input)
{
	PixelShaderOutput output;
	
	// This pass will receive the accumulated result from previous passes
	// The blending will have already combined the values
	output.Color = float4(0, 0, 0, 1); // This will be overridden by the blend state
	
	return output;
}

technique HCLToRGB
{
	pass convert
	{
		AlphaBlendEnable = false;
		VertexShader = compile vs_4_0 VertexShaderFunction();
		PixelShader = compile ps_4_0 HCLToRGBPixelShaderFunction();
	}
}

technique RGBToHCL
{
	pass convert
	{
		AlphaBlendEnable = false;
		VertexShader = compile vs_4_0 VertexShaderFunction();
		PixelShader = compile ps_4_0 RGBToHCLPixelShaderFunction();
	}
}

technique SumHCLImages
{
	pass convert
	{
		AlphaBlendEnable = false;
		VertexShader = compile vs_4_0 VertexShaderFunction();
		PixelShader = compile ps_4_0 SumHCLImagesShaderFunction();
	}
}
/*
* I have not finished this, the intent is to be able to merge images in HCL space
technique NormalizeHCLSumImage
{
	pass convert
	{
		AlphaBlendEnable = false;
		VertexShader = compile vs_4_0 VertexShaderFunction();
		PixelShader = compile ps_4_0 MergeHCLImagesShaderFunction();
	}
}
*/

technique SumRGBImages
{
    pass P0
    {	
		AlphaBlendEnable = false; 
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 SumRGBPixelShaderFunction();
    } 
}

technique NormalizeByTotal
{
	pass P0
	{
		AlphaBlendEnable = false;
		VertexShader = compile vs_4_0 VertexShaderFunction();
		PixelShader = compile ps_4_0 NormalizeShaderFunction();
	}
}

technique MergeHSVImages
{
    pass P0
    {	
		AlphaBlendEnable = true; 
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 HSVMergePixelShaderFunction();
    } 
}

