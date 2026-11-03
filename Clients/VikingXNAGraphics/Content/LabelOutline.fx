// Draws a label cached as a LabelOutlineMap (see LabelOutlineMap.cs): the text, a border of any width and an optional soft shadow,
// coloured at draw time. Pixel shader only: SpriteBatch supplies the vertex shader.
//
// The map's R channel is the text's coverage. Its G channel is 1 on the text and falls linearly to 0 at "range" map pixels away, so the
// screen-space slope of G is the fall per screen pixel, however far the label is scaled. Dividing the fall already made by that slope
// gives the distance from the text in screen pixels, which is what the border and shadow widths are in.
//
// The vertex color is the text color; its alpha scales everything drawn.

#if OPENGL
	#define SV_POSITION POSITION
	#define PS_SHADERMODEL ps_3_0
#else
	#define PS_SHADERMODEL ps_4_0
#endif

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
	Texture = <SpriteTexture>;
};

float3 OutlineColor = float3(0, 0, 0);
// Screen pixels from the text's edge to the middle of the border's fade.
float OutlineWidth = 1.5;
// Screen pixels the border takes to fade out, centred on OutlineWidth. 1 is a crisp edge; larger blurs the border.
float OutlineBlur = 2;

float3 ShadowColor = float3(0, 0, 0);
// 0 draws no shadow.
float ShadowOpacity = 0;
// Where the shadow falls, in screen pixels (x right, y down).
float2 ShadowOffset = float2(1, 1);
// Screen pixels the shadow takes to fade out from the text's edge.
float ShadowBlur = 3;

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

float4 Over(float3 color, float alpha, float4 under)
{
	return float4(color * alpha, alpha) + under * (1 - alpha);
}

// withBorder: draw the shadow and border. withText: draw the text over them. A run of neighbouring labels (the digits of a number) draws
// every border first and every text after, so one label's border never covers another's text.
float4 Compose(VertexShaderOutput input, float withBorder, float withText)
{
	float4 map = tex2D(SpriteTextureSampler, input.TextureCoordinates);
	float g = map.g;
	float2 slope = float2(ddx(g), ddy(g));
	float perPixel = max(length(slope), 0.00001);

	// Screen pixels from the text's edge. Zero on the text, large where G has run out.
	float edgeDistance = (1 - g) / perPixel;
	float halfBlur = max(OutlineBlur, 1) * 0.5;
	float borderAlpha = 1 - smoothstep(OutlineWidth - halfBlur, OutlineWidth + halfBlur, edgeDistance);

	float2 uvPerX = ddx(input.TextureCoordinates);
	float2 uvPerY = ddy(input.TextureCoordinates);
	float2 shadowUV = input.TextureCoordinates - uvPerX * ShadowOffset.x - uvPerY * ShadowOffset.y;
	float shadowDistance = (1 - tex2D(SpriteTextureSampler, shadowUV).g) / perPixel;
	float shadowAlpha = ShadowOpacity * (1 - smoothstep(0, max(ShadowBlur, 1), shadowDistance));

	float opacity = input.Color.a;
	float4 result = float4(0, 0, 0, 0);
	result = Over(ShadowColor, shadowAlpha * opacity * withBorder, result);
	result = Over(OutlineColor, borderAlpha * opacity * withBorder, result);
	result = Over(input.Color.rgb, map.r * opacity * withText, result);
	return result;
}

float4 AllPS(VertexShaderOutput input) : COLOR { return Compose(input, 1, 1); }
float4 BorderPS(VertexShaderOutput input) : COLOR { return Compose(input, 1, 0); }
float4 TextPS(VertexShaderOutput input) : COLOR { return Compose(input, 0, 1); }

technique LabelOutlineDrawing
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL AllPS();
	}
};

technique LabelOutlineBorderOnly
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL BorderPS();
	}
};

technique LabelOutlineTextOnly
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL TextPS();
	}
};
