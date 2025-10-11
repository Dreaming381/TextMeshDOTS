#ifndef TMDOUTLINE1UNLITFUNCTIONS
#define TMDOUTLINE1UNLITFUNCTIONS

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
void Outline1Unlit_float(float4 vertexColor, float4 uvAandB, float4 atlasIndexScaleIsSdf16IsBitmap, float4 outlineColor, float2 isoPerimeter, float2 softness, out float3 outColor, out float outAlpha)
{
    float3 uvA = float3(uvAandB.xy, atlasIndexScaleIsSdf16IsBitmap.x);
    float2 uvB = uvAandB.zw;
    float scale = atlasIndexScaleIsSdf16IsBitmap.y;
    bool isSdf16 = atlasIndexScaleIsSdf16IsBitmap.z;
    bool isBitmap = atlasIndexScaleIsSdf16IsBitmap.w;

    if (isBitmap)
    {
        UnityTexture2DArray bitmap = GetBitmapTextureArray();
        float4 bitmapColor = bitmap.Sample(sampler_LinearClamp, uvA);
        bitmapColor *= vertexColor;
        outColor = bitmapColor.xyz;
        outAlpha = bitmapColor.w;
        return;
    }
    else
    {
        float SD; // SD  : Signed Distance (encoded : Distance / SDR + .5)
        float2 texelSize;
        if (isSdf16)
        {
            UnityTexture2DArray sdf = GetSdf16TextureArray(texelSize);
            SD = sdf.Sample(sampler_LinearClamp, uvA).r;
        }
        else
        {
            UnityTexture2DArray sdf = GetSdf8TextureArray(texelSize);
            SD = sdf.Sample(sampler_LinearClamp, uvA).r;
        }
        
        float SSR;
        ScreenSpaceRatio(uvA.xy, texelSize.x, SSR);
        
        float2 isoPerimeterOut;
        float weightNormal = 0;
        float weightBold = 0.75f;
        GetFontWeight2(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);
                
		// The signed distance ratio is the padding value + 1.
		// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
        float SDR = 11.0; // SDR : Signed Distance Ratio        
        float2 sdfAlpha;        
        ComputeSDF2(SSR, SD, SDR, isoPerimeterOut, softness, sdfAlpha);
        
        float4 faceColor;
        Layer2(sdfAlpha, vertexColor, outlineColor, faceColor); 

        // determine final vertex color
        faceColor = faceColor * vertexColor.w;
        outColor = faceColor.xyz;
        outAlpha = faceColor.w;
        return;
    }    
}
void TextureOutline1Unlit_float(float4 vertexColor, 
    float4 uvAandB, 
    float4 atlasIndexScaleIsSdf16IsBitmap, 
    float4 outlineColor, 
    float2 isoPerimeter, 
    float2 softness, 
    UnityTexture2D faceTexture, 
    float2 faceUVSpeed, 
    float2 faceTiling, 
    float2 faceOffset,
    float4 underlayColor,
    out float3 outColor, 
    out float outAlpha)
{
    float3 uvA = float3(uvAandB.xy, atlasIndexScaleIsSdf16IsBitmap.x);
    float2 uvB = uvAandB.zw;
    float scale = atlasIndexScaleIsSdf16IsBitmap.y;
    bool isSdf16 = atlasIndexScaleIsSdf16IsBitmap.z;
    bool isBitmap = atlasIndexScaleIsSdf16IsBitmap.w;

    if (isBitmap)
    {
        UnityTexture2DArray bitmap = GetBitmapTextureArray();
        float4 bitmapColor = bitmap.Sample(sampler_LinearClamp, uvA);
        bitmapColor *= vertexColor;
        outColor = bitmapColor.xyz;
        outAlpha = bitmapColor.w;
        return;
    }
    else
    {
        float SD; // SD  : Signed Distance (encoded : Distance / SDR + .5)
        float2 texelSize;
        if (isSdf16)
        {
            UnityTexture2DArray sdf = GetSdf16TextureArray(texelSize);
            SD = sdf.Sample(sampler_LinearClamp, uvA).r;
        }
        else
        {
            UnityTexture2DArray sdf = GetSdf8TextureArray(texelSize);
            SD = sdf.Sample(sampler_LinearClamp, uvA).r;
        }
        
        float SSR;
        ScreenSpaceRatio(uvA.xy, texelSize.x, SSR);
        
        float2 isoPerimeterOut;
        float weightNormal = 0;
        float weightBold = 0.75f;
        GetFontWeight2(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);

		// The signed distance ratio is the padding value + 1.
		// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
        float SDR = 11.0; // SDR : Signed Distance Ratio
        
        //get underlay color
        float underlaySDFAlpha;
        ComputeSDF(SSR, SD, SDR, 0, 0, underlaySDFAlpha); 
        float4 underlayColorFinal;
        Layer1(underlaySDFAlpha, underlayColor, underlayColorFinal);        
        
        //get face color
        float2 uvBOUT;
        GenerateUV(uvB, faceTiling, faceOffset, faceUVSpeed, uvBOUT);
        float4 textureColor = SAMPLE_TEXTURE2D(faceTexture, faceTexture.samplerstate, uvBOUT);
        float4 faceColor = vertexColor * textureColor;
        
        float2 faceSDFAlpha;
        ComputeSDF2(SSR, SD, SDR, isoPerimeterOut, softness, faceSDFAlpha);
        float4 faceColorFinal;
        Layer2(faceSDFAlpha, faceColor, outlineColor, faceColorFinal);

        // determine final outColor
        float4 rgba = Blend(faceColorFinal, underlayColorFinal);
        rgba = rgba * vertexColor.w;
        outColor = rgba.xyz;
        outAlpha = rgba.w;
        return;
    }
}
#endif