using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace TextMeshDOTS
{
    public struct FontAssetArray
    {
        public FixedList4096Bytes<FontAssetRef> fontAssetRefs;
       
        public int length => fontAssetRefs.Length;
        public void Initialize(BlobAssetReference<FontBlob> singleFont)
        {
            fontAssetRefs.Clear();
            fontAssetRefs.Add(singleFont.Value.fontAssetRef);
        }
        public void Initialize(Entity rootFontMaterialEntity,
                               DynamicBuffer<AdditionalFontMaterialEntity> additionalFontMaterialEntities,
                               ref ComponentLookup<FontBlobReference> fontBlobReferenceLookup)
        {
            Initialize(fontBlobReferenceLookup[rootFontMaterialEntity].fontBlob);
            for (int i = 0; i < additionalFontMaterialEntities.Length; i++)
            {
                if (fontBlobReferenceLookup.TryGetComponent(additionalFontMaterialEntities[i].entity, out var blobRef))
                    fontAssetRefs.Add(blobRef.fontBlob.Value.fontAssetRef);
            }
        }        
    }
}

