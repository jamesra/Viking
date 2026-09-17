float4x4 mWorldViewProj;

//int DownsampleLogBaseTwo; //The log base 2 of the downsample level of the texture being rendered, so downsample by 1 = 1, 2 = 2, 4 = 3, 8 = 4, 16 = 5, etc...

//OK, this should sample a texture that is an alpha8 texture
uniform const texture Texture;

//Channel Color
float4 TileColor;

float TileHue;

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
	output.Color.r = color.a;
	output.Color.g = color.a;
	output.Color.b = color.a; 
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
	Color.r = Color.a * TileColor.r;
	Color.g = Color.a * TileColor.g; 
	Color.b = Color.a * TileColor.b;

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

		VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 TileBlendToGreyscalePixelShaderFunction();
    }
}

technique TileLayoutToHSVEffect
{
    pass
    {
        // TODO: set renderstates here.
		ZEnable = true;  
        ZWriteEnable = true;  

		VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 TileBlendToHSVPixelShaderFunction();
    }
}
