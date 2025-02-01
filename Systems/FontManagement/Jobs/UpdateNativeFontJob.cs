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
                dynamicFontAsset.blob = CreateDynamicFontData(ref atlasData, placedGlyphs, face, font);
                //Debug.Log($"Create new blob to add {placedGlyphs.Length} glyphs");
            }            
            dynamicFontAssetLookup[fontEntity] = dynamicFontAsset;
        }
        public static BlobAssetReference<DynamicFontBlob> CreateDynamicFontData(
            ref AtlasData atlasData,
            NativeList<GlyphBlob> placedGlyphs,
            Face face, 
            Font font)
        {
            //first, get all native data
            font.GetBaseline(Direction.LTR, Script.LATIN, out int baseLine);
            font.GetFontExtentsForDirection(Direction.LTR, out FontExtents fontExtents);

            face.GetSizeParams(out uint design_size, out uint subfamily_id, out uint subfamily_name_id, out uint range_start, out uint range_end);

            font.GetMetrics(MetricTag.CAP_HEIGHT, out int capHeight);
            font.GetMetrics(MetricTag.X_HEIGHT, out int xHeight);

            font.GetMetrics(MetricTag.SUBSCRIPT_EM_X_SIZE, out int subScriptEmXSize);
            font.GetMetrics(MetricTag.SUBSCRIPT_EM_Y_SIZE, out int subScriptEmYSize);
            font.GetMetrics(MetricTag.SUBSCRIPT_EM_X_OFFSET, out int subScriptEmXOffset);
            font.GetMetrics(MetricTag.SUBSCRIPT_EM_Y_OFFSET, out int subScriptEmYOffset);

            font.GetMetrics(MetricTag.SUPERSCRIPT_EM_X_SIZE, out int superScriptEmXSize);
            font.GetMetrics(MetricTag.SUPERSCRIPT_EM_Y_SIZE, out int superScriptEmYSize);
            font.GetMetrics(MetricTag.SUPERSCRIPT_EM_X_OFFSET, out int superScriptEmXOffset);
            font.GetMetrics(MetricTag.SUPERSCRIPT_EM_Y_OFFSET, out int superScriptEmYOffset);

            var scale = font.GetScale();
            var upem = face.GetUnitsPerEM;

            //get width of space -->is there no easier way to do this?        
            var language = new Language(HB.HB_TAG('E', 'N', 'G', ' '));
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

            fontBlobRoot.designSize = design_size;
            fontBlobRoot.subfamilyNameID = subfamily_name_id;
            fontBlobRoot.rangeStart = range_start;
            fontBlobRoot.rangeEnd = range_end;
            fontBlobRoot.unitsPerEm = face.GetUnitsPerEM;
            fontBlobRoot.scale = font.GetScale(); 

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

            int count = placedGlyphs.Length == 0 ? 1 : placedGlyphs.Length;
            var characterHashMapBuilder = builder.AllocateHashMap(ref fontBlobRoot.glyphs, count);
            foreach (var glyph in placedGlyphs)
            {
                var glyphBlob = new GlyphBlob { glyphID = glyph.glyphID, glyphExtents = glyph.glyphExtents, glyphRect = glyph.glyphRect };
                characterHashMapBuilder.Add(glyph.glyphID, glyphBlob);
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

            fontBlobRoot.designSize = dynamicFontData.designSize;
            fontBlobRoot.subfamilyNameID = dynamicFontData.subfamilyNameID;
            fontBlobRoot.rangeStart = dynamicFontData.rangeStart;
            fontBlobRoot.rangeEnd = dynamicFontData.rangeEnd;
            fontBlobRoot.unitsPerEm = dynamicFontData.unitsPerEm;
            fontBlobRoot.scale = dynamicFontData.scale;

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
            //replace existing blob with new blob, dispose old blob
            //unsafe
            //{
            //    var a = (BlobAssetReference<DynamicFontBlob>*)dynamicFontDataReference.GetUnsafePtr();
            //    var b = (BlobAssetReference<DynamicFontBlob>*)result.GetUnsafePtr();
            //    var temp = *a;
            //    *a = *b;
            //    *b = temp;
            //}            
            //result.Dispose();
        }
    }    
}