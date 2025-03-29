using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using System;
using Unity.Collections.LowLevel.Unsafe;


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
    public partial struct ClearMissingGlyphJob : IJob
    {
        public NativeList<FontEntityGlyph> missingGlyphs;
        public void Execute()
        {
            missingGlyphs.Clear();
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
