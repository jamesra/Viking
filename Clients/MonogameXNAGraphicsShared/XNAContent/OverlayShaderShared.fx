float4x4 mWorldViewProj;

uniform const float2 RenderTargetSize;

//OK, this should sample a texture
uniform const texture BackgroundTexture;
uniform const texture AnnotationTexture;
 
uniform const sampler BackgroundTextureSampler : register(s0) = sampler_state
{
    Texture = (BackgroundTexture);
    MipFilter = POINT;
    MinFilter = POINT;
    MagFilter = POINT;
};

uniform const sampler AnnotationTextureSampler : register(s1) = sampler_state
{
    Texture = (AnnotationTexture);
    MipFilter = POINT;
    MinFilter = LINEAR;
    MagFilter = POINT;
}; 

uniform const float InputLumaAlpha = 1.0f; //Defines how we weight blending the input and existing background luma values by default.

//The convention for annotation textures is that they built from two 8-bit images, one image is loaded to the RGB coordinates of the texture.
//The other image is loaded into the alpha channel.
//The verticies contain an RGB color which is converted to HSL space. 

//The alpha channel of the texture indicates whether the pixel is part of the annotation or not.  The alpha value is only used for this purpose
//The RGB component of the texture indicates the saturation value of the pixel.
//The program determines Saturation via converting the RGB color attribute of the vertex.
//The program determines the hue via converting the RGB color attribute of the vertex.
//The alpha channel of vertex color indicates how much the texture value is blended with the background value.

// 6/26/2020 Well after I wrote this I decided I never use per-vertex coloring and it was also nice to set a uniform color via an effect.
//Set to the desired foreground color of the annotation if using effect color and not vertex/texture color
float4 AnnotationHSLColor;
