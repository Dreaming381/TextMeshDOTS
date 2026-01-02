using TextMeshDOTS;
using TextMeshDOTS.HarfBuzz;
using TextMeshDOTS.HarfBuzz.Bitmap;
using Unity.Collections;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using ClipType = TextMeshDOTS.Clipper2AoS.ClipType;
using FillRule = TextMeshDOTS.Clipper2AoS.FillRule;
using Font = TextMeshDOTS.HarfBuzz.Font;

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
        //SDFCommon.WriteGlyphOutlineToFile($"Outline of glyph {character}.txt", drawData);

        //simplify. Both clipper and polybol outputs the outer contour CCW, and the inner CW, which is postscript definition
        orientation = SDFOrientation.POSTSCRIPT; //clipper always outputs the outer contour CCW, and the inner CW, which is postscript definition
        PolygonOperation.RemoveSelfIntersections(ref drawData, ClipType.Union, FillRule.NonZero);
        //SDFCommon.WriteGlyphOutlineToFile($"Clipper2 {clipType} ({fillRule}) outline of glyph {character}.txt", drawData);

        marker.Begin();
        //BezierMath.SplitCuvesToLines(ref drawData, maxDeviation, out DrawData flatenedDrawData);
        //SDF.SDFGenerateSubDivision(orientation, ref drawData, ref textureData, ref atlasRect, padding, atlasWidth, atlasHeight,padding);        
        SDF_SPMD.SDFGenerateSubDivisionLineEdges(orientation, ref drawData, ref textureData, ref atlasRect, padding, atlasWidth, atlasHeight, padding);
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
        if (face.HasVarData)
        {
            font.VariationNamedInstance = 14; //13 OK, 14 buggy for "6"
            //DisplayVariationAxis();
        }


        //var scale = font.GetScale();        
        font.SetScale(samplingPointSize, samplingPointSize);
        //Debug.Log($"scale: {scale}");

        maxDeviation = BezierMath.GetMaxDeviation(font.GetScale().x);
        //Debug.Log($"Has COLR outlines? {face.HasCOLR()}");
        //Debug.Log($"Has Color Bitmap? {face.HasColorBitmap()}");

        orientation = face.HasTrueTypeOutlines() ? SDFOrientation.TRUETYPE : SDFOrientation.POSTSCRIPT;
        return true;
    }
    void DisplayVariationAxis()
    {
        var axisCount = (int)face.AxisCount;

        //fetch a list of all variation axis
        System.Span<AxisInfo> axisInfos = stackalloc AxisInfo[axisCount];
        face.GetAxisInfos(0, 0, ref axisInfos, out _);
        AxisInfo axisInfo;
        float coord;       

        //fetch a list of named variants                        
        //Debug.Log($"found {axisCount} variation axis for font {fontReference.fontFamily} {fontReference.fontSubFamily}, {face.NamedInstanceCount} named instances");
        System.Span<float> coords = stackalloc float[axisCount];
        for (int k = 0, kk = (int)face.NamedInstanceCount; k < kk; k++)
        {
            Debug.Log($"Named Instance: {k}");
            face.GetNamedInstanceDesignCoords(k, ref coords, out uint coordLength);
            for (int f = 0, ff = (int)coordLength; f < ff; f++)
            {
                //axisInfos and coords should be aligned in length and order
                axisInfo = axisInfos[f];
                coord = coords[f];
                Debug.Log($"Variation axis: {axisInfo.axisTag} {face.GetName(axisInfo.nameID, Language.English)}, value = {coord}");
            }
        }
    }
}