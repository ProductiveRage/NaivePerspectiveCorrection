﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace NaivePerspectiveCorrection
{
    internal static class BitmapExtensions
    {
        public static float[] GetVector(this Bitmap image, double divideDimensionsBy = 8, double blockSizeFractionToMove = 0.25)
        {
            return image
                .GetGreyscale()
                .Transform((intensity, pos, source) =>
                {
                    var horizontalChange = (pos.X > 0) && (pos.X < source.Width - 1)
                        ? source[pos.X + 1, pos.Y] - source[pos.X - 1, pos.Y]
                        : 0;

                    var verticalChange = (pos.Y > 0) && (pos.Y < source.Height - 1)
                        ? source[pos.X, pos.Y + 1] - source[pos.X, pos.Y - 1]
                        : 0;

                    return Math.Abs(horizontalChange) + Math.Abs(verticalChange);
                })
                .Normalise()
                .BlockOut(
                    blockSize: (int)Math.Round(Math.Min(image.Width / divideDimensionsBy, image.Height / divideDimensionsBy)),
                    blockSizeFractionToMove,
                    reducer: block => (float)block.Enumerate().Average(pointAndValue => pointAndValue.Value)
                )
                .Enumerate()
                .Select(pointAndValue => pointAndValue.Value)
                .ToArray();
        }

        /// <summary>
        /// This will return values in the range 0-255 (inclusive)
        /// </summary>
        // Based on http://stackoverflow.com/a/4748383/3813189
        public static DataRectangle<double> GetGreyscale(this Bitmap image)
        {
            return image
                .GetAllPixels()
                .Transform(c => (0.2989 * c.R) + (0.5870 * c.G) + (0.1140 * c.B));
        }

        public static DataRectangle<Color> GetAllPixels(this Bitmap image)
        {
            var values = new Color[image.Width, image.Height];
            var data = image.LockBits(
                new Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb
            );
            try
            {
                var pixelData = new byte[data.Stride];
                for (var lineIndex = 0; lineIndex < data.Height; lineIndex++)
                {
                    Marshal.Copy(
                        source: data.Scan0 + (lineIndex * data.Stride),
                        destination: pixelData,
                        startIndex: 0,
                        length: data.Stride
                    );
                    for (var pixelOffset = 0; pixelOffset < data.Width; pixelOffset++)
                    {
                        // Note: PixelFormat.Format24bppRgb means the data is stored in memory as BGR
                        const int PixelWidth = 3;
                        values[pixelOffset, lineIndex] = Color.FromArgb(
                            red: pixelData[pixelOffset * PixelWidth + 2],
                            green: pixelData[pixelOffset * PixelWidth + 1],
                            blue: pixelData[pixelOffset * PixelWidth]
                        );
                    }
                }
            }
            finally
            {
                image.UnlockBits(data);
            }
            return DataRectangle.For(values);
        }
    }
}