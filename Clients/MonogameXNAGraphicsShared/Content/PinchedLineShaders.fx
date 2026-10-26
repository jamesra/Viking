// Pinched lines: a link between two location markers that narrows from each marker's width to a waist.
// Shared by RoundLineHSV.fx. Requires LineCurveCommon.fx, LineVertexShader.fx and LineCurveHSVPixelShaders.fx first.
//
// Positions are in the line's model space: x runs from the start marker's centre (0) to the end marker's centre
// (the line length), y is the distance from the line, both in world units. The half width at x is
// Each side eases on a raised cosine from its marker radius down to the neck: flat at the marker, flat at the neck.
// Far apart, the neck is the waist w, reached one marker diameter from each centre, with a straight run between.
// Close together, the neck is where straight tapers of that rate would meet, (rA - kA m) at m with
//   m = (rA - rB + kB L) / (kA + kB),  kA = (rA - w) / (2 rA),  kB = (rB - w) / (2 rB),
// so close markers barely narrow, and the two curves meet at a rounded neck instead of a corner.
// With pinchedDrawCaps off nothing is drawn inside either marker's circle.
// Keep LocationLinkView.HalfWidthAt in step with PinchedHalfWidth.

// Per-line shape, indexed like instanceData: (start radius, end radius, waist half width, unused).
float4 instanceShape[200];

// 1 draws a round end the size of each marker under it, 0 draws nothing beyond the marker centres.
float pinchedDrawCaps = 0;

// Opacity at the waist as a fraction of the opacity next to the markers. Alpha runs from alpha at a marker's edge to
// alpha * pinchedWaistOpacity one marker radius further on, where the link has narrowed to its waist, linearly in the
// fade depth (see PinchedFadeDepth). 0 makes the middle of a long link invisible.
float pinchedWaistOpacity = 0.5;

// Width in screen pixels over which the outline fades from opaque to clear, half inside it and half outside.
static const float pinchedFeatherPixels = 3;

// A border drawn just inside the outline so the link stands out from the image under it. The colour is plain RGB, not
// HCL: the HCL blend keeps the background's luma, so black came out as the background and white as a grey. The width
// is in screen pixels, and 0 draws no border.
float4 pinchedBorderColor = float4(0, 0, 0, 1);
float pinchedBorderPixels = 0;

// A shadow behind the link, outside its outline, so the link stands out from the image under it. The colour is plain RGB.
// It is strongest at the outline and fades to nothing pinchedShadowPixels screen pixels out. Its opacity follows the
// link's: lineColor.a, faded toward the waist the same way. 0 pixels draws no shadow.
float4 pinchedShadowColor = float4(0, 0, 0, 1);
float pinchedShadowPixels = 0;

// pinchedShadowPixels in world units at the current zoom. The vertex shader widens each line by this much so the
// shadow has room outside the widest part of the outline.
float pinchedShadowWorld = 0;

// How much of the fill's luma is the link colour's rather than the image's. Fixed at the blend links used when they
// were drawn at alpha 64, so raising the link's opacity makes it cover more without changing its colour.
static const float pinchedFillLumaAlpha = 0.75;

//NOTE: THE VERTEX SHADER OUTPUT AND PIXEL SHADER INPUT MUST MATCH EXACTLY WHEN USING MONOGAME
struct PINCHED_VS_OUTPUT
{
	float4 position : POSITION;
	float3 polar : TEXCOORD0;
	float2 posModelSpace : TEXCOORD1;
	float2 tex : TEXCOORD2;
	float4 shape : TEXCOORD3; // (rA, rB, waist half width, line length)
};

struct PINCHED_PS_INPUT
{
	float4 ScreenTexCoord : SV_Position;
	float3 polar : TEXCOORD0;
	float2 posModelSpace : TEXCOORD1;
	float2 tex : TEXCOORD2;
	float4 shape : TEXCOORD3;
};

// Same as LineVertexShader, but each line is scaled by the larger of its two radii, plus room for the shadow, instead of
// lineRadius.
PINCHED_VS_OUTPUT PinchedLineVertexShader(LINE_VS_INPUT In)
{
	PINCHED_VS_OUTPUT Out;
	float4 pos = float4(In.pos, 1);
	float distanceToOriginNormalized = In.vertScaleTrans.y;
	int index = int(In.instanceIndex);
	float x0 = instanceData[index].x;
	float y0 = instanceData[index].y;
	float rho = instanceData[index].z;
	float theta = instanceData[index].w;
	float4 shape = instanceShape[index];
	float radius = max(shape.x, shape.y) + pinchedShadowWorld;

	pos.x *= (In.vertScaleTrans.x * radius);
	pos.x += (distanceToOriginNormalized * rho);
	pos.y *= radius;

	matrix worldMatrix =
	{
		cos(theta), sin(theta), 0, 0,
		-sin(theta), cos(theta), 0, 0,
		0, 0, 1, 0,
		x0, y0, 0, 1
	};

	Out.position = mul(mul(pos, worldMatrix), viewProj);
	Out.tex = float2(distanceToOriginNormalized, (-In.pos[1] + 1) / 2.0);
	Out.posModelSpace.xy = pos.xy;
	Out.polar.xy = In.vertRhoTheta;
	Out.polar.z = distanceToOriginNormalized;
	Out.shape = float4(shape.x, shape.y, shape.z, rho);
	return Out;
}

// 1 at t = 0 and 0 at t = 1, with zero slope at both ends.
float PinchedEase(float t)
{
	return 0.5 * (1 + cos(PI * saturate(t)));
}

float PinchedHalfWidth(float x, float4 shape)
{
	float rA = shape.x;
	float rB = shape.y;
	float w = min(shape.z, min(rA, rB));
	float L = shape.w;
	float kA = (rA - w) / (2 * rA);
	float kB = (rB - w) / (2 * rB);

	float neck = w;
	float taperA = 2 * rA;
	float taperB = 2 * rB;
	if (kA + kB > 0)
	{
		float m = clamp((rA - rB + (kB * L)) / (kA + kB), 0, L);
		float meet = rA - (kA * m);
		if (meet > w)
		{
			neck = meet;
			taperA = m;
			taperB = L - m;
		}
	}

	if (x < taperA)
		return neck + ((rA - neck) * PinchedEase(x / taperA));
	if (x > L - taperB)
		return neck + ((rB - neck) * PinchedEase((L - x) / taperB));
	return neck;
}

// How far x is into the fade from the nearer marker: 0 at the marker's edge and inside it, 1 one marker radius further on,
// where a long link reaches its waist. Each end is measured by its own radius, so a small marker's end starts at full
// opacity as a large one's does. Keep LocationLinkView.FadeDepthAt in step.
float PinchedFadeDepth(float x, float4 shape)
{
	float L = shape.w;
	return saturate(min((x - shape.x) / max(shape.x, 1e-6), (L - x - shape.y) / max(shape.y, 1e-6)));
}

// Signed distance to the outline in world units, positive inside. fadeDepth is PinchedFadeDepth at the pixel, 0 beyond
// the line's ends.
float PinchedDistance(PINCHED_PS_INPUT input, out float fadeDepth)
{
	float x = input.posModelSpace.x;
	float y = abs(input.posModelSpace.y);
	float4 shape = input.shape;
	float L = shape.w;
	fadeDepth = 0;

	float fromA = length(input.posModelSpace);
	float fromB = length(input.posModelSpace - float2(L, 0));

	if (pinchedDrawCaps > 0.5)
	{
		if (x < 0)
			return shape.x - fromA;
		if (x > L)
			return shape.y - fromB;
	}
	else if (x < 0 || x > L)
	{
		return -1e6;
	}

	float h = PinchedHalfWidth(x, shape);
	fadeDepth = PinchedFadeDepth(x, shape);
	float d = h - y;

	// Without caps the markers cover their own circles, so keep the link out of them.
	if (pinchedDrawCaps <= 0.5)
		d = min(d, min(fromA - shape.x, fromB - shape.y));
	return d;
}

Color_Depth_Output DepthOnlyPinchedShader(PINCHED_PS_INPUT input)
{
	float fadeDepth;
	clip(PinchedDistance(input, fadeDepth));

	Color_Depth_Output output;
	output.Color = float4(0, 0, 0, 0);
	output.Depth = input.polar.z;
	return output;
}

Color_Depth_Output MyPSPinchedHSV(PINCHED_PS_INPUT input)
{
	float fadeDepth;
	float d = PinchedDistance(input, fadeDepth);
	// Feather the outline over pinchedFeatherPixels screen pixels, centred on it, so the edge looks soft.
	float coverage = saturate((d / (pinchedFeatherPixels * max(fwidth(d), 1e-6))) + 0.5);

	// Full opacity next to the markers, fading linearly to pinchedWaistOpacity of it at the waist.
	float fade = lerp(1, pinchedWaistOpacity, fadeDepth);
	float fillAlpha = saturate(lineColor.a * fade) * coverage;

	// The shadow lies outside the outline. x runs along the line, so its change per pixel is the world size of a pixel
	// in any direction; d's own change per pixel jumps where the outline meets a marker.
	float shadowAlpha = 0;
	if (pinchedShadowPixels > 0)
	{
		float2 xStep = float2(ddx(input.posModelSpace.x), ddy(input.posModelSpace.x));
		float pixelsOutside = -d / max(length(xStep), 1e-6);
		float falloff = saturate(1 - (pixelsOutside / pinchedShadowPixels));
		falloff *= falloff;

		// Without caps nothing is drawn inside the markers, and that includes the shadow.
		float L = input.shape.w;
		if (pinchedDrawCaps <= 0.5 &&
			(length(input.posModelSpace) < input.shape.x || length(input.posModelSpace - float2(L, 0)) < input.shape.y))
		{
			falloff = 0;
		}

		shadowAlpha = saturate(lineColor.a * fade) * falloff * (1 - coverage);
	}

	// The fill is drawn over the shadow.
	float alpha = fillAlpha + (shadowAlpha * (1 - fillAlpha));
	clip(alpha - 0.001);

	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));

	Color_Depth_Output output;
	float4 fillColor = BlendHCLColorOverBackground(lineColor, RGBBackgroundColor, pinchedFillLumaAlpha);

	// d is a distance in world units, so dividing by its change per pixel gives pixels in from the outline. The border
	// is full strength out to pinchedBorderPixels in from it, then fades over one pixel into the fill.
	float pixelsFromOutline = d / max(fwidth(d), 1e-6);
	float borderMix = pinchedBorderPixels > 0 ? 1 - saturate(pixelsFromOutline - pinchedBorderPixels + 0.5) : 0;
	float3 linkColor = lerp(fillColor.rgb, pinchedBorderColor.rgb, borderMix);

	output.Color.rgb = ((linkColor * fillAlpha) + (pinchedShadowColor.rgb * shadowAlpha * (1 - fillAlpha))) / alpha;
	output.Color.a = alpha;
	output.Depth = 0;
	return output;
}
