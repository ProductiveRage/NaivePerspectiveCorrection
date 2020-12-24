using System;

namespace NaivePerspectiveCorrection
{
    internal static class DataRectangleOfDoubleExtensions
    {
        public static (double Min, double Max) GetMinAndMax(this DataRectangle<double> source)
        {
            return source.Aggregate(
                seed: (Min: double.MaxValue, Max: double.MinValue),
                func: (acc, value) => (Math.Min(value, acc.Min), Math.Max(value, acc.Max))
            );
        }

        public static DataRectangle<bool> Mask(this DataRectangle<double> values, double threshold) => values.Transform(value => value >= threshold);

        /// <summary>
        /// This will ensure that all double values are between 0 and 1 (inclusive)
        /// </summary>
        public static DataRectangle<double> Normalise(this DataRectangle<double> source)
        {
            var (minValue, maxValue) = source.GetMinAndMax();
            var range = maxValue - minValue;
            return range > 0
                ? source.Transform(value => (value - minValue) / range)
                : source.Transform(value => 0d);
        }
    }
}