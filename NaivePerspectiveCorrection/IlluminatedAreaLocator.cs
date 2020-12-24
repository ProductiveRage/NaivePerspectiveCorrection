using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace NaivePerspectiveCorrection
{
    internal static class IlluminatedAreaLocator
    {
        /// <summary>
        /// Return the largest bright area of the image - this should be the projected slide in a presentation
        /// </summary>
        public static (Point TopLeft, Point TopRight, Point BottomRight, Point BottomLeft) GetMostHighlightedArea(Bitmap image)
        {
            var greyScaleImageData = image.GetGreyscaleData(resizeIfLargestSideGreaterThan: 200, resizeTo: 200);

            var (min, max) = greyScaleImageData.GetMinAndMax();
            var range = max - min;
            var thresholdOfRange = 2 / 3d; // This value was selected somewhat arbitrarily but it seems to work well (could try adjusting it if it doesn't work so well on other images)
            var thresholdForMasking = min + (range * thresholdOfRange);
            var mask = greyScaleImageData.Mask(thresholdForMasking);

            var highlightedAreas = GetDistinctObjects(mask);
            if (!highlightedAreas.Any())
                return default;

            var pointsInLargestHighlightedArea = highlightedAreas
                .OrderByDescending(points => points.Count())
                .First()
                .Select(p =>
                {
                    var distanceFromRight = greyScaleImageData.Width - p.X;
                    var distanceFromBottom = greyScaleImageData.Height - p.Y;
                    var fromLeftScore = p.X * p.X;
                    var fromTopScore = p.Y * p.Y;
                    var fromRightScore = distanceFromRight * distanceFromRight;
                    var fromBottomScore = distanceFromBottom * distanceFromBottom;
                    return new
                    {
                        Point = p,
                        FromTopLeft = fromLeftScore + fromTopScore,
                        FromBottomLeft = fromLeftScore + fromBottomScore,
                        FromTopRight = fromRightScore + fromTopScore,
                        FromBottomRight = fromRightScore + fromBottomScore
                    };
                })
                .ToArray(); // Call ToArray because we don't want to repeat this work four times below

            // Determine how much the image was scaled down (if it had to be scaled down at all) by comparing the width of the potentially-scaled-down data to the source image
            var reducedImageSideBy = (double)image.Width / greyScaleImageData.Width;
            return Resize(
                pointsInLargestHighlightedArea.OrderBy(p => p.FromTopLeft).First().Point,
                pointsInLargestHighlightedArea.OrderBy(p => p.FromTopRight).First().Point,
                pointsInLargestHighlightedArea.OrderBy(p => p.FromBottomRight).First().Point,
                pointsInLargestHighlightedArea.OrderBy(p => p.FromBottomLeft).First().Point,
                reducedImageSideBy,
                minX: 0,
                maxX: image.Width - 1,
                minY: 0,
                maxY: image.Height - 1
            );
        }

        private static DataRectangle<double> GetGreyscaleData(this Bitmap image, int resizeIfLargestSideGreaterThan, int resizeTo)
        {
            var largestSide = Math.Max(image.Width, image.Height);
            if (largestSide <= resizeIfLargestSideGreaterThan)
                return image.GetGreyscale();

            var (width, height) = (image.Width > image.Height)
                ? (resizeTo, (int)((double)image.Height / image.Width * resizeTo))
                : ((int)((double)image.Width / image.Height * resizeTo), resizeTo);

            using var resizedImage = new Bitmap(image, width, height);
            return resizedImage.GetGreyscale();
        }

        private static IEnumerable<IEnumerable<Point>> GetDistinctObjects(DataRectangle<bool> mask)
        {
            // Flood fill areas in the looks-like-bar-code mask to create distinct areas
            var allPoints = mask.Enumerate().Where(pointAndIsMasked => pointAndIsMasked.Value).Select(pointAndIsMasked => pointAndIsMasked.Point).ToHashSet();
            while (allPoints.Any())
            {
                var currentPoint = allPoints.First();
                var pointsInObject = GetPointsInObject(currentPoint).ToArray();
                foreach (var point in pointsInObject)
                    allPoints.Remove(point);
                yield return pointsInObject;
            }

            // Inspired by code at https://simpledevcode.wordpress.com/2015/12/29/flood-fill-algorithm-using-c-net/
            IEnumerable<Point> GetPointsInObject(Point startAt)
            {
                var pixels = new Stack<Point>();
                pixels.Push(startAt);

                var valueAtOriginPoint = mask[startAt.X, startAt.Y];
                var filledPixels = new HashSet<Point>();
                while (pixels.Count > 0)
                {
                    var currentPoint = pixels.Pop();
                    if ((currentPoint.X < 0) || (currentPoint.X >= mask.Width) || (currentPoint.Y < 0) || (currentPoint.Y >= mask.Height))
                        continue;

                    if ((mask[currentPoint.X, currentPoint.Y] == valueAtOriginPoint) && !filledPixels.Contains(currentPoint))
                    {
                        filledPixels.Add(new Point(currentPoint.X, currentPoint.Y));
                        pixels.Push(new Point(currentPoint.X - 1, currentPoint.Y));
                        pixels.Push(new Point(currentPoint.X + 1, currentPoint.Y));
                        pixels.Push(new Point(currentPoint.X, currentPoint.Y - 1));
                        pixels.Push(new Point(currentPoint.X, currentPoint.Y + 1));
                    }
                }
                return filledPixels;
            }
        }

        private static (Point TopLeft, Point TopRight, Point BottomRight, Point BottomLeft) Resize(Point topLeft, Point topRight, Point bottomRight, Point bottomLeft, double resizeBy, int minX, int maxX, int minY, int maxY)
        {
            if (resizeBy <= 0)
                throw new ArgumentOutOfRangeException("must be a positive value (less than one to make smaller, greater than one to make larger)", nameof(resizeBy));

            return (
                Constrain(Multiply(topLeft)),
                Constrain(Multiply(topRight)),
                Constrain(Multiply(bottomRight)),
                Constrain(Multiply(bottomLeft))
            );

            Point Multiply(Point p) => new Point((int)Math.Round(p.X * resizeBy), (int)Math.Round(p.Y * resizeBy));

            Point Constrain(Point p) => new Point(Math.Min(Math.Max(p.X, minX), maxX), Math.Min(Math.Max(p.Y, minY), maxY));
        }
    }
}