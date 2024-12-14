using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TextCore.Text;
using HarfBuzz;
using UnityEngine.TextCore;
using UnityEngine;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS.Authoring
{
    public static class FontBlobber
    {
        public static BlobAssetReference<FontBlob> BakeFontBlob(FontAsset fontAsset, TextFontWeight textFontWeight, bool isItalic)
        {
            fontAsset.material.SetFloat("_WeightNormal", fontAsset.regularStyleWeight);
            fontAsset.material.SetFloat("_WeightBold", fontAsset.boldStyleWeight);

            var          builder             = new BlobBuilder(Allocator.Temp);
            ref FontBlob fontBlobRoot        = ref builder.ConstructRoot<FontBlob>();

            //create references to load font data at runtime
            var faceInfo = fontAsset.faceInfo;
            fontBlobRoot.familyName = faceInfo.familyName;
            fontBlobRoot.styleName = faceInfo.styleName;
            fontBlobRoot.fontAssetRef = new FontAssetRef(TextHelper.GetHashCodeCaseInSensitive(faceInfo.familyName), textFontWeight, isItalic);

            var result = builder.CreateBlobAssetReference<FontBlob>(Allocator.Persistent);
            builder.Dispose();
            fontBlobRoot = result.Value;
            return result;
        }

        public static BlobAssetReference<DynamicFontBlob> CreateDynamicFontData(FontAsset fontAsset, Face face,Font font, FontAssetRef fontAssetRef, DynamicBuffer<uint> usedGlyphs)
        {
            //first, get all native data
            font.GetBaseline(Direction.LeftToRight, Script.Latin, out int baseLine);
            font.GetFontExtentsForDirection(Direction.LeftToRight, out FontExtents fontExtents);

            face.GetSizeParams(out uint design_size, out uint subfamily_id, out uint subfamily_name_id, out uint range_start, out uint range_end);
            font.GetScale(out int x_scale, out int y_scale);

            font.GetMetrics(MetricTag.CapHeight, out int capHeight);
            font.GetMetrics(MetricTag.XHeight, out int xHeight);

            font.GetMetrics(MetricTag.SubScriptEmXSize, out int subScriptEmXSize);
            font.GetMetrics(MetricTag.SubScriptEmYSize, out int subScriptEmYSize);
            font.GetMetrics(MetricTag.SubScriptEmXOffset, out int subScriptEmXOffset);
            font.GetMetrics(MetricTag.SubScriptEmYOffset, out int subScriptEmYOffset);

            font.GetMetrics(MetricTag.SuperScriptEmXSize, out int superScriptEmXSize);
            font.GetMetrics(MetricTag.SuperScriptEmYSize, out int superScriptEmYSize);
            font.GetMetrics(MetricTag.SuperScriptEmXOffset, out int superScriptEmXOffset);
            font.GetMetrics(MetricTag.SuperScriptEmYOffset, out int superScriptEmYOffset);

            var builder = new BlobBuilder(Allocator.Temp);
            ref DynamicFontBlob fontBlobRoot = ref builder.ConstructRoot<DynamicFontBlob>();

            //second, copy over some data from FontAsset which is set by user and does not come from native font
            fontBlobRoot.familyName = fontAsset.faceInfo.familyName;
            fontBlobRoot.styleName = fontAsset.faceInfo.styleName;
            fontBlobRoot.fontAssetRef = fontAssetRef;

            fontBlobRoot.atlasSamplingPointSize = fontAsset.faceInfo.pointSize;
            fontBlobRoot.atlasWidth = fontAsset.atlasWidth;
            fontBlobRoot.atlasHeight = fontAsset.atlasHeight;
            fontBlobRoot.materialPadding = fontAsset.material.GetPaddingForText(false, false); 
            fontBlobRoot.regularStyleSpacing = fontAsset.regularStyleSpacing;
            fontBlobRoot.boldStyleSpacing = fontAsset.boldStyleSpacing;
            fontBlobRoot.italicsStyleSlant = fontAsset.italicStyleSlant;
            fontBlobRoot.tabWidth = fontAsset.faceInfo.tabWidth;
            fontBlobRoot.tabMultiple = fontAsset.tabMultiple;

            //third, copy over native font data
            fontBlobRoot.ascender = fontExtents.ascender;
            fontBlobRoot.descender = fontExtents.descender;
            fontBlobRoot.baseLine = baseLine;

            fontBlobRoot.designSize = design_size;
            fontBlobRoot.subfamilyNameID = subfamily_name_id;
            fontBlobRoot.rangeStart = range_start;
            fontBlobRoot.rangeEnd = range_end;
            fontBlobRoot.unitsPerEm = face.UnitsPerEM;
            fontBlobRoot.xScale = x_scale;
            fontBlobRoot.yScale = y_scale;

            fontBlobRoot.capHeight = capHeight;
            fontBlobRoot.xHeight = xHeight;

            fontBlobRoot.subScriptEmXSize = subScriptEmXSize;
            fontBlobRoot.subScriptEmYSize = subScriptEmYSize;
            fontBlobRoot.subScriptEmXOffset = subScriptEmXOffset;
            fontBlobRoot.subScriptEmYOffset = subScriptEmYOffset;

            fontBlobRoot.superScriptEmXSize = superScriptEmXSize;
            fontBlobRoot.superScriptEmYSize = superScriptEmYSize;
            fontBlobRoot.superScriptEmXOffset = superScriptEmXOffset;
            fontBlobRoot.superScriptEmYOffset = superScriptEmYOffset;

            var glyphLookupTable = fontAsset.glyphLookupTable;
            int count = glyphLookupTable.Count==0 ? 1 : glyphLookupTable.Count;
            var characterHashMapBuilder = builder.AllocateHashMap(ref fontBlobRoot.glyphs, count);
            foreach (var glyph in glyphLookupTable.Values)
            {
                usedGlyphs.Add(glyph.index);
                characterHashMapBuilder.Add(glyph.index, new GlyphBlob {glyphID = glyph.index, glyphExtents = (GlyphExtents)glyph.metrics, glyphRect = glyph.glyphRect});
            }

            var result = builder.CreateBlobAssetReference<DynamicFontBlob>(Allocator.Persistent);
            builder.Dispose();
            fontBlobRoot = result.Value; //is this really needed as it was just constructed in place?
            return result;
        }
        public static void PatchDynamicFontData(ref this BlobAssetReference<DynamicFontBlob> dynamicFontDataReference, NativeList<GlyphBlob> newGlyphs)
        {
            ref var dynamicFontData = ref dynamicFontDataReference.Value;
            var builder = new BlobBuilder(Allocator.Temp);
            ref DynamicFontBlob fontBlobRoot = ref builder.ConstructRoot<DynamicFontBlob>();

            fontBlobRoot.familyName = dynamicFontData.familyName;
            fontBlobRoot.styleName = dynamicFontData.styleName;
            fontBlobRoot.fontAssetRef = dynamicFontData.fontAssetRef;

            fontBlobRoot.atlasSamplingPointSize = dynamicFontData.atlasSamplingPointSize;
            fontBlobRoot.atlasWidth = dynamicFontData.atlasWidth;
            fontBlobRoot.atlasHeight = dynamicFontData.atlasHeight;
            fontBlobRoot.materialPadding = dynamicFontData.materialPadding;
            fontBlobRoot.regularStyleSpacing = dynamicFontData.regularStyleSpacing;
            fontBlobRoot.boldStyleSpacing = dynamicFontData.boldStyleSpacing;
            fontBlobRoot.italicsStyleSlant = dynamicFontData.italicsStyleSlant;
            fontBlobRoot.tabWidth = dynamicFontData.tabWidth;
            fontBlobRoot.tabMultiple = dynamicFontData.tabMultiple;

            fontBlobRoot.ascender = dynamicFontData.ascender;
            fontBlobRoot.descender = dynamicFontData.descender;
            fontBlobRoot.baseLine = dynamicFontData.baseLine;

            fontBlobRoot.designSize = dynamicFontData.designSize;
            fontBlobRoot.subfamilyNameID = dynamicFontData.subfamilyNameID;
            fontBlobRoot.rangeStart = dynamicFontData.rangeStart;
            fontBlobRoot.rangeEnd = dynamicFontData.rangeEnd;
            fontBlobRoot.unitsPerEm = dynamicFontData.unitsPerEm;
            fontBlobRoot.xScale = dynamicFontData.xScale;
            fontBlobRoot.yScale = dynamicFontData.yScale;

            fontBlobRoot.capHeight = dynamicFontData.capHeight;
            fontBlobRoot.xHeight = dynamicFontData.xHeight;

            fontBlobRoot.subScriptEmXSize = dynamicFontData.subScriptEmXSize;
            fontBlobRoot.subScriptEmYSize = dynamicFontData.subScriptEmYSize;
            fontBlobRoot.subScriptEmXOffset = dynamicFontData.subScriptEmXOffset;
            fontBlobRoot.subScriptEmYOffset = dynamicFontData.subScriptEmYOffset;

            fontBlobRoot.superScriptEmXSize = dynamicFontData.superScriptEmXSize;
            fontBlobRoot.superScriptEmYSize = dynamicFontData.superScriptEmYSize;
            fontBlobRoot.superScriptEmXOffset = dynamicFontData.superScriptEmXOffset;
            fontBlobRoot.superScriptEmYOffset = dynamicFontData.superScriptEmYOffset;

            var newLength = dynamicFontDataReference.Value.glyphs.Count + newGlyphs.Length;

            var characterHashMapBuilder = builder.AllocateHashMap(ref fontBlobRoot.glyphs, newLength);

            var oldGlyphs = dynamicFontDataReference.Value.glyphs.GetValueArray(Allocator.Temp);
            for (int i = 0, length = oldGlyphs.Length; i<length; i++)
                characterHashMapBuilder.Add(oldGlyphs[i].glyphID, oldGlyphs[i]);

            for (int i = 0, length = newGlyphs.Length; i < length; i++)
                characterHashMapBuilder.Add(newGlyphs[i].glyphID, newGlyphs[i]);

            var result = builder.CreateBlobAssetReference<DynamicFontBlob>(Allocator.Persistent);
            builder.Dispose();

            //replace existing blob with new blob, dispose old blob
            unsafe
            {
                var a = (BlobAssetReference<DynamicFontBlob>*) dynamicFontDataReference.GetUnsafePtr();
                var b = (BlobAssetReference<DynamicFontBlob>*) result.GetUnsafePtr();
                var temp = *a;
                *a = *b;
                *b = temp;
            }
            result.Dispose();

        }
        
    }
}