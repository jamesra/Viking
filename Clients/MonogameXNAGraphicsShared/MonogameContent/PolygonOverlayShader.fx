#include "HSLRGBLib.fx"
#include "OverlayShaderShared.fx"

//The convention for annotation textures is that they built from two 8-bit images, one image is loaded to the RGB coordinates of the texture.
//The other image is loaded into the alpha channel.
//The verticies contain an RGB color which is converted to HSL space. 

//The alpha channel of the texture indicates whether the pixel is part of the annotation or not.  The alpha value is only used for this purpose
//The RGB component of the texture indicates the saturation value of the pixel.
//The program determines Saturation via converting the RGB color attribute of the vertex.
//The program determines the hue via converting the RGB color attribute of the vertex.
//The alpha channel of vertex color indicates how much the texture value is blended with the background value.

 
struct PolygonVertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float4 HSLColor : COLOR0;
};


struct PixelShaderInput
{
    float4 Position : POSITION0;
    float4 HSLColor : COLOR0;
    float2 ScreenTexCoord : SV_Position;
};
 
struct PixelShaderOutput
{
    float4 Color : COLOR0;
    float Depth : DEPTH0;
};

VertexShaderOutput PolygonVertexShaderFunction(PolygonVertexShaderInput input)
{
    VertexShaderOutput output; 
    output.Position = mul(input.Position, mWorldViewProj);
    output.HSLColor = input.Color;

    return output;
}
 
PixelShaderOutput ColorPolygonOverBackgroundLumaPixelShaderFunction(PixelShaderInput input)
{
    PixelShaderOutput output;
    output.Depth = 0.5; 

    float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));
    output.Color = BlendHSLColorOverBackground(input.HSLColor, RGBBackgroundColor, InputLumaAlpha);
    output.Color.a = input.HSLColor.a;

    return output;
}

technique ColorPolygonOverBackgroundLumaEffect
{
    pass
    {
        VertexShader = compile vs_4_0 PolygonVertexShaderFunction();
        PixelShader = compile ps_4_0 ColorPolygonOverBackgroundLumaPixelShaderFunction();
    }

}