using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace NaivePerspectiveCorrection
{
    internal static class Program
    {
        static void Main()
        {
            // I'm using this code to try to fix blurry slides in a video of a presentation that I gave; I want to match the slide shown in each frame to the original slide images and then overlay the original over the projected slides in
            // the video as they're too blurry to read
            var files = new DirectoryInfo("Frames").EnumerateFiles("*.jpg");
            var (topLeft, topRight, bottomRight, bottomLeft) = files
                .AsParallel()
                .Select(file =>
                {
                    using var image = new Bitmap(file.FullName);
                    return IlluminatedAreaLocator.GetMostHighlightedArea(image);
                })
                .GroupBy(area => area)
                .Select(group => (Area: group.Key, Count: group.Count()))
                .OrderByDescending(entry => entry.Count)
                .First()
                .Area;

            var projectionSize = SimplePerspectiveCorrection.GetProjectionSize(topLeft, topRight, bottomRight, bottomLeft);
            var originalSlideVectors = new DirectoryInfo("Slides")
                .EnumerateFiles("*.png")
                .AsParallel()
                .Select(originalSlideImageFile =>
                {
                    using var originalSlideImage = new Bitmap(originalSlideImageFile.FullName);

                    var projectionRatio = (double)projectionSize.Width / projectionSize.Height;
                    var widthToCropFromOriginalSlide = (int)Math.Round(originalSlideImage.Height * projectionRatio);

                    using var resizedOriginalSlideImage = new Bitmap(projectionSize.Width, projectionSize.Height);
                    using var g = Graphics.FromImage(resizedOriginalSlideImage);

                    // TODO: The code here presumes that if the slide was cropped out of the frame that it will be the right hand side of it that is cut off, which is correct for my video but that may not always be the case and so
                    // there should be some logic to try to guess what is most likely to have been cropped off based upon the four points that are known to frame the projected slide in the video
                    g.DrawImage(
                        originalSlideImage,
                        srcRect: new Rectangle(0, 0, widthToCropFromOriginalSlide, originalSlideImage.Height),
                        destRect: new Rectangle(0, 0, projectionSize.Width, projectionSize.Height),
                        srcUnit: GraphicsUnit.Pixel
                    );

                    return (
                        Name: Path.GetFileNameWithoutExtension(originalSlideImageFile.Name),
                        Vector: resizedOriginalSlideImage.GetVector()
                    );
                })
                .ToArray(); // Cal ToArray to evaluate immediately and not repeat this work each time the originalSlideVectors list is looked at

            var closestSlides = files
                .AsParallel()
                .Select(file =>
                {
                    using var image = new Bitmap(file.FullName);
                    using var projection = SimplePerspectiveCorrection.ExtractAndPerspectiveCorrect(image, topLeft, topRight, bottomRight, bottomLeft);
                    projection.Save("Projection of " + file.Name); // This is just for visual checking / debugging

                    return (
                        Filename: file.Name,
                        ClosestSlide: originalSlideVectors
                            .Select(slide => (slide.Name, Distance: VectorDistanceFunctions.Cosine(projection.GetVector(), slide.Vector)))
                            .OrderBy(slide => slide.Distance)
                            .First()
                            .Name
                    );
                });

            foreach (var (filename, closestSlide) in closestSlides.OrderBy(entry => int.Parse(entry.Filename.Split('.').First()[6..])))
                Console.WriteLine(filename + ": " + closestSlide);

            Console.WriteLine();
            Console.WriteLine("Finished! Press [Enter] to terminate.. ");
            Console.ReadLine();
        }
    }
}