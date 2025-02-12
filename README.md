# TextMeshDOTS

TextMeshDOTS is a standalone text package for DOTS, forked from [Latios Framework/Calligraphics](https://github.com/Dreaming381/Latios-Framework/tree/master/Calligraphics). 

Prior to version 0.9.0, TextMeshDOTS used static font atlas textures from TextCore FontAssets. 
As of version 0.9.0, generates all requiered font textures dynamically on the fly. This is made possible by utilizing 
the [Harfbuzz](https://harfbuzz.github.io/) library. Thanks to it,  TextMeshDOTS now works directly with native Truetype 
and Opentype fonts (file ending `*.ttf` and `*.otf`) and does not need Unity `Font` or `FontAsset`. The [HarfbuzzUnity](https://github.com/Dreaming381/HarfbuzzUnity) 
plugin for MacOS, Linux and Windows was made by Dreaming381. Furthermore, TextMeshDOTS is now also capabaple to render the newest 
version of colored emoji fonts natively without any additonal library dependencies such as freetype: 
[COLRv1 fonts](https://developer.chrome.com/blog/colrv1-fonts). It cannot (and may never) work with bitmap and svg version of 
these fonts (read linked blog for "Why?").

TextMeshDOTS renders world space text similar to TextMeshPro. It leverages the 
[Unity Entities](https://docs.unity3d.com/Packages/com.unity.entities@1.2/manual/index.html) 
package to generate the vertex data required for rendering, and uses native 
[Unity Entities Graphics](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.2/manual/index.html) 
for rendering. The HDRP and URP shader are wrapper around the TextMeshPro 4.0 SRP shader. TextMeshDOTS supports
almost all rich text tags of [TextMeshPro](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichText.html) 
and TextCore: \<allcaps\>, \<alpha\>, \<b\>, \<color\>, \<cspace\>, \<font\>, \<i>, \<lowercase\>, \<sub\>, 
\<sup\>, \<pos\>, \<voffset\>, \<size\>, \<space=000.00\>, \<mspace=xx.x\>, \<smallcaps\>, 
<scale=xx.x>, \<rotate\>. Other tags are recognized but might be ignored for layout and rendering purposes. 

# How to use

(1) Autoring workflow
  -	Generate backend mesh and materials: `Menue --> TextMeshDOTS --> Text BackendMesh`, `Menue --> TextMeshDOTS --> Generate materials`
    - this only needs to be done once in a given project. The generated assets are placed into `Resources` folder, and are expected there by the runtime

  -	Create a `SubScene`
  -	Add empty `GameObject`, and `TextRenderer` component on it
  - As for font usage, you need to drop all fonts you intend to use in the TextRenderer into the `Fonts` list. You got 2 options for doing so
    1. `System Fonts`: you want to use fonts that you know can be found on the target device
    2. you want to include the font files into your build
  - To use `System Fonts`, you can drop the `ttf` and `otf` files anywhere in your project. Unity cannot be stopped converting this 
    to a `font` asset, but this font asset is actually not needed and all information in it will be ignored.
  - To include the font files into your build, create under your `Assets` folder a subfolder called `StreamingAssets`. Drag and drop all 
    the `ttf` and `otf` files you intend to use there. You can organize fonts in further subfolders as you wish.
  - Please note, that pretty much any font such as "Arial" in Windows is actually multiple font files (e.g. one for `regular`,
    one for `bold`, one for `italic`, one for `bold italic`. There can be many more. You need all of them to enable TextmeshDOTS to 
    automatically select the right font when you apply FontStyles either via the buttons on the TextRenderer, or via richtext tags
    such as \<b\>, \<i> or \<font\> to explicitly select a font. TextMeshDOTS can simulate bold and italic when those variants are missing,
    but this should be the exception and not the default.    
  - Type in some text or rich text  
  -	You should now see the text    

(2) Runtime instantiation workflow
  -	Generate backend mesh and materials: `Menue --> TextMeshDOTS --> Text BackendMesh`, `Menue --> TextMeshDOTS --> Generate materials`
    - this only needs to be done once in a given project. The generated assets are placed into `Resources` folder, and are expected there by the runtime
  -	Enable & modify `TextMeshDOTS/RuntimeSpawner/RuntimeSingleFontTextRendererSpawner.cs` or `RuntimeMultiFontTextRendererSpawner.cs` 
    as needed to spawn any number of `TextRenderer` entities. Per default, auto creation of both systems is disabled.
  - You will notice, that you need to manually fill out a lot of information in the `FontRequest` struct for every font 
    you intend to use. This information can be extracted utilizing the `FontUtility` Scriptable Object.
    - Right click in a folder, then `Create --> TextMeshDOTS --> Font Utility`
    - Drag and drop the fonts from `StreamingAssets` or from anywhere else in your project 
      (in case you intend to use `System Fonts`) into the font field, and copy the information 
      over into your runtime spanwer. I know this is cumbersome, but I did not see it so far 
      as well invested time to automate runtime spawning via a baking workflow, primarily because I want to keep 
      only one unified path for triggering the runtime font loading/unloading system (via `FontBlobReference`)
  -	Hit play


# Known issues
-   None at this time


## Special Thanks To the original authors and contributors

-   Dreaming381 - not only has he created the amazing [Latios Framework](https://github.com/Dreaming381/Latios-Framework), 
   including the Calligraphics text module, but has also been of tremendous support in figuring out how to create 
   a standalone version of Calligraphics that uses Entity Graphics instead of the Kinemation rendering engine. 
   Furthermore, Dreaming381 made the harfbuzz library accessible as plugin across platforms via the [HarfbuzzUnity](https://github.com/Dreaming381/HarfbuzzUnity) 
plugin for MacOS, Linux and Windows
-   Sovogal – significant contributions to the Calligraphics module of Latios Framework (including the name)
