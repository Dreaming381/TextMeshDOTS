using UnityEngine;
using TextMeshDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace TextMeshDOTS
{
    public struct FontAssetArray
    {
        public FixedList4096Bytes<FontAssetRef> fontAssetRefs;       
        public readonly int Length => fontAssetRefs.Length;
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
            for (int i = 0; i < additionalFontMaterialEntities.Length; i++)
            {
                if (fontBlobReferenceLookup.TryGetComponent(additionalFontMaterialEntities[i].entity, out var blobRef))
                {
                    var fontAssetRef = blobRef.value.Value.fontAssetRef;
                    //Debug.Log($"Initialize {fontAssetRef.familyHash} italic? {fontAssetRef.isItalic} width? {fontAssetRef.width} weight? {fontAssetRef.weight}");
                    fontAssetRefs.Add(fontAssetRef);
                }
            }
        }        
    }
}

