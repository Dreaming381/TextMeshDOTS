#ifndef TMDGLOBALSAPI
#define TMDGLOBALSAPI

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"

uniform ByteAddressBuffer _tmdGlyphs;
TEXTURE2D_ARRAY(_tmdSdf8);
SAMPLER(sampler_tmdSdf8);
TEXTURE2D_ARRAY(_tmdSdf16);
SAMPLER(sampler_tmdSdf16);
TEXTURE2D_ARRAY(_tmdBitmap);
SAMPLER(sampler_tmdBitmap);

// Helpers
float4 UnpackHalfColor(uint2 packedColor)
{
    uint4 expanded = packedColor.xxyy;
    expanded.yw = expanded.yw >> 16u;
    expanded = expanded & 0xffffu;
    return f16tof32(expanded);
}


// Base APIs
void GetGlyph(uint glyphIndex, uint glyphStartIndex, uint glyphCount,
    out float2 blPosition,
    out float2 brPosition,
    out float2 tlPosition,
    out float2 trPosition,

    out float2 blUVB,
    out float2 brUVB,
    out float2 tlUVB,
    out float2 trUVB,

    out float4 blColor,
    out float4 brColor,
    out float4 tlColor,
    out float4 trColor,

    out float2 blUVA,
    out float2 trUVA,

    out float arrayIndex,
    out uint glyphEntryId,
    out float scale,
    out uint reserved)
{
    if (glyphIndex >= glyphCount)
    {
        blPosition = asfloat(~0u);
        brPosition = blPosition;
        tlPosition = blPosition;
        trPosition = blPosition;
        blUVB = blPosition;
        brUVB = blPosition;
        tlUVB = blPosition;
        trUVB = blPosition;
        blColor = 0;
        brColor = blColor;
        tlColor = blColor;
        trColor = blColor;
        blUVA = blPosition;
        trUVA = blPosition;
        arrayIndex = 0;
        glyphEntryId = 0;
        scale = 0;
        reserved = 0u;
        return;
    }

    uint baseAddress = (glyphStartIndex + glyphIndex) * 128;
    uint4 load0_15 = _tmdGlyphs.Load4(baseAddress);
    blPosition = asfloat(load0_15.xy);
    brPosition = asfloat(load0_15.zw);
    uint4 load16_31 = _tmdGlyphs.Load4(baseAddress + 16);
    tlPosition = asfloat(load16_31.xy);
    trPosition = asfloat(load16_31.zw);

    uint4 load32_47 = _tmdGlyphs.Load4(baseAddress + 32);
    blUVB = asfloat(load32_47.xy);
    brUVB = asfloat(load32_47.zw);
    uint4 load48_63 = _tmdGlyphs.Load4(baseAddress + 48);
    tlUVB = asfloat(load48_63.xy);
    trUVB = asfloat(load48_63.zw);

    uint4 load64_79 = _tmdGlyphs.Load4(baseAddress + 64);   //load half4 blColor and half4 brColor
    blColor = UnpackHalfColor(load64_79.xy);                //convert blColor from half4 to float4
    brColor = UnpackHalfColor(load64_79.zw);                //convert brColor from half4 to float4
    uint4 load80_95 = _tmdGlyphs.Load4(baseAddress + 80);
    tlColor = UnpackHalfColor(load80_95.xy);
    trColor = UnpackHalfColor(load80_95.zw);

    uint4 load96_111 = _tmdGlyphs.Load4(baseAddress + 96);
    blUVA = asfloat(load96_111.xy);
    trUVA = asfloat(load96_111.zw);

    uint4 load112_127 = _tmdGlyphs.Load4(baseAddress + 112);
    arrayIndex = asfloat(load112_127.x);
    glyphEntryId = load112_127.y;
    scale = asfloat(load112_127.z);
    reserved = load112_127.w;
}

// Todo: This causes Unity's shader compiler to break. Attempt to reenable this later.
//UnityTexture2DArray GetSdfTextureArray(bool is16Bit)
//{
//    if (is16Bit)
//    {
//        return UnityBuildTexture2DArrayStruct(_tmdSdf16);
//    }
//    else
//    {
//        return UnityBuildTexture2DArrayStruct(_tmdSdf8);
//    }
//}

UnityTexture2DArray GetSdf8TextureArray(out float2 texelSize)
{
    uint width, height, elements, numberOfLevels;
    _tmdSdf8.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
    texelSize = 1.0f / float2(width, height);
    return UnityBuildTexture2DArrayStruct(_tmdSdf8);
}

UnityTexture2DArray GetSdf16TextureArray(out float2 texelSize)
{
    uint width, height, elements, numberOfLevels;
    _tmdSdf16.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
    texelSize = 1.0f / float2(width, height);
    return UnityBuildTexture2DArrayStruct(_tmdSdf16);
}

UnityTexture2DArray GetBitmapTextureArray()
{
    return UnityBuildTexture2DArrayStruct(_tmdBitmap);
}


// Additional APIs

// Corner order:  bl = 0, tl = 1, tr = 2, br = 3
void GetGlyphCorner(uint glyphIndex, uint cornerIndex, uint glyphStartIndex, uint glyphCount, out float2 position, out float3 uvA, out float2 uvB, out float4 color, out float scale, out uint glyphEntryID)
{
    float2 blPosition;
    float2 brPosition;
    float2 tlPosition;
    float2 trPosition;
    float2 blUVB;
    float2 brUVB;
    float2 tlUVB;
    float2 trUVB;
    float4 blColor;
    float4 brColor;
    float4 tlColor;
    float4 trColor;
    float2 blUVA;
    float2 trUVA;
    float arrayIndex;
    uint reserved;
    GetGlyph(glyphIndex, glyphStartIndex, glyphCount, blPosition, brPosition, tlPosition, trPosition, blUVB, brUVB, tlUVB, trUVB, blColor, brColor, tlColor, trColor, blUVA, trUVA, arrayIndex, glyphEntryID, scale, reserved);
    if (cornerIndex == 0)
    {
        // bottom left
        position = blPosition;
        uvA = float3(blUVA, arrayIndex);
        //uvB = blUVB;
        uvB = float2(0, 0);
        color = blColor;
    }
    else if (cornerIndex == 1)
    {
        // top left
        position = tlPosition;
        uvA = float3(blUVA.x, trUVA.y, arrayIndex);
        //uvB = tlUVB;
        uvB = float2(0, 1);
        color = tlColor;
    }
    else if (cornerIndex == 2)
    {
        // top right
        position = trPosition;
        uvA = float3(trUVA, arrayIndex);
        //uvB = trUVB;
        uvB = float2(1, 1);
        color = trColor;
    }
    else
    {
        // bottom right
        position = brPosition;
        uvA = float3(trUVA.x, blUVA.y, arrayIndex);
        //uvB = brUVB;
        uvB = float2(1, 0);
        color = brColor;
    }
}

void ExtractGlyphFlagsFromEntryID(uint glyphEntryID, out bool isSdf16, out bool isBitmap)
{
    uint format = glyphEntryID >> 30u;
    isSdf16 = format == 1;
    isBitmap = format == 2;
}

void GetGlyphIndexAndCornerFromQuadVertexID(uint vertexID, out uint glyphIndex, out uint cornerIndex)
{
    glyphIndex = vertexID >> 2u;
    cornerIndex = vertexID & 3u;
}
void GetGlyphFromBuffer_float(float2 textShaderIndex, float vertexID, out float3 position, out float3 normal, out float3 tangent, out float4 vertexColor, out float4 uvAandB, out float4 atlasIndexScaleIsSdf16IsBitmap)
{
    uint glyphIndex;
    uint cornerIndex;
    GetGlyphIndexAndCornerFromQuadVertexID(vertexID, glyphIndex, cornerIndex);
    uint glyphStartIndex = asuint(textShaderIndex.x);
    uint glyphCount = asuint(textShaderIndex.y);
    float2 position2D;
    float3 uvA;
    float2 uvB;
    float4 color;
    float scale;
    uint glyphEntryID;
    GetGlyphCorner(glyphIndex, cornerIndex, glyphStartIndex, glyphCount, position2D, uvA, uvB, color, scale, glyphEntryID);
    bool isSdf16;
    bool isBitmap;
    ExtractGlyphFlagsFromEntryID(glyphEntryID, isSdf16, isBitmap);
    position = float3(position2D, 0.0);
    normal = float3(0.0, -1.0, 0.0);
    tangent = float3(1.0, 0.0, 0.0);
    vertexColor = color;
    uvAandB = float4(uvA.xy, uvB);
    atlasIndexScaleIsSdf16IsBitmap = float4(uvA.z, scale, isSdf16, isBitmap);
}
// UV			: Texture coordinate of the source distance field texture
// texelSize	: texelSize of the source distance field texture
void ScreenSpaceRatio(float2 uvA, float texelSize, out float SSR)
{
    SSR = rsqrt(abs(ddx(uvA.x) * ddy(uvA.y) - ddy(uvA.x) * ddx(uvA.y))) * texelSize.x;
}
void GenerateUV(float2 inUV, float2 tiling, float2 offset, float2 animSpeed, out float2 outUV)
{
    outUV = inUV * tiling + offset + (animSpeed * _Time.y);
}

void ComputeSDF(float SSR, float SD, float SDR, float isoPerimeter, float softness, out float outAlpha)
{
    softness *= SSR * SDR;
    float d = (SD - 0.5) * SDR; // Signed distance to edge, in Texture space
    outAlpha = saturate((d * 2.0 * SSR + 0.5 + isoPerimeter * SDR * SSR + softness * 0.5) / (1.0 + softness)); // Screen pixel coverage (alpha)
}
// Face only
void Layer1(float alpha, float4 color0, out float4 outColor)
{
    color0.a *= alpha;
    outColor = color0;
}
float4 Blend(float4 overlying, float4 underlying)
{
    overlying.rgb *= overlying.a;
    underlying.rgb *= underlying.a;
    float3 blended = overlying.rgb + ((1 - overlying.a) * underlying.rgb);
    float alpha = underlying.a + (1 - underlying.a) * overlying.a;
    return float4(blended / alpha, alpha);
}
#endif