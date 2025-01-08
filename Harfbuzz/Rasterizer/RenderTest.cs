using TextMeshDOTS.HarfBuzz.SDF;
using TextMeshDOTS.HarfBuzz;
using Unity.Collections;
using UnityEngine;
using Font = TextMeshDOTS.HarfBuzz.Font;
using UnityEditor;
using Unity.Profiling;


public class RenderTest : MonoBehaviour
{
    static readonly ProfilerMarker marker = new ProfilerMarker("Rasterize");
    public UnityEngine.Font sourceFont;
    public string letter;
    public uint glyphID;
    public int atlasWidth = 64;
    public int atlasHeight = 64;

    SDFOrientation orientation;
    public DrawDelegates drawDelegates;
    DrawData drawData;
    PaintDelegates paintDelegates;
    PaintData paintData;
    Face face;
    Font font;

    void Start()
    {
        drawDelegates = new DrawDelegates(true);
        paintDelegates = new PaintDelegates(true);
        LoadFont(sourceFont, 50);

        //DrawTest(letter);
        //PaintPNGTest("😉");// 🥰 😉
        PaintTest(letter);
        //PaintTest(glyphID);
    }

    void Update()
    {
        
    }

    void DrawTest(string character)
    {
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.Alpha8, false);
        var textureData = texture2D.GetRawTextureData<ColorARGB>();
        for (int i = 0; i < textureData.Length; i++)
            textureData[i] = (ColorARGB)Color.black;

        var language = new Language("eng");
        var buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);
        //buffer.AddText("😉");
        buffer.AddText(character);
        font.Shape(buffer);
        var glyphInfos = buffer.GetGlyphInfosSpan();

        drawData = new DrawData(256, 16, Allocator.Persistent);
        font.DrawGlyph(glyphInfos[0].codepoint, drawDelegates, ref drawData);

        //SDFCommon.WriteGlyphOutlineToFile("Outline.txt", ref drawData);
        //var atlasRect = new GlyphRect { x = 0, y = 0, width = atlasWidth, height = atlasHeight };
        SDFCommon.CenterGlyphInGlyphRect(ref drawData, atlasWidth, atlasHeight, 0);
        // SDF.SDFGenerateSubDivision(orientation, ref drawData, textureData, atlasRect, atlasWidth, atlasHeight);

        marker.Begin();
        var solidColor = new SolidColor(new ColorARGB(0, 0, 0, 255));
        ScanlineRasterizer.Rasterize(ref drawData, textureData, solidColor, atlasWidth, atlasHeight);
        marker.End();

        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply();
        buffer.Dispose();
    }

    void PaintTest(uint glyphID)
    {
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.ARGB32, false);
        var textureData = texture2D.GetRawTextureData<ColorARGB>();
        for (int i = 0; i < textureData.Length; i++)
            textureData[i] = (ColorARGB)Color.white;

        paintData = new PaintData(drawDelegates, textureData, atlasWidth, atlasHeight, 256, 4, Allocator.Temp);
        font.PaintGlyph(glyphID, ref paintData, paintDelegates, 0, new ColorARGB(0, 0, 0, 255));

        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply();

        paintData.Dispose();
        //buffer.Dispose();
    }
    void PaintTest(string character)
    {
        var texture2D = new Texture2D(atlasWidth, atlasHeight, TextureFormat.ARGB32, false);
        var textureData = texture2D.GetRawTextureData<ColorARGB>();
        for (int i = 0; i < textureData.Length; i++)
            textureData[i] = (ColorARGB)Color.white;

        var language = new Language("eng");
        var buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);
        buffer.AddText(character);
        font.Shape(buffer);
        var glyphInfos = buffer.GetGlyphInfosSpan();

        paintData = new PaintData(drawDelegates, textureData, atlasWidth, atlasHeight, 256, 4, Allocator.Temp);
        font.PaintGlyph(glyphInfos[0].codepoint, ref paintData, paintDelegates, 0, new ColorARGB(0, 0, 0, 255)); 

        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material.mainTexture = texture2D;
        texture2D.Apply();

        paintData.Dispose();
        //buffer.Dispose();
    }
    void PaintPNGTest(string character)
    {
        var language = new Language("eng");
        var buffer = new Buffer(Direction.LeftToRight, Script.Latin, language);
        buffer.AddText(character);
        font.Shape(buffer);
        var glyphInfos = buffer.GetGlyphInfosSpan();

        paintData = new PaintData();
        font.PaintGlyph(glyphInfos[0].codepoint, ref paintData, paintDelegates, 0, new ColorARGB(0, 0, 0, 255));
        if (paintData.imageFormat == HB_PAINT_IMAGE_FORMAT.PNG)
        {
            var png = paintData.imageBlob.GetData();
            var texture2D = new Texture2D(2, 2);
            texture2D.LoadImage(png.ToArray());

            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.material.mainTexture = texture2D;
            texture2D.Apply();
            //paintData.Dispose();
        }
        buffer.Dispose();
    }

    private void OnDestroy()
    {
        drawDelegates.Dispose();
        drawData.Dispose();
        paintDelegates.Dispose();
    }
    void LoadFont(UnityEngine.Font unityFont, int samplingPointSize)
    {
        var filePath = AssetDatabase.GetAssetPath(unityFont);
        Blob blob  = new Blob(filePath);
        face = new Face(blob.ptr, 0);
        font = new Font(face.ptr);
        //m_font.SetScale(samplingPointSize, samplingPointSize);

        orientation = face.HasTrueTypeOutlines() ? SDFOrientation.TRUETYPE : SDFOrientation.POSTSCRIPT;
    }
}
