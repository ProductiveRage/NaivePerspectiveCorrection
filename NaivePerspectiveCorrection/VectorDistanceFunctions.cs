using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace NaivePerspectiveCorrection
{
    // Code copied from https://github.com/curiosity-ai/umap-sharp
    internal static class VectorDistanceFunctions
    {
        public static float Cosine(float[] lhs, float[] rhs) => 1 - (SIMD.DotProduct(ref lhs, ref rhs) / (SIMD.Magnitude(ref lhs) * SIMD.Magnitude(ref rhs)));

        public static float CosineForNormalizedVectors(float[] lhs, float[] rhs) => 1 - SIMD.DotProduct(ref lhs, ref rhs);

        public static float Euclidean(float[] lhs, float[] rhs) => MathF.Sqrt(SIMD.Euclidean(ref lhs, ref rhs));

        private static class SIMD
        {
            private static readonly int _vs1 = Vector<float>.Count;
            private static readonly int _vs2 = 2 * Vector<float>.Count;
            private static readonly int _vs3 = 3 * Vector<float>.Count;
            private static readonly int _vs4 = 4 * Vector<float>.Count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Magnitude(ref float[] vec) => (float)Math.Sqrt(DotProduct(ref vec, ref vec));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Euclidean(ref float[] lhs, ref float[] rhs)
            {
                float result = 0f;

                var count = lhs.Length;
                var offset = 0;
                Vector<float> diff;
                while (count >= _vs4)
                {
                    diff = new Vector<float>(lhs, offset) - new Vector<float>(rhs, offset); result += Vector.Dot(diff, diff);
                    diff = new Vector<float>(lhs, offset + _vs1) - new Vector<float>(rhs, offset + _vs1); result += Vector.Dot(diff, diff);
                    diff = new Vector<float>(lhs, offset + _vs2) - new Vector<float>(rhs, offset + _vs2); result += Vector.Dot(diff, diff);
                    diff = new Vector<float>(lhs, offset + _vs3) - new Vector<float>(rhs, offset + _vs3); result += Vector.Dot(diff, diff);
                    if (count == _vs4) return result;
                    count -= _vs4;
                    offset += _vs4;
                }

                if (count >= _vs2)
                {
                    diff = new Vector<float>(lhs, offset) - new Vector<float>(rhs, offset); result += Vector.Dot(diff, diff);
                    diff = new Vector<float>(lhs, offset + _vs1) - new Vector<float>(rhs, offset + _vs1); result += Vector.Dot(diff, diff);
                    if (count == _vs2) return result;
                    count -= _vs2;
                    offset += _vs2;
                }
                if (count >= _vs1)
                {
                    diff = new Vector<float>(lhs, offset) - new Vector<float>(rhs, offset); result += Vector.Dot(diff, diff);
                    if (count == _vs1) return result;
                    count -= _vs1;
                    offset += _vs1;
                }
                if (count > 0)
                {
                    while (count > 0)
                    {
                        var d = (lhs[offset] - rhs[offset]);
                        result += d * d;
                        offset++; count--;
                    }
                }
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float DotProduct(ref float[] lhs, ref float[] rhs)
            {
                var result = 0f;
                var count = lhs.Length;
                var offset = 0;
                while (count >= _vs4)
                {
                    result += Vector.Dot(new Vector<float>(lhs, offset), new Vector<float>(rhs, offset));
                    result += Vector.Dot(new Vector<float>(lhs, offset + _vs1), new Vector<float>(rhs, offset + _vs1));
                    result += Vector.Dot(new Vector<float>(lhs, offset + _vs2), new Vector<float>(rhs, offset + _vs2));
                    result += Vector.Dot(new Vector<float>(lhs, offset + _vs3), new Vector<float>(rhs, offset + _vs3));
                    if (count == _vs4) return result;
                    count -= _vs4;
                    offset += _vs4;
                }
                if (count >= _vs2)
                {
                    result += Vector.Dot(new Vector<float>(lhs, offset), new Vector<float>(rhs, offset));
                    result += Vector.Dot(new Vector<float>(lhs, offset + _vs1), new Vector<float>(rhs, offset + _vs1));
                    if (count == _vs2) return result;
                    count -= _vs2;
                    offset += _vs2;
                }
                if (count >= _vs1)
                {
                    result += Vector.Dot(new Vector<float>(lhs, offset), new Vector<float>(rhs, offset));
                    if (count == _vs1) return result;
                    count -= _vs1;
                    offset += _vs1;
                }
                if (count > 0)
                {
                    while (count > 0)
                    {
                        result += lhs[offset] * rhs[offset];
                        offset++; count--;
                    }
                }
                return result;
            }
        }
    }
}