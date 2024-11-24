using UnityEngine;
using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TextCore.Text;

namespace TextMeshDOTS.Authoring
{
    public static class FontBlobber
    {
        public static unsafe BlobAssetReference<FontBlob> BakeFont(FontAsset font)
        {
            var faceInfo = font.faceInfo;
            font.material.SetFloat("_WeightNormal", font.regularStyleWeight);
            font.material.SetFloat("_WeightBold", font.boldStyleWeight);
            float materialPadding = font.material.GetPaddingForText(false, false);

            var          builder             = new BlobBuilder(Allocator.Temp);
            ref FontBlob fontBlobRoot        = ref builder.ConstructRoot<FontBlob>();
            fontBlobRoot.name                = font.name;
            fontBlobRoot.atlasSamplingPointSize = faceInfo.pointSize;            

            //fontBlobRoot.scale               = faceInfo.scale;
            //fontBlobRoot.pointSize           = faceInfo.pointSize;         //opsz: optical-size
            //fontBlobRoot.baseLine            = faceInfo.baseline;          //romn: roman
            //fontBlobRoot.ascentLine          = faceInfo.ascentLine;        //hasc: horizontal - ascender
            //fontBlobRoot.descentLine         = faceInfo.descentLine;       //hdsc: horizontal-descender
            //fontBlobRoot.capLine             = faceInfo.capLine;           //cpht: cap-height
            //fontBlobRoot.meanLine            = faceInfo.meanLine;          //xhgt: x-height
            //fontBlobRoot.lineHeight          = faceInfo.lineHeight;        // =ascentLine + descentLine
            //fontBlobRoot.subscriptOffset     = faceInfo.subscriptOffset;   //WRONG! hcld: horizontal-clipping-descent
            //fontBlobRoot.subscriptSize       = faceInfo.subscriptSize;     //WRONG vdsc: vertical-descender
            //fontBlobRoot.superscriptOffset   = faceInfo.superscriptOffset; //WRONG! hcla: horizontal-clipping-ascent
            //fontBlobRoot.superscriptSize     = faceInfo.superscriptSize;   //WRONG! vasc: vertical-ascender

            fontBlobRoot.tabWidth            = faceInfo.tabWidth;
            fontBlobRoot.tabMultiple         = font.tabMultiple;
            fontBlobRoot.regularStyleSpacing = font.regularStyleSpacing;
            fontBlobRoot.regularStyleWeight  = font.regularStyleWeight;
            fontBlobRoot.boldStyleSpacing    = font.boldStyleSpacing;
            fontBlobRoot.boldStyleWeight     = font.boldStyleWeight;
            fontBlobRoot.italicsStyleSlant   = font.italicStyleSlant;            
            fontBlobRoot.atlasWidth          = font.atlasWidth;
            fontBlobRoot.atlasHeight         = font.atlasHeight;
            fontBlobRoot.materialPadding     = materialPadding;

            var glyphLookupTable = font.glyphLookupTable;
            var characterHashMapBuilder = builder.AllocateHashMap(ref fontBlobRoot.glyphs, glyphLookupTable.Count);
            foreach (var glyph in glyphLookupTable.Values)
            {
                characterHashMapBuilder.Add((int)glyph.index, new GlyphBlob {  glyphMetrics = glyph.metrics, glyphRect = glyph.glyphRect, glyphScale = glyph.scale });
            }            

            var result = builder.CreateBlobAssetReference<FontBlob>(Allocator.Persistent);
            builder.Dispose();
            fontBlobRoot = result.Value;
            return result;
        }        
    }
}