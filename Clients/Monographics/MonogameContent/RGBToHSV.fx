float4x4 mWorldViewProj;

//Channel Color
float4 ChannelColor; 

float ChannelHue; 

//OK, this should sample a texture
uniform const texture Texture;

//If adding multiple HSV images this value should be set to the number of images to be combined
float NumberOfImages;

uniform const sampler TextureSampler : register(s0) = sampler_state
{
	Texture = (Texture);
	MipFilter = None;
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

struct RGBToHSVPixelShaderOutput
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

RGBToHSVPixelShaderOutput RGBToHSVPixelShaderFunction(VertexShaderOutput input)
{
    //Shade the texture according to the color parameter
	RGBToHSVPixelShaderOutput output; 
    float4 Color = tex2D(TextureSampler, input.TexCoord);

//	float alpha; 
//	float beta; 

	float Hue; 
	float Saturation; 
	float Value; 

	//Convert to HSV and write to output
	Color.r = Color.a * ChannelColor.r;
	Color.g = Color.g * ChannelColor.g; 
	Color.b = Color.b * ChannelColor.b; 
	
//	alpha = 0.5 * ( (2 * ChannelColor.r) - ChannelColor.g - ChannelColor.b);
//	beta = 0.8666 * (ChannelColor.g - ChannelColor.b); //sqrt(3) / 2 = 0.8666
//	Hue = atan2(beta, alpha);

	Value = (0.3 * Color.r) + (0.59 * Color.g) + (0.11 * Color.b);
	Hue = ChannelHue;
	Saturation = Hue / Value; 

	output.Color.r = Hue;
	output.Color.g = Value;
	output.Color.b = Saturation;
	output.Color.a = 1;

	return output; 
}

RGBToHSVPixelShaderOutput HSVAdditionPixelShaderFunction(VertexShaderOutput input)
{
    //Shade the texture according to the color parameter
	RGBToHSVPixelShaderOutput output; 
    float4 Color = tex2D(TextureSampler, input.TexCoord);

//	float alpha; 
//	float beta; 

	float Hue; 
	float Saturation; 
	float Value; 

	//Convert to HSV and write to output
	Color.r = Color.a * ChannelColor.r;
	Color.g = Color.g * ChannelColor.g; 
	Color.b = Color.b * ChannelColor.b; 
	
//	alpha = 0.5 * ( (2 * ChannelColor.r) - ChannelColor.g - ChannelColor.b);
//	beta = 0.8666 * (ChannelColor.g - ChannelColor.b); //sqrt(3) / 2 = 0.8666
//	Hue = atan2(beta, alpha);

	Value = (0.3 * Color.r) + (0.59 * Color.g) + (0.11 * Color.b);
	Hue = ChannelHue;
	Saturation = Hue / Value; 

	output.Color.r = Hue;
	output.Color.g = Value;
	output.Color.b = Saturation;
	output.Color.a = 1;

	return output; 
}

technique RGBToHSVEffect
{
    pass
    {	
		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 RGBToHSVPixelShaderFunction();
    }
}

technique HSVAdditionEffect
{
	pass
	{
		ZEnable = false;  
        ZWriteEnable = false;  
		AlphaBlendEnable = false; 
		SrcBlend = One; 
		DestBlend = One; 

		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 HSVAdditionPixelShaderFunction();
	}
}