using HarfBuzz;
using System;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using Font = HarfBuzz.Font;
using Unity.Collections;
using Unity.Profiling;
using HarfBuzz.SDF;
using Unity.Jobs;

[DisableAutoCreation]
partial struct TestSystem : ISystem
{
    static readonly ProfilerMarker marker = new ProfilerMarker("Glyph");
    static readonly ProfilerMarker marker2 = new ProfilerMarker("Atlas");
    static readonly ProfilerMarker marker3 = new ProfilerMarker("SDF");
    Blob blob;
    Face face;
    Font font;
    IntPtr drawFunct;
    NativeAtlas nativeAtlas;
    int atlasWidth;
    int atlasHeight;

    //[BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        LoadFont(50);
        atlasWidth = atlasHeight = 512;
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.Alpha8, false);
        var rawTextureData = texture2D.GetRawTextureData<byte>();
        nativeAtlas = new NativeAtlas(rawTextureData, 2048, atlasWidth, atlasHeight, Allocator.Persistent);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var glyphIDs = new NativeList<uint>(256, Allocator.TempJob);
        var reservedGlyphIDs = new NativeList<uint>(256, Allocator.TempJob);
        for (uint codepoint = 0; codepoint < 143; codepoint++)
            glyphIDs.Add(codepoint);

        var getGlyphRectsJob = new GetGlyphRectsJob()
        {
            reservedGlyphIDs = reservedGlyphIDs,
            freeRects = nativeAtlas.freeRects,
            usedRects = nativeAtlas.usedRects,
            glyphIDs = glyphIDs,
            font = font,
            drawFunct = drawFunct,
        };
        state.Dependency= getGlyphRectsJob.Schedule(state.Dependency);

        var populateAtlasTextureJob = new PopulateAtlasTextureJob()
        {
            reservedGlyphIDs = reservedGlyphIDs,
            atlasWidth = nativeAtlas.atlasWidth,
            atlasHeight = nativeAtlas.atlasHeight,
            usedRects= nativeAtlas.usedRects,
            textureData = nativeAtlas.textureData,
            font = font,
            drawFunct = drawFunct,
            marker = marker3,
        };
        state.Dependency = populateAtlasTextureJob.Schedule(reservedGlyphIDs, 1, state.Dependency);        

        glyphIDs.Dispose(state.Dependency);
        reservedGlyphIDs.Dispose(state.Dependency);
        state.Enabled = false;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        blob.Dispose();
        face.Dispose();
        font.Dispose();
        nativeAtlas.Dispose();
        HB.hb_draw_funcs_destroy(drawFunct);
    }
    void LoadFont(int pointSize)
    {
        blob = new Blob("Assets\\Resources\\Garamond\\GaramondPremrPro.otf");    
        face = new Face(blob.ptr, 0);
        font = new Font(face.ptr);
        font.SetScale(pointSize, pointSize);

        drawFunct = HB.hb_draw_funcs_create();
        var moveToDelegate = new MoveToDelegate(HBDelegateProxies.HBDraw_MoveTo);
        var lineToDelegate = new MoveToDelegate(HBDelegateProxies.HBDraw_LineTo);
        var quadraticToDelegate = new QuadraticToDelegate(HBDelegateProxies.HBDraw_QuadraticTo);
        var cubicToDelegate = new CubicToDelegate(HBDelegateProxies.HBDraw_CubicTo);
        var releaseDelegate = new ReleaseDelegate(HBDelegateProxies.Test);

        HB.hb_draw_funcs_set_move_to_func(drawFunct, moveToDelegate, IntPtr.Zero, releaseDelegate);
        HB.hb_draw_funcs_set_line_to_func(drawFunct, lineToDelegate, IntPtr.Zero, releaseDelegate);
        HB.hb_draw_funcs_set_quadratic_to_func(drawFunct, quadraticToDelegate, IntPtr.Zero, releaseDelegate);
        HB.hb_draw_funcs_set_cubic_to_func(drawFunct, cubicToDelegate, IntPtr.Zero, releaseDelegate);
    }    
}