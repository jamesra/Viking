
const static float4 LumaWeights = {0.3, 0.59, 0.11, 0}; 

const static float4 InverseLumaWeights = {1/0.3, 1/0.59, 1/0.11, 0}; 


const static int3 RGBIndexMap[] = {{0,1,2},
								  {1,0,2},
								  {2,0,1},
								  {2,1,0},
								  {1,2,0},
								  {0,2,1}};

const static float4 ComponentLumaWeightsMap[] = {
	{0.30, 0.59, 0.11, 0.89}, // 0.30 + 0.59 = 0.89
	{0.59, 0.30, 0.11, 0.89}, // 0.59 + 0.30 = 0.89
	{0.59, 0.11, 0.30, 0.70}, // 0.59 + 0.11 = 0.70
	{0.11, 0.59, 0.30, 0.70}, // 0.11 + 0.59 = 0.70
	{0.11, 0.30, 0.59, 0.41}, // 0.11 + 0.30 = 0.41
	{0.30, 0.11, 0.59, 0.41}  // 0.30 + 0.11 = 0.41
};

const static float4 InverseComponentLumaWeightsMap[] = {
	{1 / 0.30, 1 / 0.59, 1 / 0.11, 1 / 0.89},
	{1 / 0.59, 1 / 0.30, 1 / 0.11, 1 / 0.89},
	{1 / 0.59, 1 / 0.11, 1 / 0.30, 1 / 0.70},
	{1 / 0.11, 1 / 0.59, 1 / 0.30, 1 / 0.70},
	{1 / 0.11, 1 / 0.30, 1 / 0.59, 1 / 0.41},
	{1 / 0.30, 1 / 0.11, 1 / 0.59, 1 / 0.41}
};

float BlendLumaWithBackground(float BackgroundLuma, float ForegroundLuma, float Alpha)
{
	return (BackgroundLuma * (1.0 - Alpha)) + ((ForegroundLuma * Alpha));
}


// Calculate perceptual luma using ITU-R BT.601 coefficients
// This preserves brightness as perceived by human vision
float CalculatePerceptualLumaFromRGB(float4 RGB)
{
    return mul(LumaWeights, RGB);
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
	
    //float Luma = (maxC + minC) / 2.0;  
	float Luma = mul(LumaWeights, RGB); // Perceptual luma, not HSL lightness

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
    OverlayLuma = mul(ComponentLumaWeights.xyz, Components);
	m =  (Luma - OverlayLuma) * InverseComponentLumaWeightsMap[Hextant][3];
	Components.gb += m; 

	if(Components.g <= 1 && Components.g >= 0)
		return Components;
	
	Components.gb = saturate(Components.gb);

    OverlayLuma = mul(ComponentLumaWeights.xyz, Components);
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

// DEPRECATED: HSVToHCL is no longer needed
// C# code now sends HCL (Hue, Chroma, Luma) directly via ConvertToHCL()
// This function is kept commented for reference but should not be used
/*
float4 HSVToHCL(float4 HSV)
{
    float Hue = HSV.r;
    float Saturation = HSV.g;
    float Value = HSV.b;
    
    // In HSV: Chroma = Value * Saturation
    float Chroma = Value * Saturation;
    
    float m = Value - Chroma;
    
    float HPrime = Hue * 6;
    int Hextant = (int)HPrime;
    float fDescend = fmod(HPrime, 2);
    float Slope = Chroma * (1 - abs(fDescend - 1));
    
    float3 Components = {Chroma, Slope, 0};
    Components += m;
    
    float4 ComponentLumaWeights = ComponentLumaWeightsMap[Hextant];
    float Luma = mul(ComponentLumaWeights.xyz, Components);
    
    return float4(Hue, Chroma, Luma, HSV.a);
}
*/

// Calculate maximum achievable chroma at a given luma for a specific hue
// Components = {Chroma, Slope, 0} before adding base offset m
// After adding m: RGB components = Components + m
// Constraint: all RGB in [0,1], Luma = weighted sum of RGB
float GetMaxChromaAtLuma(float Hue, float Luma)
{
    // At the extremes (black/white), no chroma is possible
    if (Luma <= 0.001 || Luma >= 0.999)
        return 0.0;
    
    float HPrime = Hue * 6;
    int Hextant = (int)HPrime;
    float fDescend = fmod(HPrime, 2);
    
    // Test several chroma values to find maximum
    // The relationship: Components = {Chroma, Slope, 0} + m
    // where m adjusts to hit target luma
    float4 ComponentLumaWeights = ComponentLumaWeightsMap[Hextant];
    
    // Calculate slope coefficient
    float slopeCoeff = (1.0 - abs(fDescend - 1.0));
    
    // For a given chroma, the components are: {Chroma, Chroma*slopeCoeff, 0}
    // The luma of these components: Luma_components = w.r*Chroma + w.g*Chroma*slopeCoeff + w.b*0
    // To reach target Luma, we add m to all: m = Luma - Luma_components
    // Final components: {Chroma+m, Chroma*slopeCoeff+m, m}
    // All must be in [0,1]:
    //   Chroma + m <= 1  =>  Chroma <= 1 - m
    //   m >= 0  =>  Luma >= Luma_components
    //   m <= 1  =>  Luma_components >= Luma - 1
    
    // The chromatic components contribute: ComponentLumaWeights[0]*C + ComponentLumaWeights[1]*C*slopeCoeff
    float chromaLumaCoeff = ComponentLumaWeights[0] + ComponentLumaWeights[1] * slopeCoeff;
    
    // Base component contributes: ComponentLumaWeights[2] * m
    // Luma = chromaLumaCoeff * Chroma + (Chroma*slopeCoeff + Chroma + m) * ComponentLumaWeights (weighted)
    // Simpler: For Components {C, C*slope, 0} + m, the total luma is:
    // Luma = ComponentWeights.r*(C+m) + ComponentWeights.g*(C*slope+m) + ComponentWeights.b*m
    // Luma = C*(ComponentWeights.r + ComponentWeights.g*slope) + m*(ComponentWeights.r + ComponentWeights.g + ComponentWeights.b)
    // Luma = C*chromaLumaCoeff + m
    // So: m = Luma - C*chromaLumaCoeff
    
    // Constraints:
    // C + m <= 1  =>  C + Luma - C*chromaLumaCoeff <= 1  =>  C*(1 - chromaLumaCoeff) <= 1 - Luma  =>  C <= (1-Luma)/(1-chromaLumaCoeff)
    // m >= 0  =>  Luma >= C*chromaLumaCoeff  =>  C <= Luma/chromaLumaCoeff
    
    float maxFromUpperBound = (1.0 - Luma) / (1.0 - chromaLumaCoeff);
    float maxFromLowerBound = Luma / chromaLumaCoeff;
    
    return min(maxFromUpperBound, maxFromLowerBound);
}

// Blend HCL (Hue, Chroma, Luma) foreground color over RGB background
// Input foreground color is already in HCL format from C# ConvertToHCL
float4 BlendHCLColorOverBackground(float4 HCLForegroundColor, float4 RGBBackgroundColor, float ForegroundLumaAlpha)
{
	float Hue = HCLForegroundColor.r;
	float ForegroundChroma = HCLForegroundColor.g;
	float ForegroundLuma = HCLForegroundColor.b;
    float BackgroundLuma = CalculatePerceptualLumaFromRGB(RGBBackgroundColor);

	float Luma = BlendLumaWithBackground(BackgroundLuma, ForegroundLuma, ForegroundLumaAlpha);
	
	// Limit chroma to what's achievable at the target luma
	// This prevents CorrectLuma from clamping/desaturating
	float Chroma = ForegroundChroma;
	if (ForegroundChroma > 0.0)
	{
		float maxChroma = GetMaxChromaAtLuma(Hue, Luma);
		Chroma = min(ForegroundChroma, maxChroma);
	}

	float4 hcl = { Hue, Chroma, Luma, HCLForegroundColor.a };
	float4 finalColor = ForegroundLuma > 0 ? HCLToRGB(hcl) : RGBBackgroundColor;

	return finalColor;
}