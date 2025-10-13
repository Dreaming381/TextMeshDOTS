#ifndef TMD3OUTLINEFUNCTIONSUNLIT
#define TMD3OUTLINEFUNCTIONSUNLIT

#include "TmdGlobalsApi.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

//adjust face weight (input: face and 1 outline) 
void GetFontWeight4(float4 isoPerimeterIN, float scale, float weightNormal, float weightBold, out float4 isoPerimeterOut)
{
    isoPerimeterOut = isoPerimeterIN;
    float bold = step(scale, 0); //float bold = scale < 0.0;
    float weight = lerp(weightNormal, weightBold, bold) / 4.0;
    weight = (weight + isoPerimeterIN.x) * 0.5;
    isoPerimeterOut.x = weight;
}
void ComputeSDF4(float SSR, float SD, float SDR, float4 isoPerimeter, float4 softness, out float4 outAlpha)
{
    softness *= SSR * SDR;
    float d = (SD - 0.5f) * SDR;
    outAlpha = saturate((d * 2.0f * SSR + 0.5f + isoPerimeter * SDR * SSR + softness * 0.5) / (1.0 + softness));
}
// Face + 3 Outline
void Layer4(float4 alpha, float4 color0, float4 color1, float4 color2, float4 color3, out float4 outColor)
{
    color3.a *= alpha.w;
    color0.rgb *= color0.a;
    color1.rgb *= color1.a;
    color2.rgb *= color2.a;
    color3.rgb *= color3.a;
    outColor = lerp(lerp(lerp(color3, color2, alpha.z), color1, alpha.y), color0, alpha.x);
    outColor.rgb /= outColor.a;
}

void Color3OutlineUNLIT_float(
    float4 vertexColor,
    float4 colorIN,
    float3 uvA,
    float scale,
    float2 texelSize,
    float4 isoPerimeter,
    float4 softness,
    float4 outlineColor1,
    float4 outlineColor2,
    float4 outlineColor3,
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
    float4 isoPerimeterOut;
    GetFontWeight4(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);
        
    //get face color
    float4 sdfAlpha;
    ComputeSDF4(SSR, SD, SDR, isoPerimeterOut, softness, sdfAlpha);
    float4 faceColor;
    Layer4(sdfAlpha, vertexColor, outlineColor1, outlineColor2, outlineColor3, faceColor);

    colorOUT = faceColor * vertexColor.w;
    return;
}

void Texture3OutlineUNLIT_float(
    float4 vertexColor,
    float4 colorIN,
    float3 uvA,
    float2 uvB,
    float scale,
    float2 texelSize,
    float4 isoPerimeter,
    float4 softness,
    UnityTexture2D faceTexture,
    float2 faceUVSpeed,
    float2 faceTiling,
    float2 faceOffset,
    float4 underlayColorIN,
    float4 outlineColor1,
    float4 outlineColor2,
    float4 outlineColor3,
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
    float4 isoPerimeterOut;
    GetFontWeight4(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);
    
    //get face color
    float2 uvBOUT;
    GenerateUV(uvB, faceTiling, faceOffset, faceUVSpeed, uvBOUT);
    float4 textureColor = SAMPLE_TEXTURE2D(faceTexture, faceTexture.samplerstate, uvBOUT); //sampler_LinearClamp sampler_LinearRepeat
    float4 tmpColor = vertexColor * textureColor;
        
    float4 sdfAlpha;
    ComputeSDF4(SSR, SD, SDR, isoPerimeterOut, softness, sdfAlpha);
    float4 faceColor;
    Layer4(sdfAlpha, tmpColor, outlineColor1, outlineColor2, outlineColor3, faceColor);
    
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