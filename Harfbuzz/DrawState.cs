using System.Runtime.InteropServices;
using Unity.Entities;

namespace HarfBuzz
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DrawState
    {
        public byte path_open; //read this as bool!

        public float path_start_x;
        public float path_start_y;

        public float current_x;
        public float current_y;

        /*< private >*/
        int reserved1;
        int reserved2;
        int reserved3;
        int reserved4;
        int reserved5;
        int reserved6;
        int reserved7;
    }
}
