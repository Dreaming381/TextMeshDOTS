using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

// If you are providing a frontend, listen up!
// Your objective is to query for all entities with two components:
// RenderGlyphOld and TextRenderControl.
// Write to them in PresentationSystemGroup before KinemationRenderUpdateSuperSystem
// which is inside UpdatePresentationSystemGroup.
// If you change anything on any of the RenderGlyphs, or resize the buffer,
// you must add the Dirty flag to TextRenderControl. The other flags are optional
// and are simply there so you can defer some calculations to the GPU (they're effectively free).
// For baking, you must bake whatever data you require to populate the RenderGlyphs.
// In the namespace Latios.Kinemation.TextBackend.Authoring, call the IBaker extension
// method BakeTextBackendMeshAndMaterial() to set up the rendering side. This will add
// the required RenderGlyphOld and TextRenderControl components as well as internal rendering
// components.
//
// How it works:
// In the Kinemation Resources directory, there is a special Mesh baked which contains dummy
// vertex attributes, and has multiple submeshes. Each submesh contains the triangle vertex indices
// for various glyph counts. Inside KinemationRenderUpdateSuperSystem, for any TextRenderControl
// with the dirty flag, Kinemation will set the appropriate submesh on the entity's MaterialMeshInfo.
// It will also recalculate the RenderBounds (local-space bounds).
// In the culling loop, Kinemation will update any visible text glyphs to an upload GraphicsBuffer.
// The uploaded glyphs need to be transferred to a persistent GraphicsBuffer which is done via
// a ComputeShader. Because this ComputeShader is primarily a memory-transfer operation, most of the
// time the GPU is doing NOPs waiting on memory. We try to replace those NOPs with useful calculations
// that the CPU would otherwise have to do, such as color space conversion. The culling loop will also
// set the TextShaderIndex material property.
// Lastly, the Latios Text Shader Graph node will parse the glyphs from the persistent buffer based on
// the vertex ID. If the vertex ID does not map to any glyph (because the string is short), the node
// will instead return a vertex that the GPU will discard based on the same mechanism VFX Graph uses.
// Otherwise, the returned vertex will contain all the information the vertex needs to provide to
// TextMeshPro. The glyph stays compressed in its 96 byte form on the GPU and is decoded directly in
// the vertex shader.

namespace TextMeshDOTS.Rendering
{
    /// <summary>
    /// The glyphs to be rendered based on the processed CalliByte buffer.
    /// Copy this buffer to AnimatedRenderGlyph to apply animation to the data.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct RenderGlyph : IBufferElementData 
    {
        public float2 blPosition;   //0
        public float2 brPosition;   //8
        public float2 tlPosition;   //16
        public float2 trPosition;   //24

        public float2 blUVB;        //32
        public float2 brUVB;        //40
        public float2 tlUVB;        //48
        public float2 trUVB;        //56

        public half4 blColor;       //64
        public half4 brColor;       //72
        public half4 tlColor;       //80
        public half4 trColor;       //88

        // These should be normalized relative to the padded bounding box extents of [0, 1]
        // The uploader will patch these with the atlas coordinates using math.lerp()
        public float2 blUVA;        //96
        public float2 trUVA;        //104

        public uint arrayIndex;     //112  Converted to float in upload shader
        public uint glyphEntryId;   //116
        public float scale;         //120
        public uint reserved;       //124
                                    //128 bytes total size
    }

    /// <summary>
    /// When this buffer is present, it overrides the RenderGlyph buffer for rendering purposes.
    /// Copy the RenderGlyph buffer into this buffer and then modify the glyphs for animation purposes
    /// within AnimateGlyphsSuperSystem.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AnimatedRenderGlyph : IBufferElementData
    {
        public RenderGlyph glyph;
    }


    [MaterialProperty("_TextShaderIndex")]
    public struct TextShaderIndex : IComponentData
    {
        public uint firstGlyphIndex;
        public uint glyphCount;
    }

    internal struct GpuState : IComponentData, IEnableableComponent  // Enabled to request dispatch
    {
        internal enum State : byte
        {
            Uncommitted,
            Dynamic,
            DynamicPromoteToResident,
            Resident
        }
        internal State state;
    }

    [InternalBufferCapacity(0)]
    internal struct PreviousRenderGlyph : ICleanupBufferElementData
    {
        public RenderGlyph glyph;
    }

    internal struct ResidentRange : ICleanupComponentData
    {
        public uint start;
        public uint count;
    }

    internal partial struct NewEntitiesArrays : ICollectionComponent
    {
        public NativeArray<Entity> newGlyphEntities;
        public uint lastTouchedGlobalSystemVersion;

        public JobHandle TryDispose(JobHandle inputDeps) => inputDeps;
    }  
   

    /// <summary>
    /// An additional rendered text entity containing a different font and material.
    /// The additional entity shares the RenderGlyphOld buffer, and uses a mask to identify
    /// the glyphs to render.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AdditionalFontMaterialEntity : IBufferElementData
    {
        public Entity entity;
    }

    /// <summary>
    /// A per-glyph index into the font and material that should be used to render it.
    /// Index 0 is this entity. Index 1 is the first entity in AdditionalFontMaterialEntity buffer.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct FontMaterialSelectorForGlyph : IBufferElementData
    {
        public byte fontMaterialIndex;
    }
}

