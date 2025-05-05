using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Entities;
using TextMeshDOTS.Collections;
using TextMeshDOTS.HarfBuzz;
using UnityEngine;
using Font = TextMeshDOTS.HarfBuzz.Font;

namespace TextMeshDOTS.TextProcessing
{
    [BurstCompile]
    struct UpdateNativeFontJob : IJob
    {
        public ComponentLookup<DynamicFontAsset> dynamicFontAssetLookup;

        public Entity fontEntity;
        [ReadOnly] public ComponentLookup<NativeFontPointer> nativeFontPointerLookup;
        [ReadOnly] public ComponentLookup<AtlasData> atlasDataLookup;
        [ReadOnly] public NativeList<GlyphBlob> placedGlyphs;

        public void Execute()
        {
            var nativeFontPointer = nativeFontPointerLookup[fontEntity];
            var font = nativeFontPointer.font;
            var face = nativeFontPointer.face;
            var atlasData = atlasDataLookup[fontEntity];            

            var dynamicFontAsset = dynamicFontAssetLookup[fontEntity];
            //Debug.Log($"Trying to update DynamicFontAsset for ({nativeFontPointer.debugFamily} {nativeFontPointer.debugSubfamily})");
            if (dynamicFontAsset.blob.IsCreated)
            {
                //Debug.Log($"Patching existing blob to add {placedGlyphs.Length} glyphs");
                PatchDynamicFontData(ref dynamicFontAsset.blob, placedGlyphs);
            }
            else
            {
                //Debug.Log($"Create new blob to add {placedGlyphs.Length} glyphs on {fontEntity.ToFixedString()}");
                dynamicFontAsset.blob = CreateDynamicFontData(ref atlasData, placedGlyphs, face, font);
                
            }            
            dynamicFontAssetLookup[fontEntity] = dynamicFontAsset;
        }
        public BlobAssetReference<DynamicFontBlob> CreateDynamicFontData(
            ref AtlasData atlasData,
            NativeList<GlyphBlob> placedGlyphs,
            Face face, 
            Font font)
        {
            //first, get all native data
            font.GetBaseline(Direction.LTR, Script.LATIN, out int baseLine);
            font.GetFontExtentsForDirection(Direction.LTR, out FontExtents fontExtents);

            font.GetMetrics(MetricTag.CAP_HEIGHT, out int capHeight);
            font.GetMetrics(MetricTag.X_HEIGHT, out int xHeight);

            //get width of space -->is there no easier way to do this?        
            var language = new Language(Harfbuzz.HB_TAG('E', 'N', 'G', ' '));
            var buffer = new Buffer(Direction.LTR, Script.LATIN, language);
            buffer.ContentType = ContentType.UNICODE;
            buffer.Add(0x20, 0);
            font.Shape(buffer);
            var glyphPosition = buffer.GetGlyphPositionsSpan();
            var xWidth = glyphPosition[0].xAdvance;
            buffer.Dispose();

            var builder = new BlobBuilder(Allocator.Temp);
            ref DynamicFontBlob fontBlobRoot = ref builder.ConstructRoot<DynamicFontBlob>();

            fontBlobRoot.atlasSamplingPointSize = atlasData.samplingPointSize;
            fontBlobRoot.atlasWidth = atlasData.atlasWidth;
            fontBlobRoot.atlasHeight = atlasData.atlasHeight;
            fontBlobRoot.materialPadding = atlasData.padding;
            fontBlobRoot.regularStyleSpacing = 0;
            fontBlobRoot.boldStyleSpacing = 7;
            fontBlobRoot.italicsStyleSlant = 35;
            fontBlobRoot.tabWidth = xWidth; //typically width of space
            fontBlobRoot.tabMultiple = 10;  //tab advace = tabWidth * tabMultiple

            //third, copy over native font data
            fontBlobRoot.ascender = fontExtents.ascender;
            fontBlobRoot.descender = fontExtents.descender;
            fontBlobRoot.baseLine = baseLine;

            fontBlobRoot.capHeight = capHeight;
            fontBlobRoot.xHeight = xHeight;

            int count = placedGlyphs.Length == 0 ? 1 : placedGlyphs.Length;
            var characterHashMapBuilder = builder.AllocateHashMap(ref fontBlobRoot.glyphs, count);
            foreach (var glyph in placedGlyphs)
            {
                var glyphBlob = new GlyphBlob { glyphID = glyph.glyphID, glyphExtents = glyph.glyphExtents, glyphRect = glyph.glyphRect };
                characterHashMapBuilder.Add(glyph.glyphID, glyphBlob);
                //UnityEngine.Debug.Log($"Added glyph ID: {glyph.glyphID} to {fontEntity.ToFixedString()}");
            }

            var result = builder.CreateBlobAssetReference<DynamicFontBlob>(Allocator.Persistent);
            builder.Dispose();
            fontBlobRoot = result.Value; //is this really needed as it was just constructed in place?
            return result;
        }
        public static void PatchDynamicFontData(ref BlobAssetReference<DynamicFontBlob> dynamicFontDataReference, NativeList<GlyphBlob> newGlyphs)
        {
            ref var dynamicFontData = ref dynamicFontDataReference.Value;
            var builder = new BlobBuilder(Allocator.Temp);
            ref DynamicFontBlob fontBlobRoot = ref builder.ConstructRoot<DynamicFontBlob>();

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

            fontBlobRoot.capHeight = dynamicFontData.capHeight;
            fontBlobRoot.xHeight = dynamicFontData.xHeight;

            var newLength = dynamicFontData.glyphs.Count + newGlyphs.Length;

            var characterHashMapBuilder = builder.AllocateHashMap(ref fontBlobRoot.glyphs, newLength);

            var oldGlyphs = dynamicFontData.glyphs.GetValueArray(Allocator.Temp);
            for (int i = 0, length = oldGlyphs.Length; i < length; i++)
                characterHashMapBuilder.Add(oldGlyphs[i].glyphID, oldGlyphs[i]);

            for (int i = 0, length = newGlyphs.Length; i < length; i++)
                characterHashMapBuilder.Add(newGlyphs[i].glyphID, newGlyphs[i]);

            var result = builder.CreateBlobAssetReference<DynamicFontBlob>(Allocator.Persistent);
            builder.Dispose();

            dynamicFontDataReference.Dispose();
            dynamicFontDataReference = result;
        }
    }    
}