using System;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TextCore;
using static TextMeshDOTS.HarfBuzz.Bitmap.SDF_SPMD;

namespace TextMeshDOTS.HarfBuzz.Bitmap
{   
    internal static class SDFCommon
    {
        public static void ClearArray<T>(this NativeArray<T> array) where T : unmanaged
        {
            
            unsafe
            {
                UnsafeUtility.MemClear(array.GetUnsafePtr(), (long)array.Length * sizeof(T));
            }
        }
        public readonly static bool USE_SQUARED_DISTANCES = true;
        // SPREAD represents the permitted distance of a given pixel to an edge in bits.
        // So 8 bit means distance can be from -127 (outside) to +128 (inside).
        // We need 8 bit when storing distances in an 8 bit alpha channel. 
        // When glyphs have a lot of "inside" area (often found in BLACK font weigth), and sampling them at larger sampling
        // point sizes (e.g. 128, or 256), this will led to "holes" due to this line of code in ValidateAndSaveDistance():
        // ignore if the distance is greater than spread;
        // if (dist.distance > sp_sq) return false;
        // Could possibly also clamp the distance here, but this would not look much prettier due
        // due to clipping. Better solution is to increase SPREAD to e.g. 16. When converting to 8 bit alpha, we add SPREAD
        // to give distances from 0..2*SPREAD, and multiply by (256/(2*SPREAD ) via this line of code in the final pass:
        // var scaleTo8Bit = 256 / (spread * 2);
        public const int DEFAULT_SPREAD = 8; // SPREAD and Atlas padding are related, but do not set SPREAD too small 
        public const int MIN_SPREAD = 2;
        public const int MAX_SPREAD = 32;
        public const int MAX_NEWTON_STEPS = 4;
        public const int MAX_NEWTON_DIVISIONS = 4;
        public const int FT_TRIG_SAFE_MSB = 29;
        public const int OUTSIDE_SIGN = -1;

        public static void FinalPass(
                    NativeArray<float> distances,
                    NativeArray<float> crosses,
                    NativeArray<int> signs,
                    int spread, GlyphRect atlastRect, int atlasWidth, int atlasHeight)
        {
            // final pass
            var atlasX = atlastRect.x;
            var atlasY = atlastRect.y;
            var atlasRectWidth = atlastRect.width;
            var atlasRectHeight = atlastRect.height;
            for (int row = 0; row < atlasRectHeight; row++)
            {
                /* We assume the starting pixel of each row is outside. */
                int current_sign = OUTSIDE_SIGN;

                for (int column = 0; column < atlasRectWidth; column++)
                {
                    var sourceIndex = atlasRectWidth * row + column;

                    var distance = distances[sourceIndex];
                    var cross = crosses[sourceIndex];
                    var sign = signs[sourceIndex];
                    if (sign == 0)
                        distance = spread;
                    else
                        current_sign = sign;

                    /* clamp the values */
                    distance = math.select(distance, spread, distance > spread);

                    // determine if distance is inside(+) or outside(-)
                    distance *= current_sign;

                    distances[sourceIndex] = distance;
                    crosses[sourceIndex] = cross;
                    signs[sourceIndex] = sign;
                }
            }
        }
        public static void FinalPassFlipSign(
            NativeArray<float> distances,
            NativeArray<float> crosses,
            NativeArray<int> signs,
            int spread, GlyphRect atlastRect, int atlasWidth, int atlasHeight)
        {
            // final pass
            var atlasX = atlastRect.x;
            var atlasY = atlastRect.y;
            var atlasRectWidth = atlastRect.width;
            var atlasRectHeight = atlastRect.height;
            for (int row = 0; row < atlasRectHeight; row++)
            {
                /* We assume the starting pixel of each row is outside. */
                int current_sign = OUTSIDE_SIGN;

                for (int column = 0; column < atlasRectWidth; column++)
                {
                    var sourceIndex = atlasRectWidth * row + column;

                    var distance = distances[sourceIndex];
                    var cross = crosses[sourceIndex];
                    var sign = signs[sourceIndex];
                    if (sign == 0)
                        distance = spread;
                    else
                        current_sign = sign;

                    /* clamp the values */
                    distance = math.select(distance, spread, distance > spread);

                    // determine if distance is inside(+) or outside(-)
                    distance *= -current_sign;

                    distances[sourceIndex] = distance;
                    crosses[sourceIndex] = cross;
                    signs[sourceIndex] = sign;
                }
            }
        }
        public static void GetAlphaTexture(
            NativeArray<float> distances,
            NativeArray<byte> buffer, 
            int spread, GlyphRect atlastRect, int atlasWidth, int atlasHeight)
        {
            var atlasX = atlastRect.x;
            var atlasY = atlastRect.y;
            var atlasRectWidth = atlastRect.width;
            var atlasRectHeight = atlastRect.height;
            var scaleTo8Bit = 256 / (spread * 2);
            //var scaleTo16Bit = 65536 / (spread * 2);
            for (int row = 0; row < atlasRectHeight; row++)
            {
                for (int column = 0; column < atlasRectWidth; column++)
                {
                    var sourceIndex = atlasRectWidth * row + column;
                    var targetIndex = (atlasWidth * (row + atlasY)) + (column + atlasX);
                 
                    // convert to byte range of alpha8 texture
                    var result = (distances[sourceIndex] + spread) * scaleTo8Bit;
                    buffer[targetIndex] = (byte)result;
                }
            }
        }
        public static void MergeSDF(
            NativeArray<float> destinationDistances,
            NativeArray<float> destinationCrosses,
            NativeArray<int> destinationSigns,
            NativeArray<float> sourceDistances,
            NativeArray<float> sourceCrosses,
            NativeArray<int> sourceSigns,
            float sp_sq, PolyOrientation sourceContourOrientation)
        {
            //Debug.Log($"{sourceContourOrientation}");
            if (sourceContourOrientation == PolyOrientation.CW)
            {
                
                for (int i = 0, ii = sourceDistances.Length; i < ii; i++)
                {
                    var condition = sourceDistances[i] > destinationDistances[i];
                    destinationDistances[i] = math.select(destinationDistances[i], sourceDistances[i], condition);
                    destinationCrosses[i] = math.select(destinationCrosses[i], sourceCrosses[i], condition);
                    destinationSigns[i] = math.select(destinationSigns[i], sourceSigns[i], condition);
                }
            }
            else
            {
                for (int i = 0, ii = sourceDistances.Length; i < ii; i++)
                {
                    var condition = sourceDistances[i] < destinationDistances[i];
                    var dist1 = math.select(destinationDistances[i], sourceDistances[i], condition);
                    var cross1 = math.select(destinationCrosses[i], sourceCrosses[i], condition);
                    var sign1 = math.select(destinationSigns[i], sourceSigns[i], condition);

                    var condition2 = sourceSigns[i] == 0;
                    destinationDistances[i] = math.select(dist1, destinationDistances[i], condition2);
                    destinationCrosses[i] = math.select(cross1, destinationCrosses[i], condition2);
                    destinationSigns[i] = math.select(sign1, destinationSigns[i], condition2);
                }
            }
        }
        public static void GetTarget_DistanceCrossSign(
            NativeArray<float> distances,
            NativeArray<float> crosses,
            NativeArray<int> signs,
            int index, out float4 targetDistance, out float4 targetCross, out int4 targetSign)
        {
            targetDistance = new float4(distances[index], distances[index + 1], distances[index + 2], distances[index + 3]);
            targetCross = new float4(crosses[index], crosses[index + 1], crosses[index + 2], crosses[index + 3]);
            targetSign = new int4(signs[index], signs[index + 1], signs[index + 2], signs[index + 3]);
        }
        public static void GetTarget_DistanceCrossSign(
            NativeArray<float> distances,
            NativeArray<float> crosses,
            NativeArray<int> signs,
            int index, out float targetDistance, out float targetCross, out int targetSign)
        {
            targetDistance = distances[index];
            targetCross = crosses[index];
            targetSign = signs[index];
        }
        public static void SetTarget_DistanceCrossSign(
            NativeArray<float> distances,
            NativeArray<float> crosses,
            NativeArray<int> signs,
            int index, ref float4 validDistance, ref float4 validCross, ref int4 validSign)
        {
            distances[index] = validDistance[0];
            distances[index + 1] = validDistance[1];
            distances[index + 2] = validDistance[2];
            distances[index + 3] = validDistance[3];

            crosses[index] = validCross[0];
            crosses[index + 1] = validCross[1];
            crosses[index + 2] = validCross[2];
            crosses[index + 3] = validCross[3];

            signs[index] = validSign[0];
            signs[index + 1] = validSign[1];
            signs[index + 2] = validSign[2];
            signs[index + 3] = validSign[3];
        }
        public static void SetTarget_DistanceCrossSign(
            NativeArray<float> distances,
            NativeArray<float> crosses,
            NativeArray<int> signs,
            int index, ref float validDistance, ref float validCross, ref int validSign)
        {
            distances[index] = validDistance;
            crosses[index] = validCross;
            signs[index] = validSign;
        }
        public static void GetValid_DistanceCrossSign(
                    ref float4 distance,
                    ref float4 cross,
                    ref int4 sign,
                    ref float4 targetDistance,
                    ref float4 targetCross,
                    ref int4 targetSign,
                    float sp_sq,
                    out float4 validDistance,
                    out float4 validCross,
                    out int4 validSign
                    )
        {
            var condition = math.abs(cross) > math.abs(targetCross);
            var resolverCornerDistance = math.select(targetDistance, distance, condition);
            var resolverCornerCross = math.select(targetCross, cross, condition);
            var resolverCornerSign = math.select(targetSign, sign, condition);

            condition = targetDistance > distance;
            var greaterDistance = math.select(targetDistance, distance, condition);
            var greaterCross = math.select(targetCross, cross, condition);
            var greaterSign = math.select(targetSign, sign, condition);

            condition = BezierMath.EqualsForLargeValues(targetDistance, distance);
            var equalDistance = math.select(greaterDistance, resolverCornerDistance, condition);
            var equalCross = math.select(greaterCross, resolverCornerCross, condition);
            var equalSign = math.select(greaterSign, resolverCornerSign, condition);

            condition = targetSign == 0;
            var pixelNotSetDistance = math.select(equalDistance, distance, condition);
            var pixelNotSetCross = math.select(equalCross, cross, condition);
            var pixelNotSetSign = math.select(equalSign, sign, condition);

            // ignore if the distance is greater than spread to avoid artifacts caused by wrong sign
            condition = distance > sp_sq;
            validDistance = math.select(pixelNotSetDistance, targetDistance, condition);
            validCross = math.select(pixelNotSetCross, targetCross, condition);
            validSign = math.select(pixelNotSetSign, targetSign, condition);
        }
        public static void GetValid_DistanceCrossSign(
            ref float distance,
            ref float cross,
            ref int sign,
            ref float targetDistance,
            ref float targetCross,
            ref int targetSign,
            float sp_sq,
            out float validDistance,
            out float validCross,
            out int validSign
            )
        {
            var condition = math.abs(cross) > math.abs(targetCross);
            var resolverCornerDistance = math.select(targetDistance, distance, condition);
            var resolverCornerCross = math.select(targetCross, cross, condition);
            var resolverCornerSign = math.select(targetSign, sign, condition);

            condition = targetDistance > distance;
            var greaterDistance = math.select(targetDistance, distance, condition);
            var greaterCross = math.select(targetCross, cross, condition);
            var greaterSign = math.select(targetSign, sign, condition);

            condition = BezierMath.EqualsForLargeValues(targetDistance, distance);
            var equalDistance = math.select(greaterDistance, resolverCornerDistance, condition);
            var equalCross = math.select(greaterCross, resolverCornerCross, condition);
            var equalSign = math.select(greaterSign, resolverCornerSign, condition);

            condition = targetSign == 0;
            var pixelNotSetDistance = math.select(equalDistance, distance, condition);
            var pixelNotSetCross = math.select(equalCross, cross, condition);
            var pixelNotSetSign = math.select(equalSign, sign, condition);

            // ignore if the distance is greater than spread to avoid artifacts caused by wrong sign
            condition = distance > sp_sq;
            validDistance = math.select(pixelNotSetDistance, targetDistance, condition);
            validCross = math.select(pixelNotSetCross, targetCross, condition);
            validSign = math.select(pixelNotSetSign, targetSign, condition);
        }

        public static void GetValid_DistanceCrossSign_Legacy(
            ref float distance,
            ref float cross,
            ref int sign,
            ref float targetDistance,
            ref float targetCross,
            ref int targetSign,
            float sp_sq,
            out float validDistance,
            out float validCross,
            out int validSign
            )
        {
            if (distance > sp_sq)
            {
                validDistance = targetDistance;
                validCross = targetCross;
                validSign = targetSign;
                return;
            }
            if (targetSign == 0) // check if the pixel is already set
            {
                validDistance = distance;
                validCross = cross;
                validSign = sign;
                return;
            }
            else
            {
                if (BezierMath.EqualsForLargeValues(targetDistance, distance))
                {
                    var condition = math.abs(cross) > math.abs(targetCross);
                    validDistance = math.select(targetDistance, distance, condition);
                    validCross = math.select(targetCross, cross, condition);
                    validSign = math.select(targetSign, sign, condition);
                    return;
                }
                else if (targetDistance > distance)
                {
                    validDistance = distance;
                    validCross = cross;
                    validSign = sign;
                    return;
                }
            }
            validDistance = targetDistance;
            validCross = targetCross;
            validSign = targetSign;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetMinDistanceLineToPoint(float4 ax, float4 ay, float4 bx, float4 by, float4 px, float4 py, out float4 distance, out float4 cross, out int4 sign)
        {
            var abx = bx - ax;                          // Vector from A to B
            var aby = by - ay;                          // Vector from A to B
            var apx = px - ax;                          // Vector from A to P
            var apy = py - ay;                          // Vector from A to P
            var abLengthSq = abx * abx + aby * aby;
            var frac = abx * apx + aby * apy;
            frac = math.max(frac, 0.0f);                // Check if P projection is over vectorAB 
            frac = math.min(frac, abLengthSq);          // Check if P projection is over vectorAB 

            frac = frac / abLengthSq;                   // The normalized "distance" from a to your closest point
            var nx = ax + abx * frac;                   // nearest point on egde
            var ny = ay + aby * frac;                   // nearest point on egde

            var pnx = nx - px;
            var pny = ny - py;
            var pnLengthSq = pnx * pnx + pny * pny;
            cross = BezierMath.cross2D(pnx, pny, abx, aby);

            sign = math.select(-1, 1, cross < 0);
            distance = math.select(math.sqrt(pnLengthSq), pnLengthSq, SDFCommon.USE_SQUARED_DISTANCES);

            var nIsEndPoint = BezierMath.EqualsForSmallValues(frac, 0) | BezierMath.EqualsForSmallValues(frac, 1);
            cross = math.select(1, GetCross(abx, aby, pnx, pny, abLengthSq, pnLengthSq), nIsEndPoint);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetMinDistanceLineToPoint(float ax, float ay, float bx, float by, float px, float py, out float distance, out float cross, out int sign)
        {
            var abx = bx - ax;                          // Vector from A to B
            var aby = by - ay;                          // Vector from A to B
            var apx = px - ax;                          // Vector from A to P
            var apy = py - ay;                          // Vector from A to P
            var abLengthSq = abx * abx + aby * aby;
            var frac = abx * apx + aby * apy;
            frac = math.max(frac, 0.0f);                // Check if P projection is over vectorAB 
            frac = math.min(frac, abLengthSq);          // Check if P projection is over vectorAB 

            frac = frac / abLengthSq;                   // The normalized "distance" from a to your closest point
            var nx = ax + abx * frac;                   // nearest point on egde
            var ny = ay + aby * frac;                   // nearest point on egde

            var pnx = nx - px;
            var pny = ny - py;
            var pnLengthSq = pnx * pnx + pny * pny;
            cross = BezierMath.cross2D(pnx, pny, abx, aby);

            sign = math.select(-1, 1, cross < 0);
            distance = math.select(math.sqrt(pnLengthSq), pnLengthSq, SDFCommon.USE_SQUARED_DISTANCES);

            var nIsEndPoint = BezierMath.EqualsForSmallValues(frac, 0) | BezierMath.EqualsForSmallValues(frac, 1);
            cross = math.select(1, GetCross(abx, aby, pnx, pny, abLengthSq, pnLengthSq), nIsEndPoint);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float GetCross(float abx, float aby, float pnx, float pny, float abLengthSq, float pnLengthSq)
        {
            var abLength = math.sqrt(abLengthSq);
            var pnLength = math.sqrt(pnLengthSq);
            var abxNorm = abx / abLength;
            var abyNorm = aby / abLength;
            var pnxNorm = pnx / pnLength;
            var pnyNorm = pny / pnLength;
            return BezierMath.cross2D(abxNorm, abyNorm, pnxNorm, pnyNorm);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float4 GetCross(float4 abx, float4 aby, float4 pnx, float4 pny, float4 abLengthSq, float4 pnLengthSq)
        {
            var abLength = math.sqrt(abLengthSq);
            var pnLength = math.sqrt(pnLengthSq);
            var abxNorm = abx / abLength;
            var abyNorm = aby / abLength;
            var pnxNorm = pnx / pnLength;
            var pnyNorm = pny / pnLength;
            return BezierMath.cross2D(abxNorm, abyNorm, pnxNorm, pnyNorm);
        }

        public static void WriteGlyphOutlineToFile(string path, NativeList<SDFEdge> edges)
        {
            if(edges.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            var edge = edges[0];
            writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
            for (int i = 0, end = edges.Length; i < end; i++)
            {
                edge = edges[i];
                writer.WriteLine($"{edge.end_pos.x} {edge.end_pos.y}");              
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteGlyphOutlineToFile(string path, NativeList<Edge> edges)
        {
            if (edges.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            var edge = edges[0];

            for (int i = 0, end = edges.Length; i < end; i++)
            {
                edge = edges[i];
                writer.WriteLine($"{edge.x0} {edge.y0} {edge.invert}");
                writer.WriteLine($"{edge.x1} {edge.y1}");
                writer.WriteLine();
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteMinDistancesToFile(string path, in NativeArray<SDFDebug> sdfDebug)
        {
            if (sdfDebug.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = sdfDebug.Length; i < end; i++)
            {
                writer.WriteLine($"{sdfDebug[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteMinDistancesToFile(string path, in NativeArray<float> minDistances)
        {
            if (minDistances.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = minDistances.Length; i < end; i++)
            {
                writer.WriteLine($"{minDistances[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteMinDistancesToFile(string path, in NativeArray<byte> minDistances)
        {
            if (minDistances.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = minDistances.Length; i < end; i++)
            {
                writer.WriteLine($"{minDistances[i]}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteMinDistancesToFile(string path, NativeArray<SDFDebug> sdfDebug)
        {
            if (sdfDebug.Length == 0) return;
            StreamWriter writer = new StreamWriter(path, false);
            for (int i = 0, end = sdfDebug.Length; i < end; i++)
            {
                var c = sdfDebug[i];
                writer.WriteLine($"{c.row} {c.column} {c.distanceRaw} {c.signRaw} {c.currentSignRaw} {c.distance} {c.sign} {c.currentSign} {c.cross}");
            }
            writer.WriteLine();
            writer.Close();
        }
        public static void WriteGlyphOutlineToFile(string path, ref DrawData drawData, bool fullBezier=false)
        {
            var edges = drawData.edges;
            var contourIDs = drawData.contourIDs;
            if (contourIDs.Length < 2 || edges.Length == 0)
                return;

            StreamWriter writer = new StreamWriter(path, false);
            SDFEdge edge;
            for (int contourID = 0, end = contourIDs.Length - 1; contourID < end; contourID++) //for each contour
            {
                int startID = contourIDs[contourID];
                int nextStartID = contourIDs[contourID + 1];
                for (int edgeID = startID; edgeID < nextStartID; edgeID++) //for each edge
                {
                    edge = edges[edgeID];
                    if(fullBezier)
                        writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y} {edge.control1.x} {edge.control1.y} {edge.end_pos.x} {edge.end_pos.y} {edge.edge_type}");
                    else
                        writer.WriteLine($"{edge.start_pos.x} {edge.start_pos.y}");
                    
                }
                writer.WriteLine();
            }
            writer.Close();
        }
    }
    public struct SDFDebug
    {
        public int row;
        public int column;
        public float distanceRaw;
        public int signRaw;
        public float distance;
        public int sign;
        public float cross;
        public int currentSignRaw;
        public int currentSign;
        public SDFDebug(int row, int column, float distanceRaw, int signRaw, int currentSignRaw, float cross)
        {
            this.row = row;
            this.column = column;
            this.distanceRaw = distanceRaw;
            this.signRaw = signRaw;
            this.currentSignRaw = currentSignRaw;
            this.distance = float.MinValue;
            this.sign = int.MinValue;
            this.cross = cross;
            this.currentSign = 0;
        }
    }

}
