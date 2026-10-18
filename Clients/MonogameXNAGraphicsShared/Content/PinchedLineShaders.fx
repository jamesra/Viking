// Pinched lines: a link between two location markers that narrows from each marker's width to a waist.
// Shared by RoundLineHSV.fx. Requires LineCurveCommon.fx, LineVertexShader.fx and LineCurveHSVPixelShaders.fx first.
//
// Positions are in the line's model space: x runs from the start marker's centre (0) to the end marker's centre
// (the line length), y is the distance from the line, both in world units. The half width at x is
//   h(x) = max(w, rA - kA * x, rB - kB * (L - x)),  kA = (rA - w) / (2 rA),  kB = (rB - w) / (2 rB)
// so each side narrows at a constant rate and reaches the waist w one marker diameter from that marker's centre.
// The outline is the union of the two funnels: markers closer than that meet before either reaches the waist.
// Keep LocationLinkView.HalfWidthAt in step with PinchedHalfWidth.

// Per-line shape, indexed like instanceData: (start radius, end radius, waist half width, unused).
float4 instanceShape[200];

// 1 draws a round end the size of each marker under it, 0 draws nothing beyond the marker centres.
float pinchedDrawCaps = 0;

// How much more opaque the line is at the waist than at full width: alpha * (1 + boost * squeeze).
float pinchedOpacityBoost = 1;

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

// Same as LineVertexShader, but each line is scaled by the larger of its two radii instead of lineRadius.
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
	float radius = max(shape.x, shape.y);

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

float PinchedHalfWidth(float x, float4 shape)
{
	float rA = shape.x;
	float rB = shape.y;
	float w = min(shape.z, min(rA, rB));
	float L = shape.w;
	float kA = (rA - w) / (2 * rA);
	float kB = (rB - w) / (2 * rB);
	return max(w, max(rA - (kA * x), rB - (kB * (L - x))));
}

// Signed distance to the outline in world units, positive inside. squeeze is 0 at full width and 1 at the waist.
float PinchedDistance(PINCHED_PS_INPUT input, out float squeeze)
{
	float x = input.posModelSpace.x;
	float y = abs(input.posModelSpace.y);
	float4 shape = input.shape;
	float L = shape.w;
	squeeze = 0;

	if (x < 0)
		return pinchedDrawCaps > 0.5 ? shape.x - length(input.posModelSpace) : -1e6;
	if (x > L)
		return pinchedDrawCaps > 0.5 ? shape.y - length(input.posModelSpace - float2(L, 0)) : -1e6;

	float h = PinchedHalfWidth(x, shape);
	float full = max(shape.x, shape.y);
	float w = min(shape.z, min(shape.x, shape.y));
	squeeze = full > w ? saturate((full - h) / (full - w)) : 0;
	return h - y;
}

Color_Depth_Output DepthOnlyPinchedShader(PINCHED_PS_INPUT input)
{
	float squeeze;
	clip(PinchedDistance(input, squeeze));

	Color_Depth_Output output;
	output.Color = float4(0, 0, 0, 0);
	output.Depth = input.polar.z;
	return output;
}

Color_Depth_Output MyPSPinchedHSV(PINCHED_PS_INPUT input)
{
	float squeeze;
	float d = PinchedDistance(input, squeeze);
	// Fade the outline over about one screen pixel.
	float coverage = saturate((d / max(fwidth(d), 1e-6)) + 0.5);
	clip(coverage - 0.001);

	float alpha = saturate(lineColor.a * (1 + (pinchedOpacityBoost * squeeze)));
	float4 RGBBackgroundColor = tex2D(BackgroundTextureSampler, ((input.ScreenTexCoord.xy) / (RenderTargetSize.xy - 1)));

	Color_Depth_Output output;
	output.Color = BlendHCLColorOverBackground(lineColor, RGBBackgroundColor, 1.0f - alpha);
	output.Color.a = alpha * coverage;
	output.Depth = 0;
	return output;
}
