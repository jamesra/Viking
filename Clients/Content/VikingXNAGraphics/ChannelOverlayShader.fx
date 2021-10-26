#include "HSLRGBLib.fx"

float4x4 mWorldViewProj;

//OK, this should sample a texture
uniform const texture BackgroundTexture;
uniform const texture OverlayTexture;

							  

uniform const sampler BackgroundTextureSampler : register(s0) = sampler_state
{
	Texture = (BackgroundTexture);
	MipFilter = POINT;
	MinFilter = POINT;
	MagFilter = POINT;
};

uniform const sampler OverlayTextureSampler : register(s1) = sampler_state
{
	Texture = (OverlayTexture);
	MipFilter = POINT; 
	MinFilter = POINT; 
	MagFilter = POINT; 
};

// My shader requires a texture and verticies
struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
	

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



PixelShaderOutput HSOverBackgroundValuePixelShaderFunction(VertexShaderOutput input)
{
    //Shade the texture according to the color parameter
	PixelShaderOutput output; 

	float Hue; 
	float Saturation;
	float Value;

    float4 BackgroundColor = tex2D(BackgroundTextureSampler, input.TexCoord);
	float4 HSVForeground = tex2D(OverlayTextureSampler, input.TexCoord); 
	
	clip(any(HSVForeground) ? 1 : -1);
	
	Hue = HSVForeground.r;
	Saturation = HSVForeground.g;
	Value = BackgroundColor.b;
	//Value = (BackgroundColor.b * (1-HSVForeground.a)) + ((HSVForeground.b * HSVForeground.a));  //This should be a greyscale image, so any component will match the value

	float4 hsv = {Hue, Saturation, Value, HSVForeground.a};

	output.Color = HCLToRGB(hsv);

    return output;
}

PixelShaderOutput HSVOnlyPixelShaderFunction(VertexShaderOutput input)
{
    //Shade the texture according to the color parameter
	PixelShaderOutput output; 

	float Hue; 
	float Saturation;
	float Value;

	float Chroma; 
	float m;
	float HPrime; 
	float X; 
	
	float4 HSVForeground = tex2D(OverlayTextureSampler, input.TexCoord); 
	
	Hue = HSVForeground.r;
	Saturation = HSVForeground.g;
	Value = HSVForeground.b;  //This should be a greyscale image, so any component will match the value

	//Chroma = Value * Saturation;

	float4 HSVToRGBInput = {Hue, Saturation, Value, HSVForeground.a}; 

	output.Color = HCLToRGB(HSVToRGBInput); 

    return output;
}

PixelShaderOutput BackgroundValueOnlyPixelShaderFunction(VertexShaderOutput input)
{
    //Shade the texture according to the color parameter
	PixelShaderOutput output;
	
	output.Color = tex2D(BackgroundTextureSampler, input.TexCoord);

    return output;
}

technique HSOverBackgroundValueOverlayEffect
{
    pass
    {
        // TODO: set renderstates here.
		//CullMode = CW;
		//AlphaBlendEnable = true;
		//SrcBlend = SrcAlpha;
		//DestBlend = InvSrcAlpha;
		//BlendOp = Add;
		
		VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 HSOverBackgroundValuePixelShaderFunction();
    }

}

technique HSVOnlyOverlayEffect
{
    pass
    {
        // TODO: set renderstates here.
		//CullMode = CW;
		//AlphaBlendEnable = true;
		//SrcBlend = SrcAlpha;
		//DestBlend = InvSrcAlpha;
		//BlendOp = Add;
		
		VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 HSVOnlyPixelShaderFunction();
    }
}

technique BackgroundOnlyOverlayEffect
{
    pass
    {
        // TODO: set renderstates here.
		//CullMode = CW;
		//AlphaBlendEnable = true;
		//SrcBlend = SrcAlpha;
		//DestBlend = InvSrcAlpha;
		//BlendOp = Add;
		
		VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 BackgroundValueOnlyPixelShaderFunction();
    }
}
