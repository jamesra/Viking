float4x4 World;
float4x4 View;
float4x4 Projection;

// TODO: add effect parameters here.
float4 ChannelColor;

//OK, this should sample a texture
uniform const texture Texture;

uniform const sampler TextureSampler : register(s0) = sampler_state
{
	Texture = (Texture);
	MipFilter = Linear;
	MinFilter = Linear;
	MagFilter = Linear;
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

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output; 
    output.TexCoord = input.TexCoord;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    //Shade the texture according to the color parameter
    float4 color = tex2D(TextureSampler, input.TexCoord);
	color.r = color.r * ChannelColor.r;
	color.g = color.g * ChannelColor.g;
	color.b = color.b * ChannelColor.b; 
	color.a = color.a * ChannelColor.a; 
    return color; 
}

technique ChannelEffect
{
    pass
    {
        // TODO: set renderstates here.
		//CullMode = CW;
		//AlphaBlendEnable = true;
		//SrcBlend = SrcAlpha;
		//DestBlend = InvSrcAlpha;
		//BlendOp = Add;
		
		VertexShader = compile vs_1_1 VertexShaderFunction();
        PixelShader = compile ps_1_1 PixelShaderFunction();
    }
}
