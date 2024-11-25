using UnityEngine;

namespace HarfBuzz
{

    public delegate void ReleaseDelegate();
    internal static unsafe class DelegateProxies
    {
        public static void Test()
        {
            Debug.Log($"harfbuzz blob called this delegate upon destroying blob");
        }
    }
}
