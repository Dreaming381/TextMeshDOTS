#ifndef TMDSIMPLEUNLITFUNCTIONS
#define TMDSIMPLEUNLITFUNCTIONS

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


void SimpleUnlit_float(float4 vertexColor, float4 uvAandB, float4 atlasIndexScaleIsSdf16IsBitmap, float isoPerimeter, float softness, out float3 outColor, out float outAlpha)
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
        
        float isoPerimeterOut;
        float weightNormal = 0;
        float weightBold = 0.75f;
        GetFontWeight(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);

		// The signed distance ratio is the padding value + 1.
		// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
        float SDR = 11.0; // SDR : Signed Distance Ratio        
        float faceSDFAlpha;
        ComputeSDF(SSR, SD, SDR, isoPerimeterOut, softness, faceSDFAlpha);
        
        float4 faceColor;
        Layer1(faceSDFAlpha, vertexColor, faceColor);  

        // determine final vertex color
        faceColor = faceColor * vertexColor.w;
        outColor = faceColor.xyz;
        outAlpha = faceColor.w;
        return;
    }
}

void SimpleTextureUnlit_float(float4 vertexColor, 
    float4 uvAandB, 
    float4 atlasIndexScaleIsSdf16IsBitmap, 
    float isoPerimeter, 
    float softness, 
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
        
        float isoPerimeterOut;
        float weightNormal = 0;
        float weightBold = 0.75f;
        GetFontWeight(isoPerimeter, scale, weightNormal, weightBold, isoPerimeterOut);        

		// The signed distance ratio is the padding value + 1.
		// Todo: Need to pack the sampling point size enumeration into glyphEntryID.
        float SDR = 11.0; // SDR : Signed Distance Ratio        
        
        //get underlay color
        float underlaySDFAlpha;
        ComputeSDF(SSR, SD, SDR, isoPerimeterOut, softness, underlaySDFAlpha);
        float4 underlayColorFinal;
        Layer1(underlaySDFAlpha, underlayColor, underlayColorFinal);        
        
        //get face color
        float2 uvBOUT;
        GenerateUV(uvB, faceTiling, faceOffset, faceUVSpeed, uvBOUT);
        float4 textureColor = SAMPLE_TEXTURE2D(faceTexture, faceTexture.samplerstate, uvBOUT); //sampler_LinearClamp sampler_LinearRepeat
        float4 faceColor = vertexColor * textureColor;
        
        float faceSDFAlpha;
        ComputeSDF(SSR, SD, SDR, isoPerimeterOut, softness, faceSDFAlpha);        
        float4 faceColorFinal;
        Layer1(faceSDFAlpha, faceColor, faceColorFinal);        

        // determine final outColor
        float4 rgba = Blend(faceColorFinal, underlayColorFinal);
        rgba = rgba * vertexColor.w;
        outColor = rgba.xyz;
        outAlpha = rgba.w;
        return;
    }
}
#endif