using System;

namespace NaivePerspectiveCorrection
{
    internal static class DataRectangleOfDoubleExtensions
    {
        /// <summary>
        /// This will ensure that all double values are between 0 and 1 (inclusive)
        /// </summary>
        public static DataRectangle<double> Normalise(this DataRectangle<double> source)
        {
            var (minValue, maxValue) = source.Aggregate(
                seed: (Min: double.MaxValue, Max: double.MinValue),
                func: (acc, value) => (Math.Min(value, acc.Min), Math.Max(value, acc.Max))
            );
            var range = maxValue - minValue;
            return range > 0
                ? source.Transform(value => (value - minValue) / range)
                : source.Transform(value => 0d);
        }
    }
}