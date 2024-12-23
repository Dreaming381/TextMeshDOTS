using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;


namespace TextMeshDOTS.TextProcessing
{
    //[BurstCompile]
    //public partial struct FindMissingGlyphsJob : IJobEntity
    //{
    //    [ReadOnly] public NativeParallelHashSet<uint> glyphAtlas;
    //    public NativeList<uint>.ParallelWriter missingGlyphs;
    //    public void Execute(in DynamicBuffer<GlyphOTF> glyphOTFBuffer)
    //    {
    //        var glyphOTFs = glyphOTFBuffer.AsNativeArray();
    //        for (int i = 0, ii = glyphOTFs.Length; i < ii; i++)
    //        {
    //            var glyphID = glyphOTFs[i].codepoint;
    //            if (!glyphAtlas.Contains(glyphID))
    //                missingGlyphs.AddNoResize(glyphID);
    //        }
    //    }
    //}
    [BurstCompile]
    public partial struct SortMissingGlyphJob : IJob
    {
        public NativeList<FontEntityGlyph> missingGlyphs;
        public void Execute()
        {
            missingGlyphs.Sort(new FontEntityGlyphComparer());
        }
    }
    [BurstCompile]
    public partial struct CopyMissingGlyphsToFontEntitiesJob : IJobEntity
    {
        [ReadOnly] public NativeList<FontEntityGlyph> newMissingGlyphs;
        public void Execute(Entity entity, ref DynamicBuffer<MissingGlyphs> missingGlyphsBuffer)
        {
            var missingGlyphsBufferAsUint = missingGlyphsBuffer.Reinterpret<uint>();
            foreach (var glyph in newMissingGlyphs)
            {
                if (glyph.entity == entity && !missingGlyphsBufferAsUint.AsNativeArray().Contains(glyph.glyphID))
                    missingGlyphsBufferAsUint.Add(glyph.glyphID);
            }
        }
    }
}
