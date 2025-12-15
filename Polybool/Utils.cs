using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace TextMeshDOTS.Polybool
{    
    internal static class Utils
    {
        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SignedArea<T>(this T data, int start, int end) where T : INativeList<double2>
        {
            double area = default;
            for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
                area += (data[prev].x - data[i].x) * (data[i].y + data[prev].y);
            return area * 0.5;
        }

        public static void Reverse<T>(this UnsafeList<T> nodes) where T : unmanaged
        {
            int i = 0, j = nodes.Length - 1;
            T temp;
            while (i < j)
            {
                temp = nodes[i];
                nodes[i] = nodes[j];
                nodes[j] = temp;
                i++;
                j--;
            }
        }


        public static void WriteEventsToFile(string path, NativeList<EventBool> events, NativeList<Segment> segments)
        {
            if (events.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = events.Length; i < end; i++)
            {
                var seg = segments[events[i].segmentID];
                writer.WriteLine($"{seg.p0_start.x} {seg.p0_start.y}");
                writer.WriteLine($"{seg.p1_end.x} {seg.p1_end.y}\n");
            }
            writer.Close();
        }
        public static void WriteSegmentsToFile(string path, NativeList<Segment> segments)
        {
            if (segments.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = segments.Length; i < end; i++)
            {
                var seg = segments[i];
                writer.WriteLine($"{seg.p0_start.x} {seg.p0_start.y} {seg.windingTopToBottom}");
                writer.WriteLine($"{seg.p1_end.x} {seg.p1_end.y}\n");
            }
            writer.Close();
        }
        public static void WriteAnnotatedSegmentsToFile(string path, NativeList<Segment> segments)
        {
            if (segments.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = segments.Length; i < end; i++)
            {
                var seg = segments[i];
                writer.WriteLine($"{seg.p0_start.x} {seg.p0_start.y} above: {seg.above} {seg.windingTopToBottom} {seg.windingLeftToRight}");
                writer.WriteLine($"{seg.p1_end.x} {seg.p1_end.y} below: {seg.below}\n");
            }
            writer.Close();
        }
    }
}