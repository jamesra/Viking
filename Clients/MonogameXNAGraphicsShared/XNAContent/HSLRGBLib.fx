
const static float4 LumaWeights = {0.3, 0.59, 0.11, 0}; 

const static float4 InverseLumaWeights = {1/0.3, 1/0.59, 1/0.11, 0}; 


const static int3 RGBIndexMap[] = {{0,1,2},
								  {1,0,2},
								  {2,0,1},
								  {2,1,0},
								  {1,2,0},
								  {0,2,1}};

const static float4 ComponentLumaWeightsMap[] = {{0.30, 0.59, 0.11, 0.70}, //Luma weights of Chroma, slope, base, slope+base
												 {0.59, 0.30, 0.11, 0.41},
												 {0.59, 0.11, 0.30, 0.41},
												 {0.11, 0.59, 0.30, 0.89},
												 {0.11, 0.30, 0.59, 0.89},
												 {0.30, 0.11, 0.59, 0.70}};

const static float4 InverseComponentLumaWeightsMap[] = {{1/0.30, 1/0.59, 1/0.11, 1/0.70}, //Luma weights of Chroma, slope, base, slope+base
												 {1/0.59, 1/0.30, 1/0.11, 1/0.41},
												 {1/0.59, 1/0.11, 1/0.30, 1/0.41},
												 {1/0.11, 1/0.59, 1/0.30, 1/0.89},
												 {1/0.11, 1/0.30, 1/0.59, 1/0.89},
												 {1/0.30, 1/0.11, 1/0.59, 1/0.70}};

float BlendLumaWithBackground(float BackgroundLuma, float ForegroundLuma, float Alpha)
{
	return (BackgroundLuma * (1 - Alpha)) + ((ForegroundLuma * Alpha));
}

float CalculateHSLLumaFromRGB(float4 RGB)
{
    float3 maxval = max(max(RGB.r, RGB.g), RGB.b);
    float3 minval = min(min(RGB.r, RGB.g), RGB.b);

    return (maxval + minval) / 2.0;
}

//Convert RGB value to Hue, Chroma, Luma, slope
float4 RGBToHCL(float4 RGB)
{
	
	float maxC = max(RGB.r, RGB.g); 
	maxC = max(maxC, RGB.b); 
	float minC = min(RGB.r, RGB.g); 
	minC = min(minC, RGB.b); 

	float Hue = 0; 
	float Chroma = maxC - minC;
	float Value = maxC;
	
	float HPrime = 0;

	if(Chroma == 0)
		HPrime = 0; 
	else if(RGB.r == maxC)
	{
		HPrime = ((RGB.g - RGB.b) / Chroma);
		if(HPrime < 0)
			HPrime = HPrime + 6;

	}
	else if(RGB.g == maxC)
	{
		HPrime = ((RGB.b - RGB.r) / Chroma) + 2;
	}
	else
	{
		HPrime = ((RGB.r - RGB.g) / Chroma) + 4;
	}

	float fDescend = fmod(HPrime, 2);
	
	Hue = HPrime / 6;
	
    float Luma = (maxC + minC) / 2.0; //mul(LumaWeights, RGB);

	float4 HCL = {Hue, Chroma, Luma, RGB.a};

	return HCL;
}

float3 CorrectLuma(int Hextant, float3 Components, float Luma)
{
	float4 ComponentLumaWeights = ComponentLumaWeightsMap[Hextant];

	float OverlayLuma = mul(ComponentLumaWeights.xyz, Components); 
	float m = (Luma - OverlayLuma);
	Components += m; 

	if(Components.r <= 1 && Components.r >= 0)
		return Components;

	Components.rg = saturate(Components.rg); 

	//Figure out how much to spill over
	OverlayLuma = mul(ComponentLumaWeights, Components); 
	m =  (Luma - OverlayLuma) * 1/ComponentLumaWeights[3];
	Components.gb += m; 

	if(Components.g <= 1 && Components.g >= 0)
		return Components;
	
	Components.gb = saturate(Components.gb);

	OverlayLuma = mul(ComponentLumaWeights, Components);
	m =  (Luma - OverlayLuma);
	Components.b += m * 1 / (ComponentLumaWeights[0] + ComponentLumaWeights[1]); 

	return Components;
}


//Hue, Chroma, Luma, slope to RGB value
float4 HCLToRGB(float4 hcls)
{
	float Hue = hcls[0]; 
	float Chroma = hcls[1];
	float Luma = hcls[2];

	float HPrime = Hue * 6; 
	int Hextant = (int)HPrime; 
	//float remainder = modf(HPrime, Hextant);
	
	float fDescend = fmod(HPrime, 2);

	float Slope = Chroma * (1 - abs(fDescend - 1));

	float3 Components = {Chroma, Slope, 0};

	//What is the luma of the channels used in the overlay only?  Determines how much we can boost without burning

	Components = CorrectLuma(Hextant, Components, Luma); 

	int3 RGBIndex = RGBIndexMap[Hextant]; 
	
	float4	output = {Components[RGBIndex[0]], Components[RGBIndex[1]], Components[RGBIndex[2]], hcls [3]};
	return output;
}

float4 BlendHSLColorOverBackground(float4 HSLForegroundColor, float4 RGBBackgroundColor, float ForegroundLumaAlpha)
{
	float Hue = HSLForegroundColor.r;
	float Saturation = HSLForegroundColor.g;
	float ForegroundLuma = HSLForegroundColor.b;

    float BackgroundLuma = CalculateHSLLumaFromRGB(RGBBackgroundColor); //mul(RGBBackgroundColor.xyz, LumaWeights.xyz);

	float Luma = BlendLumaWithBackground(BackgroundLuma, ForegroundLuma, ForegroundLumaAlpha);

	float4 hsv = { Hue, Saturation, Luma, HSLForegroundColor.a };
	float4 finalColor = ForegroundLuma > 0 ? HCLToRGB(hsv) : RGBBackgroundColor;

	return finalColor;
}