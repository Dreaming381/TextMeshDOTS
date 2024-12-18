using TextMeshDOTS.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.TextCore.Text;
using HarfBuzz;
using Font = HarfBuzz.Font;

namespace TextMeshDOTS.Authoring
{
    public static class FontBlobber
    {
        public static BlobAssetReference<FontBlob> BakeFontBlob(FontItem fontItem, FontAssetRef fontAssetRef, string familyName, string subFamily)
        {
            var          builder             = new BlobBuilder(Allocator.Temp);
            ref FontBlob fontBlobRoot        = ref builder.ConstructRoot<FontBlob>();

            //create references to load font data at runtime
            fontBlobRoot.familyName = familyName;
            fontBlobRoot.styleName = subFamily;
            fontBlobRoot.fontAssetRef = fontAssetRef;

            var result = builder.CreateBlobAssetReference<FontBlob>(Allocator.Persistent);
            builder.Dispose();
            fontBlobRoot = result.Value;
            return result;
        } 
    }
}