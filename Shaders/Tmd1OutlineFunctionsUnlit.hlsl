#ifndef TMD1OUTLINEFUNCTIONSUNLIT
#define TMD1OUTLINEFUNCTIONSUNLIT

#include "TmdGlobalsApi.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

//adjust face weight (input: face and 1 outline) 
void GetFontWeight2(float2 isoPerimeterIN, float scale, float weightNormal, float weightBold, out float2 isoPerimeterOut)
{
    isoPerimeterOut = isoPerimeterIN;
    float bold = step(scale, 0); //float bold = scale < 0.0;
    float weight = lerp(weightNormal, weightBold, bold) / 4.0;
    weight = (weight + isoPerimeterIN.x) * 0.5;
    isoPerimeterOut.x = weight;
}
void ComputeSDF2(float SSR, float SD, float SDR, float2 isoPerimeter, float2 softness, out float2 outAlpha)
{
    softness *= SSR * SDR;
    float d = (SD - 0.5f) * SDR;
    outAlpha = saturate((d * 2.0f * SSR + 0.5f + isoPerimeter * SDR * SSR + softness * 0.5) / (1.0 + softness));
}
// Face + 1 Outline
void Layer2(float2 alpha, float4 color0, float4 color1, out float4 outColor)
{
    color1.a *= alpha.y;
    color0.rgb *= color0.a;
    color1.rgb *= color1.a;
    outColor = lerp(color1, color0, alpha.x);
    outColor.rgb /= outColor.a;
}

void Color1OutlineUNLIT_float(
    float4 vertexColor,
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
    
    colorOUT = faceColor * vertexColor.w;
    return;
}

void Texture1OutlineUNLIT_float(
    float4 vertexColor,
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
    
    //get underlay color
    float underlaySDFAlpha;
    ComputeSDF(SSR, SD, SDR, isoPerimeterOut.x, softness.x, underlaySDFAlpha);
    float4 underlayColor;
    Layer1(underlaySDFAlpha, underlayColorIN, underlayColor);

    // determine final outColor
    float4 rgba = Blend_float(faceColor, underlayColor);
    colorOUT = rgba * vertexColor.w;
}
#endif