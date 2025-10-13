#ifndef TMD1OUTLINEFUNCTIONSLIT
#define TMD1OUTLINEFUNCTIONSLIT

#include "TmdGlobalsApi.hlsl"
#include "Tmd1OutlineFunctionsUnlit.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"


void Color1OutlineLIT_float(
    float4 vertexColor,
    float3 normal,
    float4 colorIN,
    float3 uvA,
    float scale,
    float2 texelSize,
    float2 isoPerimeter,
    float2 softness,
    float4 outlineColor,
    out float4 colorOUT)
{
    float SD = colorIN.r; // SD  : Signed Distance (encoded : Distance / SDR + .5)
    
    // The signed distance ratio is the padding value + 1.
	// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
    float SDR = 11.0; // SDR : Signed Distance Ratio        

    float SSR;
    ScreenSpaceRatio(uvA.xy, texelSize.x, SSR);      
    
    float weightNormal = 0;
    float weightBold = 0.75f;
    float2 isoPerimeterOut;
    GetFontWeight2(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);
        
    //get face color
    float2 faceSDFAlpha;
    ComputeSDF2(SSR, SD, SDR, isoPerimeterOut, softness, faceSDFAlpha);
    float4 faceColor;
    Layer2(faceSDFAlpha, vertexColor, outlineColor, faceColor);
    
    float4 litFaceColor;
    EvaluateLight_float(normal, faceColor, litFaceColor);
    
    colorOUT = litFaceColor * vertexColor.w;
    return;
}
void Texture1OutlineLIT_float(
    float4 vertexColor,
    float3 normal,
    float4 colorIN,
    float3 uvA,
    float2 uvB,
    float scale,
    float2 texelSize,
    float2 isoPerimeter,
    float2 softness,
    UnityTexture2D faceTexture,
    float2 faceUVSpeed,
    float2 faceTiling,
    float2 faceOffset,
    float4 underlayColorIN,
    float4 outlineColor,
    out float4 colorOUT)
{
    float SD = colorIN.r; // SD  : Signed Distance (encoded : Distance / SDR + .5)
    
    // The signed distance ratio is the padding value + 1.
	// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
    float SDR = 11.0; // SDR : Signed Distance Ratio
    
    float SSR;
    ScreenSpaceRatio(uvA.xy, texelSize.x, SSR);        
    
    float weightNormal = 0;
    float weightBold = 0.75f;
    float2 isoPerimeterOut;
    GetFontWeight2(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);
    
    //get face color
    float2 uvBOUT;
    GenerateUV(uvB, faceTiling, faceOffset, faceUVSpeed, uvBOUT);
    float4 textureColor = SAMPLE_TEXTURE2D(faceTexture, faceTexture.samplerstate, uvBOUT); //sampler_LinearClamp sampler_LinearRepeat
    float4 tmpColor = vertexColor * textureColor;
        
    float2 faceSDFAlpha;
    ComputeSDF2(SSR, SD, SDR, isoPerimeterOut, softness, faceSDFAlpha);
    float4 faceColor;
    Layer2(faceSDFAlpha, tmpColor, outlineColor, faceColor);
    
    float4 litFaceColor;
    EvaluateLight_float(normal, faceColor, litFaceColor);
    
    //get underlay color
    float underlaySDFAlpha;
    ComputeSDF(SSR, SD, SDR, isoPerimeterOut.x, softness.x, underlaySDFAlpha);
    float4 underlayColor;
    Layer1(underlaySDFAlpha, underlayColorIN, underlayColor);

    // determine final outColor
    float4 rgba = Blend_float(litFaceColor, underlayColor);
    colorOUT = rgba * vertexColor.w;
}
#endif