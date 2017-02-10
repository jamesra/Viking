uniform const float4x4 mWorldViewProj;

//These textures are in HSV space, except that Hue is indicated by two components, alpha and beta.  The only value we need from the textures is saturation.  Hue is constant for each channel
//Layout is as follows:
//  r = Hue
//  g = Value
//	b = Saturation

//Only have variables above three because you shouldn't run this if you don't have two channels to blendfs

#include "HSLRGBLib.fx"

int NumTextures;

uniform const float ChannelHueAlpha[6];
uniform const float ChannelHueBeta[6] ;
uniform const float4 ChannelRGBColor[6]; 
uniform const float4 ChannelRGBColorTotal;

uniform const texture Texture1;
uniform const texture Texture2;
uniform const texture Texture3;
uniform const texture Texture4;
uniform const texture Texture5;

uniform const sampler ChannelSampler[5] = 
{
	sampler_state
	{
		Texture = (Texture1);
		MipFilter = NONE;
	},
	sampler_state
	{
		Texture = (Texture2);
		MipFilter = NONE;
	},
	sampler_state
	{
		Texture = (Texture3);
		MipFilter = NONE;
	},
	sampler_state
	{
		Texture = (Texture4);
		MipFilter = NONE;
	},
	sampler_state
	{
		Texture = (Texture5);
		MipFilter = NONE;
	}
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

PixelShaderOutput HSVMergePixelShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output;
	float4 ChannelColor;
	float3 Final = {0,0,0};
	float maxSat = 0; 
	//float maxVal = 0; 
	
	for(int i = 0; i < NumTextures; i++)
	{
		ChannelColor = tex2D(ChannelSampler[i], input.TexCoord);

		float3 HS = {ChannelHueAlpha[i] * ChannelColor.g,
					 ChannelHueBeta[i] * ChannelColor.g,
					 ChannelColor.b};
		maxSat = maxSat + ChannelColor.g; //max(maxSat, ChannelColor.g); 
		//maxVal = max(maxVal, ChannelColor.b); 

		Final = Final + HS;
	}
		
	//Convert Final to an HSV image
	float2 Sat = {Final[0], Final[1]};
	output.Color.r = saturate((atan2(Final[1], Final[0]) / 6.28318) + 0.5);
	output.Color.g = length(Sat) / maxSat; 
	output.Color.b = Final[2] / NumTextures; 
	output.Color.a = 1;

	return output;
}

PixelShaderOutput RGBMergePixelShaderFunction(VertexShaderOutput input)
{
	PixelShaderOutput output;
	float4 ChannelColor;
	float4 Final = {0,0,0,0};
	float maxSat = 0; 
	float maxVal = 0; 
	
	for(int i = 0; i < NumTextures; i++)
	{
		ChannelColor = tex2D(ChannelSampler[i], input.TexCoord.xy);

		float4 HS = {ChannelRGBColor[i].r * ChannelColor.r,
					 ChannelRGBColor[i].g * ChannelColor.g,
					 ChannelRGBColor[i].b * ChannelColor.b,
					 ChannelRGBColor[i].a * ChannelColor.a}; 

		Final = Final + HS;
	}
	
	Final = Final / ChannelRGBColorTotal;

	//Make sure we preserve the color values
	//Final[3] = 1;

	if(ChannelRGBColorTotal[0] == 0)
		Final[0] = 0; 

	if(ChannelRGBColorTotal[1] == 0)
		Final[1] = 0; 

	if(ChannelRGBColorTotal[2] == 0)
		Final[2] = 0;

	if(ChannelRGBColorTotal[3] == 0)
		Final[3] = 0;

	output.Color = RGBToHCL(Final); 

	//Convention, black with alpha=0 will be ignored in later stages
	//any other values with alpha=0 will use background luma and foreground hue
	//any values with alpha = 0 or a mix will blend foreground and background value proportionately.f
	
	return output;
}



technique MergeRGBImages
{
    pass
    {	
		AlphaBlendEnable = false; 
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader =  compile ps_4_0 RGBMergePixelShaderFunction();
    }
}

technique MergeHSVImages
{
    pass
    {	
		AlphaBlendEnable = false; 
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader =  compile ps_4_0 HSVMergePixelShaderFunction();
    }
}
