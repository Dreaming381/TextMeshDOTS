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

[DisableAutoCreation]
partial struct TestSystem : ISystem
{
    static readonly ProfilerMarker marker = new ProfilerMarker("SDF");
    Blob blob;
    Face face;
    Font font;
    Buffer buffer;
    BezierData bezierData;
    NativeArray<byte> rawTextureData;

    //[BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        blob = new Blob("Assets\\Resources\\Garamond\\GaramondPremrPro.otf");
        //blobBold = new Blob("Assets\\Resources\\GaramondPremrPro-Bd.otf");
        //blobRegular = new Blob("Assets\\Resources\\LiberationSans.ttf");        
        face = new Face(blob.ptr, 0);
        font = new Font(face.ptr);
        font.SetScale(50, 50);
        //Debug.Log($"FaceCount {blobRegular.FaceCount} GlyphCount {faceRegular.GlyphCount} {font.GetStyleTag(StyleTag.Weight)} {font.GetStyleTag(StyleTag.Width)}");

        bezierData = new BezierData();
        bezierData.edges = new NativeList<SDFEdge>(16, Allocator.Persistent);

        var language = new Language(HB.HB_TAG('A', 'P', 'P', 'H'));
        buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);

        buffer.AddText("W");
        HB.hb_shape(font.ptr, buffer.ptr, IntPtr.Zero, 0);

        var glyphInfos = buffer.GlyphInfo();
        var glyphPositions = buffer.GlyphPositions();
        var glyphInfo = glyphInfos[0];
        var glyphPos = glyphPositions[0];
        //Debug.Log($"draw glyph {glyphInfo.codepoint} {glyphPos}");

        var drawFunct = HB.hb_draw_funcs_create();
        MoveToDelegate moveToDelegate = new MoveToDelegate(HBDelegateProxies.hb_draw_extents_move_to);
        MoveToDelegate lineToDelegate = new MoveToDelegate(HBDelegateProxies.hb_draw_extents_line_to);
        QuadraticToDelegate quadraticToDelegate = new QuadraticToDelegate(HBDelegateProxies.hb_draw_extents_quadratic_to);
        CubicToDelegate cubicToDelegate = new CubicToDelegate(HBDelegateProxies.hb_draw_extents_cubic_to);
        ReleaseDelegate releaseDelegate = new ReleaseDelegate(HBDelegateProxies.Test);

        HB.hb_draw_funcs_set_move_to_func(drawFunct, moveToDelegate, IntPtr.Zero, releaseDelegate);
        HB.hb_draw_funcs_set_line_to_func(drawFunct, lineToDelegate, IntPtr.Zero, releaseDelegate);
        HB.hb_draw_funcs_set_quadratic_to_func(drawFunct, quadraticToDelegate, IntPtr.Zero, releaseDelegate);
        HB.hb_draw_funcs_set_cubic_to_func(drawFunct, cubicToDelegate, IntPtr.Zero, releaseDelegate);

        HB.hb_font_draw_glyph(font.ptr, glyphInfo.codepoint, drawFunct, ref bezierData);
        HB.hb_draw_funcs_destroy(drawFunct);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        //var point = new float2(20, 20);
        //foreach (SDFEdge edge in bezierData.edges)
        //{
        //    Debug.Log($"From {edge.start_pos} {edge.end_pos}");
        //    //SDFEdgeGetMinDistance(edge, point, ref bla);
        //    //Debug.Log($"sign {bla.sign} {bla.distance:F2} cross {bla.cross:F2} ");
        //}

        int width = 64;
        int height = 64;
        rawTextureData = new NativeArray<byte>(width * height, Allocator.Temp);

        marker.Begin();
        SDFFixedPoint.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, ref rawTextureData, width, height);
        marker.End();

        marker.Begin();
        SDF.SDFGenerate(bezierData.edges, SDFCommon.DEFAULT_SPREAD, ref rawTextureData, width, height);
        marker.End();

        marker.Begin();
        SDF.SDFGenerateSubDivision(bezierData.edges, SDFCommon.DEFAULT_SPREAD, rawTextureData, width, height);
        marker.End();

        //marker.Begin();
        //for (int i = 0, length = 640000; i < length; i++);
        //    SDF.MUL_26D6(37, 16, 6);
        //marker.End();

        //marker.Begin();
        //for (int i = 0, length = 640000; i < length; i++) ;
        //SDF.MUL_26D6(37, 16);
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
        bezierData.edges.Dispose();
    }
}
