using System;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace NaivePerspectiveCorrection
{
    // Code based on a file from https://github.com/curiosity-ai/umap-sharp
    internal static class VectorDistanceFunctions
    {
        public static float Cosine(ImmutableArray<float> lhs, ImmutableArray<float> rhs) => 1 - (SIMD.DotProduct(lhs.AsSpan(), rhs.AsSpan()) / (SIMD.Magnitude(lhs.AsSpan()) * SIMD.Magnitude(rhs.AsSpan())));

        public static float CosineForNormalizedVectors(ImmutableArray<float> lhs, ImmutableArray<float> rhs) => 1 - SIMD.DotProduct(lhs.AsSpan(), rhs.AsSpan());

        public static float Euclidean(ImmutableArray<float> lhs, ImmutableArray<float> rhs) => MathF.Sqrt(SIMD.Euclidean(lhs.AsSpan(), rhs.AsSpan()));

        private static class SIMD
        {
            private static readonly int _vs1 =     Vector<float>.Count;
            private static readonly int _vs2 = 2 * Vector<float>.Count;
            private static readonly int _vs3 = 3 * Vector<float>.Count;
            private static readonly int _vs4 = 4 * Vector<float>.Count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Magnitude(ReadOnlySpan<float> vec) => (float)Math.Sqrt(DotProduct(vec, vec));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static float Euclidean(ReadOnlySpan<float> lhs, ReadOnlySpan<float> rhs)
            {
                var result = 0f;

                var count = lhs.Length;
                var offset = 0;
                Vector<float> diff;
                while (count >= _vs4)
                {
                    diff = new Vector<float>(lhs[offset..])          - new Vector<float>(rhs[offset..]);          result += Vector.Dot(diff, diff);
                    diff = new Vector<float>(lhs[(offset + _vs1)..]) - new Vector<float>(rhs[(offset + _vs1)..]); result += Vector.Dot(diff, diff);
                    diff = new Vector<float>(lhs[(offset + _vs2)..]) - new Vector<float>(rhs[(offset + _vs2)..]); result += Vector.Dot(diff, diff);
                    diff = new Vector<float>(lhs[(offset + _vs3)..]) - new Vector<float>(rhs[(offset + _vs3)..]); result += Vector.Dot(diff, diff);
                    if (count == _vs4) return result;
                    count -= _vs4;
                    offset += _vs4;
                }

                if (count >= _vs2)
                {
                    diff = new Vector<float>(lhs[offset..])          - new Vector<float>(rhs[offset..]);          result += Vector.Dot(diff, diff);
                    diff = new Vector<float>(lhs[(offset + _vs1)..]) - new Vector<float>(rhs[(offset + _vs1)..]); result += Vector.Dot(diff, diff);
                    if (count == _vs2) return result;
                    count -= _vs2;
                    offset += _vs2;
                }
                if (count >= _vs1)
                {
                    diff = new Vector<float>(lhs[offset..])          - new Vector<float>(rhs[offset..]);          result += Vector.Dot(diff, diff);
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
            public static float DotProduct(ReadOnlySpan<float> lhs, ReadOnlySpan<float> rhs)
            {
                var result = 0f;
                var count = lhs.Length;
                var offset = 0;
                while (count >= _vs4)
                {
                    result += Vector.Dot(new Vector<float>(lhs[offset..]),          new Vector<float>(rhs[offset..]));
                    result += Vector.Dot(new Vector<float>(lhs[(offset + _vs1)..]), new Vector<float>(rhs[(offset + _vs1)..]));
                    result += Vector.Dot(new Vector<float>(lhs[(offset + _vs2)..]), new Vector<float>(rhs[(offset + _vs2)..]));
                    result += Vector.Dot(new Vector<float>(lhs[(offset + _vs3)..]), new Vector<float>(rhs[(offset + _vs3)..]));
                    if (count == _vs4) return result;
                    count -= _vs4;
                    offset += _vs4;
                }
                if (count >= _vs2)
                {
                    result += Vector.Dot(new Vector<float>(lhs[offset..]),          new Vector<float>(rhs[offset..]));
                    result += Vector.Dot(new Vector<float>(lhs[(offset + _vs1)..]), new Vector<float>(rhs[(offset + _vs1)..]));
                    if (count == _vs2) return result;
                    count -= _vs2;
                    offset += _vs2;
                }
                if (count >= _vs1)
                {
                    result += Vector.Dot(new Vector<float>(lhs[offset..]),          new Vector<float>(rhs[offset..]));
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