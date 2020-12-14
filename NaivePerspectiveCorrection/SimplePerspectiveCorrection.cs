using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace NaivePerspectiveCorrection
{
    /// <summary>
    /// This uses a simple algorithm to try to undo the distortion of a rectangle in an image due to perspective - it takes the content of the rectangle and stretches it into a rectangle. This is only a simple approximation and does not guarantee
    /// accuracy (in fact, it will result in an image that is slightly vertically stretched such that its aspect ratio will not match the original content and a more thorough approach is necessary if this is too great an approximation)
    /// </summary>
    internal static class SimplePerspectiveCorrection
    {
        public static Size GetProjectionSize(Point topLeft, Point topRight, Point bottomRight, Point bottomLeft) => GetProjectionDetails(topLeft, topRight, bottomRight, bottomLeft).ProjectionSize;

        public static Bitmap ExtractAndPerspectiveCorrect(Bitmap image, Point topLeft, Point topRight, Point bottomRight, Point bottomLeft)
        {
            var (projectionSize, sourceSliceLocations, sliceRenderer) = GetProjectionDetails(topLeft, topRight, bottomRight, bottomLeft);
            var pixels = image.GetAllPixels(); // It's quicker to read all of the pixels once and then get their values from a DataRectangle than it is to call GetPixel repeatedly on a bitmap
            var projection = new Bitmap(projectionSize.Width, projectionSize.Height);
            foreach (var (lineToTrace, index) in sourceSliceLocations)
            {
                var lengthOfLineToTrace = LengthOfLine(lineToTrace);

                var pixelsOnLine = Enumerable
                    .Range(0, lengthOfLineToTrace)
                    .Select(j =>
                    {
                        var fractionOfProgressAlongLineToTrace = (float)j / lengthOfLineToTrace;
                        var point = GetPointAlongLine(lineToTrace, fractionOfProgressAlongLineToTrace);
                        return GetAverageColour(pixels, point);
                    });

                sliceRenderer(projection, pixelsOnLine, index);
            }
            return projection;

            static Color GetAverageColour(DataRectangle<Color> pixels, PointF point)
            {
                var (integralX, fractionalX) = GetIntegralAndFractional(point.X);
                var x0 = integralX;
                var x1 = Math.Min(integralX + 1, pixels.Width);

                var (integralY, fractionalY) = GetIntegralAndFractional(point.Y);
                var y0 = integralY;
                var y1 = Math.Min(integralY + 1, pixels.Height);

                var (topColour0, topColour1) = GetColours(new Point(x0, y0), new Point(x1, y0));
                var (bottomColour0, bottomColour1) = GetColours(new Point(x0, y1), new Point(x1, y1));

                return CombineColours(
                    CombineColours(topColour0, topColour1, fractionalX),
                    CombineColours(bottomColour0, bottomColour1, fractionalX),
                    fractionalY
                );

                (Color c0, Color c1) GetColours(Point p0, Point p1)
                {
                    var c0 = pixels[p0.X, p0.Y];
                    var c1 = (p0 == p1) ? c0 : pixels[p1.X, p1.Y];
                    return (c0, c1);
                }

                static (int Integral, float Fractional) GetIntegralAndFractional(float value)
                {
                    var integral = (int)Math.Truncate(value);
                    var fractional = value - integral;
                    return (integral, fractional);
                }

                static Color CombineColours(Color x, Color y, float proportionOfSecondColour)
                {
                    if ((proportionOfSecondColour == 0) || (x == y))
                        return x;

                    if (proportionOfSecondColour == 1)
                        return y;

                    return Color.FromArgb(
                        red: CombineComponent(x.R, y.R),
                        green: CombineComponent(x.G, y.G),
                        blue: CombineComponent(x.B, y.B),
                        alpha: CombineComponent(x.A, y.A)
                    );

                    int CombineComponent(int x, int y) => Math.Min((int)Math.Round((x * (1 - proportionOfSecondColour)) + (y * proportionOfSecondColour)), 255);
                }
            }
        }

        private delegate void SliceRenderer(Bitmap image, IEnumerable<Color> pixelsOnLine, int index);

        /// <summary>
        /// Depending upon the dimensions of the source area, better results may be achieved by taking slices across the image horizontally or vertically - this method will be responsible for deciding that and returning slice enumerators
        /// (for getting the source data in whichever dimension is best) and slice renderers (again, for which dimension is best). Note: For now, this always takes vertical slices but that might change in the future (calling code won't
        /// need to change)
        /// </summary>
        private static (Size ProjectionSize, IEnumerable<((PointF From, PointF To) Line, int Index)> SourceSliceLocations, SliceRenderer SliceRenderer) GetProjectionDetails(Point topLeft, Point topRight, Point bottomRight, Point bottomLeft)
        {
            var edgeToStartFrom = (From: topLeft, To: topRight);
            var edgeToConnectTo = (From: bottomLeft, To: bottomRight);
            var lengthOfEdgeToStartFrom = LengthOfLine(edgeToStartFrom);
            var dimensions = new Size(lengthOfEdgeToStartFrom, Math.Max(LengthOfLine((topLeft, bottomLeft)), LengthOfLine((topRight, bottomRight))));
            return (dimensions, GetSourceSliceLocationEnumerator(), RenderSlice);

            IEnumerable<((PointF From, PointF To) Line, int Index)> GetSourceSliceLocationEnumerator()
            {
                return Enumerable
                    .Range(0, lengthOfEdgeToStartFrom)
                    .Select(i =>
                    {
                        var fractionOfProgressAlongPrimaryEdge = (float)i / lengthOfEdgeToStartFrom;
                        return ((GetPointAlongLine(edgeToStartFrom, fractionOfProgressAlongPrimaryEdge), GetPointAlongLine(edgeToConnectTo, fractionOfProgressAlongPrimaryEdge)), i);
                    });
            }

            static void RenderSlice(Bitmap bitmap, IEnumerable<Color> pixelsOnLine, int index)
            {
                var pixelsOnLineArray = pixelsOnLine.ToArray();

                using var slice = new Bitmap(1, pixelsOnLineArray.Length);
                for (var j = 0; j < pixelsOnLineArray.Length; j++)
                    slice.SetPixel(0, j, pixelsOnLineArray[j]);

                using var g = Graphics.FromImage(bitmap);
                g.DrawImage(
                    slice,
                    srcRect: new Rectangle(0, 0, slice.Width, slice.Height),
                    destRect: new Rectangle(index, 0, 1, bitmap.Height),
                    srcUnit: GraphicsUnit.Pixel
                );
            }
        }

        private static PointF GetPointAlongLine((PointF From, PointF To) line, float fraction)
        {
            var deltaX = line.To.X - line.From.X;
            var deltaY = line.To.Y - line.From.Y;
            return new PointF(
                (deltaX * fraction) + line.From.X,
                (deltaY * fraction) + line.From.Y
            );
        }

        private static int LengthOfLine((PointF From, PointF To) line)
        {
            var deltaX = line.To.X - line.From.X;
            var deltaY = line.To.Y - line.From.Y;
            return (int)Math.Round(Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)));
        }
    }
}