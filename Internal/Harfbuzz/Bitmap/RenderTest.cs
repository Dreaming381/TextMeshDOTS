using TextMeshDOTS.HarfBuzz.Bitmap;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;
using UnityEngine;
using Font = TextMeshDOTS.HarfBuzz.Font;
using UnityEditor;
using Unity.Profiling;
using TextMeshDOTS;


internal class RenderTest : MonoBehaviour
{
    static readonly ProfilerMarker marker = new ProfilerMarker("Rasterize");
    public Object sourceFont;
    [SerializeField] private string fontAssetPath;
    public string letter;
    public uint glyphID;
    public int atlasWidth = 1024;
    public int atlasHeight = 1024;
    public int samplingPointSize = 256;
    public bool renderGlyphID;
    

    float maxDeviation;
    SDFOrientation orientation;
    public DrawDelegates drawFunctions;
    DrawData drawData;
    PaintDelegates paintFunctions;
    PaintData paintData;
    Blob blob;
    Face face;
    Font font;

    void Start()
    {
#if UNITY_EDITOR
        fontAssetPath = AssetDatabase.GetAssetPath(sourceFont);
#endif
        if (fontAssetPath == null)
            return;

        drawFunctions = new DrawDelegates(true);
        paintFunctions = new PaintDelegates(true);
        if (!LoadFont(fontAssetPath, samplingPointSize))
            return;

        DrawTest(letter, glyphID);
        //PaintTest(letter, glyphID); //🌁😉🥰💀✌️🌴🐢🐐🍄⚽🍻👑📸😬👀🚨🏡🕊️🏆😻🌟🧿🍀🎨🍜
    }

    void Update()
    {
        
    }

    void DrawTest(string character, uint glyphID)
    {
        var padding = samplingPointSize / 6;
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.Alpha8, false);
        var textureData = texture2D.GetRawTextureData<byte>();
        for (int i = 0; i < textureData.Length; i++)
            textureData[i] = 0;

        Buffer buffer = default;
        if (!renderGlyphID)
        {
            var language = Language.English;
            buffer = new Buffer(Direction.LTR, Script.LATIN, language);
            //buffer.AddText("😉");
            buffer.AddText(character);
            font.Shape(buffer);
            var glyphInfos = buffer.GetGlyphInfosSpan();
            glyphID = glyphInfos[0].codepoint;
        }

        drawData = new DrawData(256, 16, maxDeviation, Allocator.Persistent);
        font.DrawGlyph(glyphID, drawFunctions, ref drawData);
                
        font.GetGlyphExtents(glyphID, out GlyphExtents glyphExtents);
        var atlasRect = glyphExtents.GetPaddedAtlasRect(24, 24, padding);

        //SDFCommon.WriteGlyphOutlineToFile("Outline.txt", ref drawData, true);
        BezierMath.SplitCuvesToLines(ref drawData, maxDeviation, out DrawData flatenedDrawData);
        //SDF.SDFGenerateSubDivisionLineEdges(orientation, ref drawData, textureData, atlasRect, padding, atlasWidth, atlasHeight,padding);
        marker.Begin();
        //for(int i = 0; i<10; i++)
            SDF_SPMD.SDFGenerateSubDivisionLineEdges_Overlap(orientation, ref drawData, ref textureData, ref atlasRect, padding, atlasWidth, atlasHeight, padding);
        marker.End();

        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply();
        if (!renderGlyphID)
            buffer.Dispose();
    }   
    void PaintTest(string character, uint glyphID)
    {
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.ARGB32, false);
        var textureData = texture2D.GetRawTextureData<ColorARGB>();
        Blending.SetBlack(textureData);

        Buffer buffer = default;
        BBox clipRect;
        if (!renderGlyphID)
        {
            var language = Language.English;
            buffer = new Buffer(Direction.LTR, Script.LATIN, language);
            buffer.AddText(character);
            font.Shape(buffer);
            var glyphInfos = buffer.GetGlyphInfosSpan();
            var glyphPositions = buffer.GetGlyphPositionsSpan();
            glyphID = glyphInfos[0].codepoint;
            //Debug.Log($"glyphID {glyphID} {glyphPositions[0]}");
        }

        paintData = new PaintData(drawFunctions, 256, 4, maxDeviation, Allocator.Temp);
        font.GetGlyphExtents(glyphID, out GlyphExtents glyphExtents);
        paintData.clipRect = glyphExtents.ClipRect;
        paintData.clipRect.Expand(1);//prevents rendering artifacts that occur for outlines that strech from minX to maxX of clipRect, reason unknown
        paintData.paintSurface = new NativeArray<ColorARGB>(paintData.clipRect.intWidth * paintData.clipRect.intHeight, Allocator.Temp);
        //Debug.Log($"clipBox: {paintData.clipRect}");

        marker.Begin();
        font.PaintGlyph(glyphID, ref paintData, paintFunctions, 0, new ColorARGB(255, 0, 0, 0));
        marker.End();

        if (paintData.imageData.Length > 0)//render PNG and SVG
        {
            if (paintData.imageFormat == PaintImageFormat.PNG)
            {
                var png = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                png.LoadImage(paintData.imageData.ToArray());
                var sourceTexture = png.GetRawTextureData<ColorARGB>();
                PaintUtils.BlitRawTexture(sourceTexture, paintData.imageWidth, paintData.imageHeight, textureData, atlasWidth, atlasHeight, 0, 0);
            }
            if (paintData.imageFormat == PaintImageFormat.SVG)
            {
                //could use com.unity.vectorgraphics (designed to parse, tesselate and render svg) if it would not be a class 
            }
        }
        else if (paintData.paintSurface.Length > 0) // content from COLR, or raw BGRA data from sbix, CBDT
        {
            clipRect = paintData.clipRect;
            PaintUtils.BlitRawTexture(paintData.paintSurface, clipRect.intWidth, clipRect.intHeight, textureData, atlasWidth, atlasHeight, 0, 0);
        }

       
        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply(false);
        if (!renderGlyphID)
            buffer.Dispose();
    }

    private void OnDestroy()
    {
        drawFunctions.Dispose();
        drawData.Dispose();
        paintFunctions.Dispose();
        font.Dispose();
        face.Dispose();
        blob.Dispose();
    }
    bool LoadFont(string fontAssetPath, int samplingPointSize)
    {
        if (!TextHelper.IsValidFont(fontAssetPath))
        { 
            Debug.LogWarning("Ensure you only have files ending with 'ttf' or 'otf' (case insensitiv) in font list");
            return false;
        }

        blob = new Blob(fontAssetPath);
        face = new Face(blob, 0);
        font = new Font(face);

        //var scale = font.GetScale();        
        font.SetScale(samplingPointSize, samplingPointSize);
        //Debug.Log($"scale: {scale}");

        maxDeviation = BezierMath.GetMaxDeviation(font.GetScale().x);
        //Debug.Log($"Has COLR outlines? {face.HasCOLR()}");
        //Debug.Log($"Has Color Bitmap? {face.HasColorBitmap()}");

        orientation = face.HasTrueTypeOutlines() ? SDFOrientation.TRUETYPE : SDFOrientation.POSTSCRIPT;
        return true;
    }
}