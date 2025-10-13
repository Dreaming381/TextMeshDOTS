#ifndef TMD0OUTLINEFUNCTIONSUNLIT
#define TMD0OUTLINEFUNCTIONSUNLIT

#include "TmdGlobalsApi.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"


//adjust face weight (input: face) 
void GetFontWeight(float isoPerimeterIN, float scale, float weightNormal, float weightBold, out float isoPerimeterOut)
{
    float bold = step(scale, 0); //float bold = scale < 0.0;
    float weight = lerp(weightNormal, weightBold, bold) / 4.0;
    weight = (weight + isoPerimeterIN) * 0.5;
    isoPerimeterOut = weight;
}
void ColorNoOutlineUNLIT_float(
    float4 vertexColor,
    float4 colorIN,
    float3 uvA,
    float scale,
    float2 texelSize,
    float isoPerimeter,
    float softness,
    out float4 colorOUT)
{
    float SD = colorIN.r; // SD  : Signed Distance (encoded : Distance / SDR + .5)
    
    // The signed distance ratio is the padding value + 1.
	// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
    float SDR = 11.0; // SDR : Signed Distance Ratio        

    float SSR;
    ScreenSpaceRatio(uvA.xy, texelSize.x, SSR);
        
    float isoPerimeterOut;
    float weightNormal = 0;
    float weightBold = 0.75f;
    GetFontWeight(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);
        
    //get face color
    float faceSDFAlpha;
    ComputeSDF(SSR, SD, SDR, isoPerimeterOut, softness, faceSDFAlpha);
    float4 faceColor;
    Layer1(faceSDFAlpha, vertexColor, faceColor);
    
    colorOUT = faceColor * vertexColor.w;
    return;
}
void TextureNoOutlineUNLIT_float(
    float4 vertexColor,
    float4 colorIN,
    float3 uvA,
    float2 uvB,
    float scale,
    float2 texelSize,
    float isoPerimeter,
    float softness,
    UnityTexture2D faceTexture,
    float2 faceUVSpeed,
    float2 faceTiling,
    float2 faceOffset,
    float4 underlayColorIN,
    out float4 colorOUT)
{
    float SD = colorIN.r; // SD  : Signed Distance (encoded : Distance / SDR + .5)
    
    // The signed distance ratio is the padding value + 1.
	// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
    float SDR = 11.0; // SDR : Signed Distance Ratio
    
    float SSR;
    ScreenSpaceRatio(uvA.xy, texelSize.x, SSR);
        
    float isoPerimeterOut;
    float weightNormal = 0;
    float weightBold = 0.75f;
    GetFontWeight(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);
    
    //get face color
    float2 uvBOUT;
    GenerateUV(uvB, faceTiling, faceOffset, faceUVSpeed, uvBOUT);
    float4 textureColor = SAMPLE_TEXTURE2D(faceTexture, faceTexture.samplerstate, uvBOUT); //sampler_LinearClamp sampler_LinearRepeat
    float4 tmpColor = vertexColor * textureColor;
        
    float faceSDFAlpha;
    ComputeSDF(SSR, SD, SDR, isoPerimeterOut, softness, faceSDFAlpha);
    float4 faceColor;
    Layer1(faceSDFAlpha, tmpColor, faceColor);
    
    //get underlay color
    float underlaySDFAlpha;
    ComputeSDF(SSR, SD, SDR, isoPerimeterOut, softness, underlaySDFAlpha);
    float4 underlayColor;
    Layer1(underlaySDFAlpha, underlayColorIN, underlayColor);

    // determine final outColor
    float4 rgba = Blend_float(faceColor, underlayColor);
    colorOUT = rgba * vertexColor.w;
}
#endif