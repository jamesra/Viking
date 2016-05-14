
static const float PI = 3.14159265f;
static const float HALF_PI = 3.14159265f / 2.0f;
static const float TAU = 3.14159265f * 2.0f;

//Use linear interpolation to convert a value from zero to one to fall within min-max
float ClampToRange(float scalar, float min, float max)
{
	return (scalar * (max - min)) + min;
}


// Helper function used by several pixel shaders to blur the line edges
//rho in this context is not line length, it is distance from the line center
float BlurEdge(float rho, float threshold)
{
	if (rho < threshold)
	{
		return 1.0f;
	}
	else
	{
		float normrho = (rho - threshold) * 1 / (1 - threshold);
		return 1 - normrho;
	}
}