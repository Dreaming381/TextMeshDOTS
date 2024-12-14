using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using TextMeshDOTS;
using TextMeshDOTS.Collections;
using UnityEngine.TextCore;

namespace HarfBuzz.SDF
{
    [BurstCompile]
    struct SpawnNativeFontJob : IJob
    {
        public ComponentLookup<FontTextureReference> fontTextureReferenceLookup;
        public BufferLookup<GlyphsInUse> glyphsInUseLookup;

        public Entity fontEntity;
        [ReadOnly] public ComponentLookup<HBFontPointer> hbFontPointerLookup;
        [ReadOnly] public ComponentLookup<HBFontAssetRef> hbFontAssetRefLookup;
        public int atlasSamplingPointSize;//size of font in atlas
        public int atlasWidth;
        public int atlasHeight;
        public int padding;
        //public NativeHashMap<int, FontTextureReference> fontTextureReferenceMap;
        //[ReadOnly] public NativeHashMap<uint, RectInt> glyphRects;
        [ReadOnly] public NativeList<GlyphBlob> hbGlyphs;
        //[ReadOnly] public UnityObjectRef<Texture2D> texture2D;

        public void Execute()
        {
            var hbFontPointer = hbFontPointerLookup[fontEntity];
            var font = hbFontPointer.font;
            var face = hbFontPointer.face;
            var buffer = glyphsInUseLookup[fontEntity];
            var hbFontAssetRef= hbFontAssetRefLookup[fontEntity];
            var dynamicFontBlobRef = CreateDynamicFontData(atlasSamplingPointSize, atlasWidth, atlasHeight, padding, hbGlyphs, face, font, hbFontAssetRef, buffer.Reinterpret<uint>());
            //var fontTextureReference = new FontTextureReference { texture = texture2D, blob = dynamicFontBlobRef };
            //fontTextureReferenceLookup[fontEntity] = fontTextureReference;
            var fontTextureReference = fontTextureReferenceLookup[fontEntity];
            fontTextureReference.blob = dynamicFontBlobRef;
            fontTextureReferenceLookup[fontEntity] = fontTextureReference;

            //fontTextureReferenceMap.Add(hbFontAssetRef.fontAssetRef.familyNameHash, fontTextureReference);
        }
        public static BlobAssetReference<DynamicFontBlob> CreateDynamicFontData(
            int atlasSamplingPointSize,
            int atlasWidth,
            int atlasHeight,
            int padding,
            NativeList<GlyphBlob> hbGlyphs,
            Face face, 
            Font font,
            HBFontAssetRef hbFontAssetRef, 
            DynamicBuffer<uint> usedGlyphs)
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
            fontBlobRoot.familyName = hbFontAssetRef.family;
            fontBlobRoot.styleName = hbFontAssetRef.subFamily;
            fontBlobRoot.fontAssetRef = hbFontAssetRef.fontAssetRef;

            fontBlobRoot.atlasSamplingPointSize = atlasSamplingPointSize;
            fontBlobRoot.atlasWidth = atlasWidth;
            fontBlobRoot.atlasHeight = atlasHeight;
            //fontBlobRoot.materialPadding = fontAsset.material.GetPaddingForText(false, false);
            fontBlobRoot.materialPadding = padding;
            fontBlobRoot.regularStyleSpacing = 0;
            fontBlobRoot.boldStyleSpacing = 7;
            fontBlobRoot.italicsStyleSlant = 35;
            fontBlobRoot.tabWidth = face.UnitsPerEM/100; //review what to set here
            fontBlobRoot.tabMultiple = 10;

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

            int count = hbGlyphs.Length == 0 ? 1 : hbGlyphs.Length;
            var characterHashMapBuilder = builder.AllocateHashMap(ref fontBlobRoot.glyphs, count);
            foreach (var glyph in hbGlyphs)
            {
                usedGlyphs.Add(glyph.glyphID);
                var glyphBlob = new GlyphBlob { glyphID = glyph.glyphID, glyphExtents = glyph.glyphExtents, glyphRect = glyph.glyphRect };
                characterHashMapBuilder.Add(glyph.glyphID, glyphBlob);
            }

            var result = builder.CreateBlobAssetReference<DynamicFontBlob>(Allocator.Persistent);
            builder.Dispose();
            fontBlobRoot = result.Value; //is this really needed as it was just constructed in place?
            return result;
        }
    }    
}