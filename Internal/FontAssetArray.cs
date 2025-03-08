using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace TextMeshDOTS
{
    public struct FontAssetArray
    {
        public FixedList4096Bytes<FontAssetRef> fontAssetRefs;
        public readonly int Length => fontAssetRefs.Length;
        public readonly FontAssetRef this[int index] => fontAssetRefs[index];
        public void Initialize(BlobAssetReference<FontBlob> singleFont)
        {
            var fontAssetRef = singleFont.Value.fontAssetRef;
            //Debug.Log($"Initialize {fontAssetRef.familyHash} italic? {fontAssetRef.isItalic} width? {fontAssetRef.width} weight? {fontAssetRef.weight}");
            fontAssetRefs.Clear();
            fontAssetRefs.Add(fontAssetRef);
        }
        public void Initialize(Entity rootFontMaterialEntity,
                               DynamicBuffer<AdditionalFontMaterialEntity> additionalFontMaterialEntities,
                               ref ComponentLookup<FontBlobReference> fontBlobReferenceLookup)
        {
            Initialize(fontBlobReferenceLookup[rootFontMaterialEntity].value);
            for (int i = 0, ii= additionalFontMaterialEntities.Length; i <ii ; i++)
            {
                if (fontBlobReferenceLookup.TryGetComponent(additionalFontMaterialEntities[i].entity, out var blobRef))
                {
                    var fontAssetRef = blobRef.value.Value.fontAssetRef;
                    //Debug.Log($"Initialize {fontAssetRef.familyHash} italic? {fontAssetRef.isItalic} width? {fontAssetRef.width} weight? {fontAssetRef.weight}");
                    fontAssetRefs.Add(fontAssetRef);
                }
            }
        }
        /// <summary> Find font entity requested by combination of font family and style </summary>
        public int GetFontIndex(FontAssetRef desiredFontAssetRef)
        {
            //Debug.Log($"Search for: {desiredFontAssetRef}");
            for (int i = 0, lenght = fontAssetRefs.Length; i < lenght; i++)
            {
                //Debug.Log($"candidate: {fontAssetRefs[i].ToString()}");
                if (fontAssetRefs[i] == desiredFontAssetRef)                  
                    return i;                
            }

            //fall back to family in case we end up here
            for (int i = 0, lenght = fontAssetRefs.Length; i < lenght; i++)
            {
                //Debug.Log($"fallback candidate: {fontAssetRefs[i].ToString()}");
                if (fontAssetRefs[i].familyHash == desiredFontAssetRef.familyHash)
                    return i;
            }
            //Debug.Log($"Requested font not found");
            return -1;
        }
    }
}

