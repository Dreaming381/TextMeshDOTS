using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using TextMeshDOTS.HarfBuzz;
using Unity.Burst.Intrinsics;
using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;


namespace TextMeshDOTS.TextProcessing
{
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
            var missingGlyphs = missingGlyphsBuffer.Reinterpret<uint>();
            foreach (var glyph in newMissingGlyphs)
            {
                if (glyph.entity == entity && !missingGlyphs.Contains(glyph.glyphID))
                    missingGlyphs.Add(glyph.glyphID);
            }
        }
    }
    [BurstCompile]
    public partial struct UpdateMissingGlyphsJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public BufferLookup<MissingGlyphs> missingGlyphsLookup;
        public BufferLookup<UsedGlyphs> usedGlyphsLookup;
        [NativeDisableContainerSafetyRestriction]
        NativeList<FontEntityGlyph> requiredGlyphs;
        public void Execute(Entity entity, in DynamicBuffer<GlyphOTF> glyphOTFBuffer)
        {
            requiredGlyphs.Clear();
            if (glyphOTFBuffer.Length==0) 
                return;
            for (int i = 0 , ii = glyphOTFBuffer.Length; i<ii; i++)
            {
                var glyphOTF=glyphOTFBuffer[i];
                var fontEntityGlyph = new FontEntityGlyph { entity = glyphOTF.fontEntity, glyphID = glyphOTF.codepoint };
                if (!requiredGlyphs.Contains(fontEntityGlyph))
                    requiredGlyphs.Add(fontEntityGlyph);
            }
            requiredGlyphs.Sort(new FontEntityGlyphComparer());
            int nextID = 0;
            var currentFont = requiredGlyphs[nextID].entity;
            var requiredGlyph = requiredGlyphs[nextID];
            while (currentFont != Entity.Null && nextID < requiredGlyphs.Length)
            {                
                var usedGlyphs = usedGlyphsLookup[requiredGlyph.entity].Reinterpret<uint>();
                var missingGlyphs = missingGlyphsLookup[requiredGlyph.entity].Reinterpret<uint>();
                var startFont = currentFont;
                while (currentFont == startFont)
                {
                    if(!usedGlyphs.Contains(requiredGlyph.glyphID) && !missingGlyphs.Contains(requiredGlyph.glyphID))
                        missingGlyphs.Add(requiredGlyph.glyphID);
                    nextID++;
                    if (nextID < requiredGlyphs.Length)
                    {                         
                        requiredGlyph = requiredGlyphs[nextID];
                        currentFont = requiredGlyph.entity;
                    }
                    else
                        currentFont = Entity.Null;
                }
            }
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!requiredGlyphs.IsCreated)
                requiredGlyphs = new NativeList<FontEntityGlyph>(1024, Allocator.Temp);            
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
        {
        }
    }
    public unsafe static class DynamicBufferExtensions
    {
        
        /// <summary>
        /// Returns true if a particular value is present in this array.
        /// </summary>
        /// <typeparam name="T">The type of elements in this array.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="array">The array to search.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>True if the value is present in this array.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(int) })]
        public static bool Contains<T, U>(this DynamicBuffer<T> array, U value) where T : unmanaged, IEquatable<U>
        {
            return IndexOf<T, U>(array.GetUnsafeReadOnlyPtr(), array.Length, value) != -1;
        }
        /// <summary>
        /// Finds the index of the first occurrence of a particular value in a buffer.
        /// </summary>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        /// <typeparam name="U">The value type.</typeparam>
        /// <param name="ptr">A buffer.</param>
        /// <param name="length">Number of elements in the buffer.</param>
        /// <param name="value">The value to locate.</param>
        /// <returns>The index of the first occurrence of the value in the buffer. Returns -1 if no occurrence is found.</returns>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(int) })]
        public static int IndexOf<T, U>(void* ptr, int length, U value) where T : unmanaged, IEquatable<U>
        {
            for (int i = 0; i != length; i++)
            {
                if (UnsafeUtility.ReadArrayElement<T>(ptr, i).Equals(value))
                    return i;
            }
            return -1;
        }
    }
}
