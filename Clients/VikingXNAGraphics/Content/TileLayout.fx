float4x4 mWorldViewProj;

//int DownsampleLogBaseTwo; //The log base 2 of the downsample level of the texture being rendered, so downsample by 1 = 1, 2 = 2, 4 = 3, 8 = 4, 16 = 5, etc...

//OK, this should sample a texture that is an alpha8 texture
uniform const texture Texture;

//Channel Color
float4 TileColor;

float TileHue;

//Display window of the texture sample, in texture units (0 to 1).  A sample at DisplayMin shows black and one at DisplayMax shows white.
//Ordinary tiles use 0 to 1, which leaves them unchanged.  Crushed tiles hold codes, so the window expands them.
float DisplayMin = 0;
float DisplayMax = 1;

//Gamma applied to the windowed value: shown = value ^ (1 / DisplayGamma).  Above 1 lifts the dark and middle tones, 1 leaves the window linear.
float DisplayGamma = 1;

float ApplyDisplayWindow(float sampleValue)
{
	float windowed = saturate((sampleValue - DisplayMin) / max(DisplayMax - DisplayMin, 0.000001));
	return pow(windowed, 1.0 / max(DisplayGamma, 0.01));
}

uniform const sampler TextureSampler : register(s0) = sampler_state
{
	Texture = (Texture);
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

struct TileBlendPixelShaderOutput
{
	float4 Color : COLOR0; 
	float  Depth  : DEPTH0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
	
    VertexShaderOutput output; 
    output.TexCoord = input.TexCoord;

    output.Position = mul(input.Position, mWorldViewProj);
	
    return output;
}

TileBlendPixelShaderOutput TileBlendToGreyscalePixelShaderFunction(VertexShaderOutput input)
{
    //Shade the texture according to the color parameter
	TileBlendPixelShaderOutput output; 
	float XDist;
	float YDist;
    float4 color = tex2D(TextureSampler, input.TexCoord); //Input is a luminance texture
	float shown = ApplyDisplayWindow(color.a);
	output.Color.r = shown;
	output.Color.g = shown;
	output.Color.b = shown;
	output.Color.a = 1;

	XDist = (input.TexCoord.x - 0.5);
	YDist = (input.TexCoord.y - 0.5);

	output.Depth = (XDist * XDist) + (YDist * YDist);
	
    return output; 
}

TileBlendPixelShaderOutput TileBlendToHSVPixelShaderFunction(VertexShaderOutput input)
{
	TileBlendPixelShaderOutput output; 
	float XDist;
	float YDist;

	XDist = (input.TexCoord.x - 0.5);
	YDist = (input.TexCoord.y - 0.5);

	output.Depth = (XDist * XDist) + (YDist * YDist);
	//output.Depth = input.TexCoordDistanceFromCenter.x + input.TexCoordDistanceFromCenter.y;

    //Shade the texture according to the color parameter
	
    float4 Color = tex2D(TextureSampler, input.TexCoord);

	float alpha; 
	float beta; 

	float Hue; 
	float Saturation; 
	float Value; 

	//Convert to color from greyscale and write to output
	float shown = ApplyDisplayWindow(Color.a);
	Color.r = shown * TileColor.r;
	Color.g = shown * TileColor.g;
	Color.b = shown * TileColor.b;

	float maxC = max(Color.r, Color.g); 
	maxC = max(maxC, Color.b); 
	float minC = min(Color.r, Color.g); 
	minC = min(minC, Color.b); 

	float Chroma = maxC - minC; 
	
	Value = maxC;
	if(Value > 0 && Chroma > 0)
	{
		Saturation = Chroma / Value; 
	}
	else
	{
		Saturation = Value; 
	}

	output.Color.r = TileHue;
	output.Color.g = Chroma;
	output.Color.b = Value;
	output.Color.a = 1;

	return output;
}

technique TileLayoutToGreyscaleEffect
{
    pass
    {
        // TODO: set renderstates here.
		ZEnable = true;  
        ZWriteEnable = true;  

		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 TileBlendToGreyscalePixelShaderFunction();
    }
}

technique TileLayoutToHSVEffect
{
    pass
    {
        // TODO: set renderstates here.
		ZEnable = true;  
        ZWriteEnable = true;  

		VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 TileBlendToHSVPixelShaderFunction();
    }
}
