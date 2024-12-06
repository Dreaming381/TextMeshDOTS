using HarfBuzz;
using System.Collections.Generic;
using TextMeshDOTS.Rendering.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TextCore.Text;


namespace TextMeshDOTS.Authoring
{
    public class FontBlobAuthoring : MonoBehaviour
    {
        public List<FontAsset> fontAssets;
    }
    class FontBlobAuthoringBaker : Baker<FontBlobAuthoring>
    {
        public override void Bake(FontBlobAuthoring authoring)
        {
            if (authoring.fontAssets == null || authoring.fontAssets.Count == 0 || authoring.fontAssets[0] == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);
            var mesh = Resources.Load<Mesh>(TextBackendBakingUtility.kTextBackendMeshResource);

            AddComponent(entity, new BackEndMesh { value = mesh });
            var fontMaterialRefs = new NativeList<FontMaterialRef>(authoring.fontAssets.Count, Allocator.Temp);

            var fontReferences = AddBuffer<FontBlobReference>(entity);

            foreach (var fontAsset in authoring.fontAssets)
            {
                if (fontAsset == null)
                    continue;
                fontAsset.ReadFontAssetDefinition();
                var fontBlobRef = BakeFontAsset(fontAsset, TextFontWeight.Regular, false);
                fontReferences.Add(new FontBlobReference { fontBlob = fontBlobRef, fontAsset = fontAsset });
                fontMaterialRefs.Add(new FontMaterialRef { value = fontAsset.material });
            }
            var fontMaterialRefsBuffer = AddBuffer<FontMaterialRef>(entity);
            fontMaterialRefsBuffer.AddRange(fontMaterialRefs.AsArray());
        }
        BlobAssetReference<FontBlob> BakeFontAsset(FontAsset fontAsset, TextFontWeight textFontWeight, bool isItalic)
        {
            var customHash = new Unity.Entities.Hash128((uint)fontAsset.GetHashCode(), 0, 0, 0);
            if (!TryGetBlobAssetReference(customHash, out BlobAssetReference<FontBlob> blobReference))
            {
                blobReference = FontBlobber.BakeFontBlob(fontAsset, textFontWeight, isItalic);

                // Register the Blob Asset to the Baker for de-duplication and reverting.
                AddBlobAssetWithCustomHash<FontBlob>(ref blobReference, customHash);
            }
            return blobReference;
        }
    }
}

