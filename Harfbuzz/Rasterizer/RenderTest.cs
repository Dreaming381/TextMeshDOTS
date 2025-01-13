using TextMeshDOTS.HarfBuzz.SDF;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;
using UnityEngine;
using Font = TextMeshDOTS.HarfBuzz.Font;
using UnityEditor;
using Unity.Profiling;
using TextMeshDOTS;
using UnityEngine.TextCore;

public class RenderTest : MonoBehaviour
{
    static readonly ProfilerMarker marker = new ProfilerMarker("Rasterize");
    public UnityEngine.Font sourceFont;
    public string letter;
    public uint glyphID;
    public int atlasWidth = 64;
    public int atlasHeight = 64;
    public bool renderGlyphID;

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
        drawFunctions = new DrawDelegates(true);
        paintFunctions = new PaintDelegates(true);
        LoadFont(sourceFont, 512);

        //DrawTest(letter);
        //PaintPNGTest("😉");
        PaintTest(letter, glyphID); //😉🥰💀✌️🌴🐢🐐🍄⚽🍻👑📸😬👀🚨🏡🕊️🏆😻🌟🧿🍀🎨🍜
    }

    void Update()
    {
        
    }

    void DrawTest(string character)
    {
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.Alpha8, false);
        var textureData = texture2D.GetRawTextureData<byte>();
        for (int i = 0; i < textureData.Length; i++)
            textureData[i] = 0;

        var language = new Language("eng");
        var buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);
        //buffer.AddText("😉");
        buffer.AddText(character);
        font.Shape(buffer);
        var glyphInfos = buffer.GetGlyphInfosSpan();

        drawData = new DrawData(256, 16, Allocator.Persistent);
        font.DrawGlyph(glyphInfos[0].codepoint, drawFunctions, ref drawData);

        //SDFCommon.WriteGlyphOutlineToFile("Outline.txt", ref drawData, true);
        var glyphRect = new GlyphRect(64,64, (int)drawData.glyphRect.width+10, (int)drawData.glyphRect.height+10);
        SDF.SDFGenerateSubDivision(orientation, ref drawData, textureData, glyphRect, atlasWidth, atlasHeight);

        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply();
        buffer.Dispose();
    }   
    void PaintTest(string character, uint glyphID)
    {
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.ARGB32, false);
        var textureData = texture2D.GetRawTextureData<ColorARGB>();
        for (int i = 0; i < textureData.Length; i++)
            textureData[i] = (ColorARGB)Color.white;

        if (!renderGlyphID)
        {
            var language = new Language("eng");
            var buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);
            buffer.AddText(character);
            font.Shape(buffer);
            var glyphInfos = buffer.GetGlyphInfosSpan();
            glyphID = glyphInfos[0].codepoint;
        }

        paintData = new PaintData(drawFunctions, 256, 4, Allocator.Temp);
        font.GetGlyphExtends(glyphID, out GlyphExtents glyphExtents);
        //Debug.Log($"glyphExtents: {glyphExtents}");
        marker.Begin();
        font.PaintGlyph(glyphID, ref paintData, paintFunctions, 0, new ColorARGB(0, 0, 0, 255));
        marker.End();

        if (paintData.imageData.Length > 0)//render PNG and SVG
        {
            if (paintData.imageFormat == HB_PAINT_IMAGE_FORMAT.PNG)
            {
                var png = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                png.LoadImage(paintData.imageData.ToArray());
                var sourceTexture = png.GetRawTextureData<ColorARGB>();                
                PaintUtils.BlitRawTexture(sourceTexture, paintData.imageWidth, paintData.imageHeight, textureData, atlasWidth, atlasHeight, 0, 0);
            }
            if (paintData.imageFormat == HB_PAINT_IMAGE_FORMAT.SVG)
            {
                //could use com.unity.vectorgraphics if it would not be a class (designed to parse, tesselate and render svg)
            }
        }
        else if (paintData.textureData.Length > 0) // render COLR, sbix, CBDT
        {
            var clipRect = paintData.clipRect;
            PaintUtils.BlitRawTexture(paintData.textureData, (int)clipRect.width, (int)clipRect.height, textureData, atlasWidth, atlasHeight, 0, 0);
        }        

        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply(false);

        //buffer.Dispose();
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
    void LoadFont(UnityEngine.Font unityFont, int samplingPointSize)
    {
        var filePath = AssetDatabase.GetAssetPath(unityFont);
        blob  = new Blob(filePath);
        face = new Face(blob.ptr, 0);
        font = new Font(face.ptr);
        font.SetScale(samplingPointSize, samplingPointSize);
        Debug.Log($"Has COLR outlines? {face.HasCOLR()}");
        Debug.Log($"Has Color Bitmap? {face.HasColorBitmap()}");

        orientation = face.HasTrueTypeOutlines() ? SDFOrientation.TRUETYPE : SDFOrientation.POSTSCRIPT;
    }
}
