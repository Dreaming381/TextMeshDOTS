using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace TextMeshDOTS.Polybool
{    
    internal static class Utils
    {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static byte SetBit(byte value, int bitIndex, bool flag)
		{
			if (flag)
				return (byte) (value | (1 << bitIndex));   // set bit
			else
				return (byte) (value & ~(1 << bitIndex));  // clear bit
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool GetBit(byte value, int bitIndex)
		{
			return (value & (1 << bitIndex)) != 0;
		}
		///// <summary>
		///// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
		///// </summary>
		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		//      public static double SignedArea<T>(this T data, int start, int end) where T : INativeList<long2>
		//      {
		//          double area = default;
		//          for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
		//              area += (data[prev].x - data[i].x) * (data[i].y + data[prev].y);
		//          return area * 0.5;
		//      }
		/// <summary>
		/// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double SignedArea<T>(this T data, int start, int end) where T : INativeList<long2>
		{
			double area = default;
			for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
				area += (data.ElementAt(prev).x - data.ElementAt(i).x) * (data.ElementAt(i).y + data.ElementAt(prev).y);
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

        public static void WriteEventsToFile(string path, List<EventBool> events, List<Segment> segments)
        {
            if (events.Count == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = events.Count; i < end; i++)
            {
                var seg = segments[events[i].segmentID];
                writer.WriteLine($"{seg.start.x} {seg.start.y}");
                writer.WriteLine($"{seg.end.x} {seg.end.y}\n");
            }
            writer.Close();
        }
        public static void WriteSegmentsToFile(string path, List<Segment> segments)
        {
            if (segments.Count == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = segments.Count; i < end; i++)
            {
                var seg = segments[i];
                writer.WriteLine($"{seg.start.x} {seg.start.y} {seg.windingTopToBottom}");
                writer.WriteLine($"{seg.end.x} {seg.end.y}\n");
            }
            writer.Close();
        }
        public static void WriteAnnotatedSegmentsToFile(string path, List<Segment> segments)
        {
            if (segments.Count == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = segments.Count; i < end; i++)
            {
                var seg = segments[i];
				//writer.WriteLine($"{seg.p0_start.x} {seg.p0_start.y} above: {seg.above} {seg.windingTopToBottom} {seg.windingLeftToRight}");
				writer.WriteLine($"{seg.start.x} {seg.start.y} {seg.fillAbove} {seg.fillOtherAbove} {seg.fillBelow} {seg.fillOtherBelow}");
				writer.WriteLine($"{seg.end.x} {seg.end.y} \n");
            }
            writer.Close();
        }
		public static void WritePolygonToFile(string path, Polygon polygon)
		{
			var nodes = polygon.nodes;
			var startIDs = polygon.startIDs;
			if (nodes.Length == 0) return;
			StreamWriter writer = new StreamWriter(path, false);
			for (int k = 0, kk = startIDs.Length - 1; k < kk; k++)
			{
				var start = startIDs[k];
				var end = startIDs[k + 1];
				for (int i = start; i < end; i++)
				{
					var node = nodes[i];
					//writer.WriteLine($"{seg.p0_start.x} {seg.p0_start.y} above: {seg.above} {seg.windingTopToBottom} {seg.windingLeftToRight}");
					writer.WriteLine($"{node.x} {node.y}");
				}
				writer.WriteLine($"{nodes[start].x} {nodes[start].y}\n");
			}
			writer.Close();
		}
	}
}