// Instanced circle overlay: solid and textured circles in a single batched draw.
// Input: unit square vertices (-1..1) with instance index.

#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0
#define PS_SHADERMODEL ps_4_0
#endif

#include "../../MonogameXNAGraphicsShared/Content/HSLRGBLib.fx"
#include "../../MonogameXNAGraphicsShared/Content/OverlayShaderShared.fx"

float4x4 viewProj;
float4 CircleData[200];   // (centerX, centerY, radius, textureLayerIndex)
float4 CircleColors[200]; // (H, C, L, A) HCL

// Atlas: layers stacked vertically; sample y = (layer + v) * InvCircleTextureLayers
texture CircleTextures;
float InvCircleTextureLayers;
sampler CircleTextureSampler : register(s2) = sampler_state
{
    Texture = (CircleTextures);
    MipFilter = POINT;
    MinFilter = LINEAR;
    MagFilter = LINEAR;
    AddressU = CLAMP;
    AddressV = CLAMP;
};

struct CIRCLE_VS_INPUT
{
    float4 pos : POSITION0;
    float instanceIndex : TEXCOORD0;
};

struct CIRCLE_VS_OUTPUT
{
    float4 position : POSITION0;
    float2 CenterDistance : TEXCOORD0;
    float4 HSLColor : TEXCOORD1;
    float2 TexCoord : TEXCOORD2;
    float textureLayerIndex : TEXCOORD3;
#if OPENGL
    float4 positionCopy : TEXCOORD4;
#endif
};

CIRCLE_VS_OUTPUT CircleInstancedVertexShader(CIRCLE_VS_INPUT In)
{
    CIRCLE_VS_OUTPUT Out;
    int index = (int)In.instanceIndex;
    float2 center = CircleData[index].xy;
    float radius = CircleData[index].z;
    float layerIndex = CircleData[index].w;

    float2 worldXY = center + radius * In.pos.xy;
    float4 worldPos = float4(worldXY, 0, 1);
    Out.position = mul(worldPos, viewProj);

    Out.CenterDistance = In.pos.xy;
    Out.HSLColor = CircleColors[index];
    Out.TexCoord = (In.pos.xy + 1) * 0.5;
    Out.textureLayerIndex = layerIndex;
#if OPENGL
    Out.positionCopy = Out.position;
#endif
    return Out;
}

struct CIRCLE_PS_INPUT
{
#if OPENGL
    float4 position : TEXCOORD4;
#else
    float4 position : SV_Position;
#endif
    float2 CenterDistance : TEXCOORD0;
    float4 HSLColor : TEXCOORD1;
    float2 TexCoord : TEXCOORD2;
    float textureLayerIndex : TEXCOORD3;
};

// Alpha technique: clip outside circle, sample texture, multiply by HCL color
float4 CircleInstancedPixelShaderAlpha(CIRCLE_PS_INPUT input) : SV_Target
{
    float distSq = dot(input.CenterDistance, input.CenterDistance);
    clip(1 - distSq);

    float2 texCoord = input.TexCoord;
    float2 atlasUV = float2(texCoord.x, (input.textureLayerIndex + texCoord.y) * InvCircleTextureLayers);
    float4 texColor = tex2D(CircleTextureSampler, atlasUV);
    clip(texColor.a - 0.0001);

    float4 hcl = input.HSLColor;
    float4 rgb = HCLToRGB(hcl);
    float4 finalColor = float4(rgb.rgb * texColor.rgb, rgb.a * texColor.a);
    return finalColor;
}

// Luma technique: same circle + texture, blend over background
float4 CircleInstancedPixelShaderLuma(CIRCLE_PS_INPUT input) : SV_Target
{
    float distSq = dot(input.CenterDistance, input.CenterDistance);
    clip(1 - distSq);

    float2 texCoord = input.TexCoord;
    float2 atlasUV = float2(texCoord.x, (input.textureLayerIndex + texCoord.y) * InvCircleTextureLayers);
    float4 texColor = tex2D(CircleTextureSampler, atlasUV);
    clip(texColor.a - 0.0001);

    float4 hcl = input.HSLColor;
    float2 screenCoord = input.position.xy / (RenderTargetSize.xy - 1);
    float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, screenCoord);
    float4 finalColor = BlendHCLColorOverBackground(hcl, RGBBackgroundColor, InputLumaAlpha);
    return finalColor;
}

technique CircleInstancedAlpha
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CircleInstancedVertexShader();
        PixelShader = compile PS_SHADERMODEL CircleInstancedPixelShaderAlpha();
    }
}

technique CircleInstancedLuma
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CircleInstancedVertexShader();
        PixelShader = compile PS_SHADERMODEL CircleInstancedPixelShaderLuma();
    }
}
