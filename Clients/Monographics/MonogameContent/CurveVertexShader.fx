matrix viewProj;
float lineRadius;

float curveTotalLength; //Total length of the curve
						// Per-curve instance data:
float4 CurveSegmentData[200]; // (x0, y0, normalized_distance_to_origin, theta (tangent to curve))
 
float texture_x_start = 0;  //Begin the texture at this normalized distance from the start
float texture_x_end = 1;	//End the texture at this normalized distance from the end


struct CURVE_VS_INPUT
{
	float4 pos : POSITION;
	float2 vertRhoTheta : NORMAL;
	float curvesegmentIndex : TEXCOORD0;
};

struct CURVE_VS_OUTPUT
{
	float4 position : POSITION;
	float3 polar : TEXCOORD0;
	float2 posModelSpace : TEXCOORD1;
	float2 tex   : TEXCOORD2;
};


CURVE_VS_OUTPUT CurveVertexShader(CURVE_VS_INPUT In)
{
	CURVE_VS_OUTPUT Out = (CURVE_VS_OUTPUT)0;
	float4 pos = In.pos; //Position on the line, either along the center or the edge

	float x0 = CurveSegmentData[In.curvesegmentIndex].x; //Position of the control point in world space
	float y0 = CurveSegmentData[In.curvesegmentIndex].y;
	float distanceToOriginNormalized = CurveSegmentData[In.curvesegmentIndex].z; //Distance to the origin of the polyline in world space
	float tangent_theta = CurveSegmentData[In.curvesegmentIndex].w; //Tangent to the polyline at this control point
	float theta = tangent_theta;// +(3.14159 / 2.0); //Adjust the tangent 90 degrees so we rotate our verticies a lineradius distance away from the control point
	float vert_distance_from_center_normalized = In.vertRhoTheta.x;
	// Scale X by lineRadius, and translate X by rho, in worldspace
	// based on what part of the line we're on

	// Always scale Y by lineRadius regardless of what part of the line we're on
	pos.y *= lineRadius;

	// World matrix is rotate(theta) * translate(p0)
	matrix worldMatrix =
	{
		cos(theta), sin(theta), 0, 0,
		-sin(theta), cos(theta), 0, 0,
		0, 0, 1, 0,
		x0, y0, 0, 1
	};

	Out.position = mul(mul(pos, worldMatrix), viewProj);

	Out.polar = float3(In.vertRhoTheta, distanceToOriginNormalized);

	Out.posModelSpace.xy = float2(curveTotalLength * distanceToOriginNormalized, pos.y);
	 
	Out.tex = float2((distanceToOriginNormalized - texture_x_start) / (texture_x_end - texture_x_start),
		(-In.pos.y + 1) / 2.0);

	return Out;
}