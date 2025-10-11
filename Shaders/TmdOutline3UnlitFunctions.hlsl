#ifndef TMDOUTLINE3UNLITFUNCTIONS
#define TMDOUTLINE3UNLITFUNCTIONS

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
void Outline3Unlit_float(float4 vertexColor, 
    float4 uvAandB, 
    float4 atlasIndexScaleIsSdf16IsBitmap, 
    float4 outlineColor1, 
    float4 outlineColor2, 
    float4 outlineColor3, 
    float4 isoPerimeter, 
    float4 softness, 
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
        
        float4 isoPerimeterOut;
        float weightNormal = 0;
        float weightBold = 0.75f;        
        GetFontWeight4(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);

		// The signed distance ratio is the padding value + 1.
		// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
        float SDR = 11.0; // SDR : Signed Distance Ratio        
        
        //get face color
        float4 sdfAlpha;        
        ComputeSDF4(SSR, SD, SDR, isoPerimeterOut, softness, sdfAlpha);        
        float4 rgba;
        Layer4(sdfAlpha, vertexColor, outlineColor1, outlineColor2, outlineColor3, rgba);

        // determine final vertex color
        rgba = rgba * vertexColor.w;
        outColor = rgba.xyz;
        outAlpha = rgba.w;
        return;
    }    
}
void TextureOutline3Unlit_float(float4 vertexColor, 
    float4 uvAandB, 
    float4 atlasIndexScaleIsSdf16IsBitmap, 
    float4 outlineColor1, 
    float4 outlineColor2, 
    float4 outlineColor3, 
    float4 isoPerimeter, 
    float4 softness, 
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
        
        float4 isoPerimeterOut;
        float weightNormal = 0;
        float weightBold = 0.75f;
        GetFontWeight4(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);

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
        
        float4 faceSDFAlpha;
        ComputeSDF4(SSR, SD, SDR, isoPerimeterOut, softness, faceSDFAlpha);
        float4 faceColorFinal;
        Layer4(faceSDFAlpha, faceColor, outlineColor1, outlineColor2, outlineColor3, faceColorFinal);

        // determine final outColor
        float4 rgba = Blend(faceColorFinal, underlayColorFinal);
        rgba = rgba * vertexColor.w;
        outColor = rgba.xyz;
        outAlpha = rgba.w;
        return;
    }
}
#endif