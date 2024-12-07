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
            var fontMaterialRefs = new NativeList<FontMaterialRef>(16, Allocator.Temp);
            var fontBlobReferences = new NativeList<FontBlobReference>(16, Allocator.Temp);

            for (int i = 0, length = authoring.fontAssets.Count; i < length; i++)
            {
                var mainFontAsset = authoring.fontAssets[i];
                if (mainFontAsset == null)
                    continue;
                mainFontAsset.ReadFontAssetDefinition();

                // add fontWeight
                var fontBlobRef = BakeFontAsset(mainFontAsset, TextFontWeight.Regular, false);
                fontBlobReferences.Add(new FontBlobReference { fontBlob = fontBlobRef, fontAsset = mainFontAsset });
                fontMaterialRefs.Add(new FontMaterialRef { value = mainFontAsset.material });

                //add fontWeight italic
                AddFontWeightPair(TextFontWeight.Regular, mainFontAsset, fontBlobReferences, fontMaterialRefs); //for regular FontWeight, this call will just add italic
                AddFontWeightPair(TextFontWeight.Bold, mainFontAsset, fontBlobReferences, fontMaterialRefs);
            }
            var fontReferencesBuffer = AddBuffer<FontBlobReference>(entity);
            fontReferencesBuffer.AddRange(fontBlobReferences.AsArray());
            var fontMaterialRefsBuffer = AddBuffer<FontMaterialRef>(entity);
            fontMaterialRefsBuffer.AddRange(fontMaterialRefs.AsArray());
        }

        void AddFontWeightPair(TextFontWeight textFontWeight, FontAsset mainFontAsset, NativeList<FontBlobReference> fontBlobReferences, NativeList<FontMaterialRef> fontMaterialRefs)
        {
            var fontWeightPair = mainFontAsset.fontWeightTable[TextCoreExtensions.GetTextFontWeightIndex(textFontWeight)];
            //add fontWeight 
            if (fontWeightPair.regularTypeface != null)
            {
                var fontBlobRef = BakeFontAsset(fontWeightPair.regularTypeface, textFontWeight, false);
                fontBlobReferences.Add(new FontBlobReference { fontBlob = fontBlobRef, fontAsset = fontWeightPair.regularTypeface });
                fontMaterialRefs.Add(new FontMaterialRef { value = fontWeightPair.regularTypeface.material });
            }

            //add fontWeight italic
            if (fontWeightPair.italicTypeface != null)
            {
                var fontBlobRef = BakeFontAsset(fontWeightPair.italicTypeface, textFontWeight, true);
                fontBlobReferences.Add(new FontBlobReference { fontBlob = fontBlobRef, fontAsset = fontWeightPair.italicTypeface });
                fontMaterialRefs.Add(new FontMaterialRef { value = fontWeightPair.italicTypeface.material });
            }
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

