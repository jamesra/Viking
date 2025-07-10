uniform const float4x4 mWorldViewProj;

//These textures are in HSV space, except that Hue is indicated by two components, alpha and beta.  The only value we need from the textures is saturation.  Hue is constant for each channel
//Layout is as follows:
//  r = Hue
//  g = Value
//	b = Saturation

//Only have variables above three because you shouldn't run this if you don't have two channels to blendfs

#include "HSLRGBLib.fx"

int NumTextures;
int CurrentTextureIndex; // Index of current texture being processed

uniform const float ChannelHueAlpha[6];
uniform const float ChannelHueBeta[6] ;
uniform const float4 ChannelRGBColor[6]; 
uniform const float4 ChannelRGBColorTotal;

uniform const texture Texture1;
uniform const texture Texture2;
uniform const texture Texture3;
uniform const texture Texture4;
uniform const texture Texture5;

uniform const sampler ChannelSampler = sampler_state
{
	MipFilter = NONE;
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

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
	
    VertexShaderOutput output; 
    output.TexCoord = input.TexCoord;

    output.Position = mul(input.Position, mWorldViewProj);
	
    return output;
}

// Single HSV processing function that uses CurrentTextureIndex
PixelShaderOutput HSVMergePixelShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output;
	float4 ChannelColor = tex2D(ChannelSampler, input.TexCoord);

	float3 HS = {ChannelHueAlpha[CurrentTextureIndex] * ChannelColor.g,
				 ChannelHueBeta[CurrentTextureIndex] * ChannelColor.g,
				 ChannelColor.b};
	
	output.Color.r = saturate((atan2(HS[1], HS[0]) / 6.28318) + 0.5);
	output.Color.g = length(float2(HS[0], HS[1])) / ChannelColor.g; 
	output.Color.b = HS[2]; 
	output.Color.a = ChannelColor.g; // Use saturation as alpha for blending

	return output;
}

// Single RGB processing function that uses CurrentTextureIndex
PixelShaderOutput RGBMergePixelShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output;
	float4 ChannelColor = tex2D(ChannelSampler, input.TexCoord.xy);

	float4 HS = {ChannelRGBColor[CurrentTextureIndex].r * ChannelColor.r,
				 ChannelRGBColor[CurrentTextureIndex].g * ChannelColor.g,
				 ChannelRGBColor[CurrentTextureIndex].b * ChannelColor.b,
				 ChannelRGBColor[CurrentTextureIndex].a * ChannelColor.a}; 

	output.Color = HS;
	output.Color.a = ChannelColor.a; // Use original alpha for blending

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

technique MergeRGBImages
{
    pass P0
    {	
		AlphaBlendEnable = false; 
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 RGBMergePixelShaderFunction();
    }
    
    pass P1
    {	
		AlphaBlendEnable = true;
		SrcBlend = ONE;
		DestBlend = ONE;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 RGBMergePixelShaderFunction();
    }
    
    pass P2
    {	
		AlphaBlendEnable = true;
		SrcBlend = ONE;
		DestBlend = ONE;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 RGBMergePixelShaderFunction();
    }
    
    pass P3
    {	
		AlphaBlendEnable = true;
		SrcBlend = ONE;
		DestBlend = ONE;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 RGBMergePixelShaderFunction();
    }
    
    pass P4
    {	
		AlphaBlendEnable = true;
		SrcBlend = ONE;
		DestBlend = ONE;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 RGBMergePixelShaderFunction();
    }
    
    pass Final
    {	
		AlphaBlendEnable = false;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 RGBFinalComposition();
    }
}

technique MergeHSVImages
{
    pass P0
    {	
		AlphaBlendEnable = false; 
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 HSVMergePixelShaderFunction();
    }
    
    pass P1
    {	
		AlphaBlendEnable = true;
		SrcBlend = ONE;
		DestBlend = ONE;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 HSVMergePixelShaderFunction();
    }
    
    pass P2
    {	
		AlphaBlendEnable = true;
		SrcBlend = ONE;
		DestBlend = ONE;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 HSVMergePixelShaderFunction();
    }
    
    pass P3
    {	
		AlphaBlendEnable = true;
		SrcBlend = ONE;
		DestBlend = ONE;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 HSVMergePixelShaderFunction();
    }
    
    pass P4
    {	
		AlphaBlendEnable = true;
		SrcBlend = ONE;
		DestBlend = ONE;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 HSVMergePixelShaderFunction();
    }
    
    pass Final
    {	
		AlphaBlendEnable = false;
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 HSVFinalComposition();
    }
}
