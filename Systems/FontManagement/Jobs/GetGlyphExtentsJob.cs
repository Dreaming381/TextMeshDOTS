using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Entities;


namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    struct GetGlyphExtentsJob : IJobParallelFor
    {
        public NativeList<GlyphExtents>.ParallelWriter glyphExtents;

        public Entity fontEntity;
        [ReadOnly] public ComponentLookup<NativeFontPointer> nativeFontPointerLookup;
        [ReadOnly] public DynamicBuffer<uint> missingGlyphsBuffer;

        public void Execute(int index)
        {            
            var nativeFontPointer = nativeFontPointerLookup[fontEntity];
            var font = nativeFontPointer.font;
            
            var glyphID = missingGlyphsBuffer[index];
            font.GetGlyphExtents(glyphID, out GlyphExtents glyphExtent);
            glyphExtents.AddNoResize(glyphExtent);           
        }
    }    
}
