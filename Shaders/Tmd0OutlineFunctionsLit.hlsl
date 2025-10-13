#ifndef TMD0OUTLINEFUNCTIONSLIT
#define TMD0OUTLINEFUNCTIONSLIT

#include "TmdGlobalsApi.hlsl"
#include "Tmd0OutlineFunctionsUnlit.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"


void ColorNoOutlineLIT_float(
    float4 vertexColor,
    float3 normal,
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
    
    float4 litFaceColor;
    EvaluateLight_float(normal, faceColor, litFaceColor);
    
    colorOUT = litFaceColor * vertexColor.w;
    return;
}
void TextureNoOutlineLIT_float(
    float4 vertexColor,
    float3 normal,
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
    
    float4 litFaceColor;
    EvaluateLight_float(normal, faceColor, litFaceColor);
    
    //get underlay color
    float underlaySDFAlpha;
    ComputeSDF(SSR, SD, SDR, isoPerimeterOut, softness, underlaySDFAlpha);
    float4 underlayColor;
    Layer1(underlaySDFAlpha, underlayColorIN, underlayColor);

    // determine final outColor
    float4 rgba = Blend_float(litFaceColor, underlayColor);
    colorOUT = rgba * vertexColor.w;
}
#endif