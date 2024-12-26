
using System;

namespace HarfBuzz.SDF
{
    public struct PaintData : IDisposable
    {
        public HB_PAINT_IMAGE_FORMAT imageFormat;
        public Blob imageBlob;
        public void Dispose()
        {
            imageBlob.Dispose();
        }
    }    
}