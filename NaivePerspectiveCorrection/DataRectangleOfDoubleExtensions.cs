using System.Linq;

namespace NaivePerspectiveCorrection
{
    internal static class DataRectangleOfDoubleExtensions
    {
        /// <summary>
        /// This will ensure that all double values are between 0 and 1 (inclusive)
        /// </summary>
        public static DataRectangle<double> Normalise(this DataRectangle<double> source)
        {
            var minValue = source.Enumerate().Min(pointAndValue => pointAndValue.Value);
            var maxValue = source.Enumerate().Max(pointAndValue => pointAndValue.Value);
            var range = maxValue - minValue;
            return range > 0
                ? source.Transform(value => (value - minValue) / range)
                : source;
        }
    }
}