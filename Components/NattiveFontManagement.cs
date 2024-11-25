using HarfBuzz;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS
{
    public class FontAssetReference : IComponentData
    {
        public FontAsset value;
    }
    public struct FontAtlasInfo : IComponentData
    {
        public NativeParallelHashSet<uint> glyphAtlas;
        public NativeList<uint> missingGlyphs;
    }
    public class FontManager : IComponentData
    {
        public Dictionary<string, NativeFont> fontDictionary;
        public Dictionary<string, FontAsset> fontAssets;
    }
    public struct NativeFont : IComponentData
    {
        public bool isCreated;
        public Face nativeFace;
        public Font nativeFont;
        public Blob nativeBlob;

        public float designSize;
        public float subfamilyNameID;
        public float rangeStart;
        public float rangeEnd;
        public float unitsPerEm;
        public float xScale;
        public float yScale;
        public float baseline;

        public float capHeight;
        public float xHeight;

        public float subScriptEmXSize;
        public float subScriptEmYSize;
        public float subScriptEmXOffset;
        public float subScriptEmYOffset;

        public float superScriptEmXSize;
        public float superScriptEmYSize;
        public float superScriptEmXOffset;
        public float superScriptEmYOffset;

        public FontExtents GetFontExtents(Direction direction)
        {
            nativeFont.GetFontExtentsForDirection(direction, out FontExtents fontExtents);
            return fontExtents;
        }
        //public NativeFont(string path)
        unsafe public NativeFont(ref FontBlob fontblob, uint length)
        {
            isCreated = true;
            nativeBlob = new Blob(fontblob.nativeFontFile.GetUnsafePtr(), length, MemoryMode.Readonly);
            //nativeBlob = new Blob(path);
            nativeFace = new Face(nativeBlob.ptr, 0);
            nativeFont = new Font(nativeFace.ptr);

            nativeFont.MakeImmutable();

            //Debug.Log($"Loaded? {path} Blob:{nativeBlob.ptr != IntPtr.Zero} (Length:{nativeBlob.Length}) Face:{nativeFace.ptr != IntPtr.Zero} Font:{nativeFont.ptr != IntPtr.Zero}");
            //Debug.Log($"Loaded? {fontblob.name} Blob:{nativeBlob.ptr != IntPtr.Zero} (Length:{nativeBlob.Length}) Face:{nativeFace.ptr != IntPtr.Zero} Font:{nativeFont.ptr != IntPtr.Zero}");

            unitsPerEm = nativeFace.UnitsPerEM;
            //nativeFont.GetBaseline(Direction.LeftToRight, HB.hb_language_from_string("en"));
            baseline = 0;

            nativeFace.GetSizeParams(out uint design_size, out uint subfamily_id, out uint subfamily_name_id, out uint range_start, out uint range_end);
            nativeFont.GetScale(out int x_scale, out int y_scale);

            nativeFont.GetMetrics(MetricTag.CapHeight, out int capHeight);
            nativeFont.GetMetrics(MetricTag.XHeight, out int xHeight);

            nativeFont.GetMetrics(MetricTag.SubScriptEmXSize, out int subScriptEmXSize);
            nativeFont.GetMetrics(MetricTag.SubScriptEmYSize, out int subScriptEmYSize);
            nativeFont.GetMetrics(MetricTag.SubScriptEmXOffset, out int subScriptEmXOffset);
            nativeFont.GetMetrics(MetricTag.SubScriptEmYOffset, out int subScriptEmYOffset);

            nativeFont.GetMetrics(MetricTag.SuperScriptEmXSize, out int superScriptEmXSize);
            nativeFont.GetMetrics(MetricTag.SuperScriptEmYSize, out int superScriptEmYSize);
            nativeFont.GetMetrics(MetricTag.SuperScriptEmXOffset, out int superScriptEmXOffset);
            nativeFont.GetMetrics(MetricTag.SuperScriptEmYOffset, out int superScriptEmYOffset);

            this.designSize = design_size;
            this.subfamilyNameID = subfamily_name_id;
            this.rangeStart = range_start;
            this.rangeEnd = range_end;

            this.xScale = x_scale;
            this.yScale = y_scale;

            this.capHeight = capHeight;
            this.xHeight = xHeight;

            this.subScriptEmXSize = subScriptEmXSize;
            this.subScriptEmYSize = subScriptEmYSize;
            this.subScriptEmXOffset = subScriptEmXOffset;
            this.subScriptEmYOffset = subScriptEmYOffset;

            this.superScriptEmXSize = superScriptEmXSize;
            this.superScriptEmYSize = superScriptEmYSize;
            this.superScriptEmXOffset = superScriptEmXOffset;
            this.superScriptEmYOffset = superScriptEmYOffset;
        }
    }
}
