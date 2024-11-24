using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using HarfBuzz;


namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    public partial struct FindMissingGlyphsJob : IJobEntity
    {
        [ReadOnly] public NativeParallelHashSet<uint> glyphAtlas;
        public NativeList<uint>.ParallelWriter missingGlyphs;
        public void Execute(in DynamicBuffer<GlyphOTF> glyphOTFBuffer)
        {
            var glyphOTFs = glyphOTFBuffer.AsNativeArray();
            for (int i = 0, ii = glyphOTFs.Length; i < ii; i++)
            {
                var glyphID = glyphOTFs[i].codepoint;
                if (!glyphAtlas.Contains(glyphID))
                    missingGlyphs.AddNoResize(glyphID);
            }
        }
    }
}
