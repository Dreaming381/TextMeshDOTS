using HarfBuzz;
using System;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using Font = HarfBuzz.Font;
using Buffer = HarfBuzz.Buffer;
using Unity.Collections;
using Unity.Profiling;
using HarfBuzz.SDF;
using UnityEngine.TextCore;

[DisableAutoCreation]
partial struct TestSystem : ISystem
{
    static readonly ProfilerMarker marker = new ProfilerMarker("Glyph");
    static readonly ProfilerMarker marker2 = new ProfilerMarker("Atlas");
    static readonly ProfilerMarker marker3 = new ProfilerMarker("SDF");
    Blob blob;
    Face face;
    Font font;
    Buffer buffer;
    IntPtr drawFunct;
    BezierData bezierData;
    NativeTextureAtlas nativeTextureAtlas;
    Language language;
    BBox bbox;
    GlyphRect glyphRect;
    int atlasWidth;
    int atlasHeight;

    //[BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        LoadFont(50);
        atlasWidth = atlasHeight = 512;
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.Alpha8, false);
        var rawTextureData = texture2D.GetRawTextureData<byte>();
        nativeTextureAtlas = new NativeTextureAtlas(rawTextureData, 2048, atlasWidth, atlasHeight, Allocator.Persistent);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        for (uint codepoint = 2; codepoint < 128; codepoint++)
        {
            marker.Begin();
            bezierData.Clear();
            GetGlyphBezierData(codepoint, ref bezierData, font, drawFunct);
            marker.End();
            bbox = bezierData.glyphRect;
            if (bbox.max.x == float.NegativeInfinity || bbox.max.y == float.NegativeInfinity)
            {
                Debug.LogError($"GlyphID {codepoint} not Found! {bbox.min.x},{bbox.min.y}, width: {bbox.Width} height: {bbox.Height}");
                continue;
            }
            //Debug.Log($"Try to place glyphID {codepoint} {bbox.min.x},{bbox.min.y}, width: {bbox.Width} height: {bbox.Height}");
            glyphRect = new GlyphRect(0, 0, (int)bbox.Width, (int)bbox.Height);
            var addedGlyph = nativeTextureAtlas.TryAddGlyph(codepoint, ref glyphRect);
            if (addedGlyph)
            {
                marker3.Begin();
                //Debug.Log($"placed glyphID {codepoint} at {glyphRect.x},{glyphRect.y}, width: {glyphRect.width} height: {glyphRect.height}");
                SDF.SDFGenerateSubDivision(ref bezierData, SDFCommon.DEFAULT_SPREAD, nativeTextureAtlas.textureData, glyphRect, atlasWidth, atlasHeight);
                //SDFFixedPoint.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, ref rawTextureData, width, height);
                //SDF.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, ref rawTextureData, width, height);
                marker3.End();
            }
            else
            {
                Debug.Log($"Atlas is full at id {codepoint}");
                break;
            }
        }
        //marker.Begin();
        //SDFFixedPoint.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, ref rawTextureData, width, height);
        //marker.End();

        //marker.Begin();
        //SDF.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, rawTextureData, width, height);
        //marker.End();

        //marker.Begin();
        //SDF.SDFGenerateSubDivision(ref bezierData, SDFCommon.DEFAULT_SPREAD, rawTextureData, width, height);
        //marker.End();

        state.Enabled = false;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        blob.Dispose();
        face.Dispose();
        font.Dispose();
        buffer.Dispose();
        bezierData.Dispose();
        nativeTextureAtlas.Dispose();
        HB.hb_draw_funcs_destroy(drawFunct);
    }
    void LoadFont(int pointSize)
    {
        blob = new Blob("Assets\\Resources\\Garamond\\GaramondPremrPro.otf");
        //blobBold = new Blob("Assets\\Resources\\GaramondPremrPro-Bd.otf");
        //blobRegular = new Blob("Assets\\Resources\\LiberationSans.ttf");        
        face = new Face(blob.ptr, 0);
        font = new Font(face.ptr);
        font.SetScale(pointSize, pointSize);
        language = new Language(HB.HB_TAG('A', 'P', 'P', 'H'));
        buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);

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

        bezierData = new BezierData(64, 16, Allocator.Persistent);
    }
    void ResetBuffer()
    {
        buffer.Reset();
        buffer.Language = language;
        buffer.Script = Script.Latin;
        buffer.Direction = Direction.LeftToRight;
    }
    static void ShapeGlyph(string glyph, Font font, Buffer buffer, out GlyphInfo glyphInfo)
    {
        buffer.AddText(glyph);
        HB.hb_shape(font.ptr, buffer.ptr, IntPtr.Zero, 0);

        var glyphInfos = buffer.GlyphInfo();
        //var glyphPositions = buffer.GlyphPositions();
        glyphInfo = glyphInfos[0];
        //var glyphPos = glyphPositions[0];
        //Debug.Log($"draw glyph {glyphInfo.codepoint} {glyphPos}");
        //font.GetGlyphExtends(glyphInfo.codepoint, out GlyphExtents glyphExtents);
        //Debug.Log($"Glyph extend: {glyphExtents}");
    }
    static void GetGlyphBezierData(uint glyphID, ref BezierData bezierData, Font font, IntPtr drawFunct)
    {
        HB.hb_font_draw_glyph(font.ptr, glyphID, drawFunct, ref bezierData);
        bezierData.contourIDs.Add(bezierData.edges.Length);//close the last contour

        //Debug.Log($"Glyph has {bezierData.contourIDs.Length-1} contours.  {bezierData.glyphRect}");
        //Debug.Log($"BBox: {bezierData.glyphRect} (before padding)");
        bezierData.glyphRect.Expand(9);
        //Debug.Log($"BBox: {bezierData.glyphRect} (after padding)");

        var edges = bezierData.edges;
        var shift = -bezierData.glyphRect.min;
        for (int i = 0, length = edges.Length; i < length; i++)
        {
            ref var edge = ref edges.ElementAt(i);
            edge.start_pos += shift;
            edge.end_pos += shift;
            edge.control1 += shift;
            edge.control2 += shift;
            //Debug.Log($"From {edge.start_pos} {edge.end_pos}");
        }
        bezierData.glyphRect.min = bezierData.glyphRect.min + shift;
        bezierData.glyphRect.max = bezierData.glyphRect.max + shift;
        //Debug.Log($"BBox: {bezierData.glyphRect} (after shifting)");
    }
}
