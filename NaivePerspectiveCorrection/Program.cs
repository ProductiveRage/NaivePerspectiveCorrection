using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace NaivePerspectiveCorrection
{
    internal static class Program
    {
        static void Main()
        {
            // I'm using this code to try to fix blurry slides in a video of a presentation that I gave - code that isn't in this project was used to determine the points that form the quadrilateral around the perspective-distorted slides
            // in each of the extracted frames and I want to match the slide shown in each frame to the original slide images and then overlay the original over the projected slides in the video as they're too blurry to read (the same
            // applies to the start and end frames; there are intro and outro animations on the video that I'm trying to improve)
            // - This puts my need for "perspective correction" into context and explains why this project isn't currently something that you can clone and run immediately with provided test data (the file locations are all set to folders
            //   on my computer).. one day I might include some sample data with this project to make it immediately runnable on computers other than my own
            var topLeft = new Point(1228, 194);
            var topRight = new Point(1919, 80);
            var bottomRight = new Point(1919, 654);
            var bottomLeft = new Point(1231, 638);
            const int startFrame = 300;
            const int maxFrame = 59_779;
            const int frequency = 20;

            var originalSlideVectors = Lazy(() =>
            {
                var projectionSize = SimplePerspectiveCorrection.GetProjectionSize(topLeft, topRight, bottomRight, bottomLeft);
                return new DirectoryInfo(@"D:\Downloads\4K Video Downloader\Extracted\Slides")
                    .EnumerateFiles("*.png")
                    .AsParallel() // Note: This happens within a lock (controlled by the Lazy wrapper), so any AsParallel applied to the outer call will have blocked all but one threads and so we might as well put the cores to use here
                    .Select(originalSlideImageFile =>
                    {
                        using var originalSlideImage = new Bitmap(originalSlideImageFile.FullName);

                        var projectionRatio = (double)projectionSize.Width / projectionSize.Height;
                        var widthToCropFromOriginalSlide = (int)Math.Round(originalSlideImage.Height * projectionRatio);

                        using var resizedOriginalSlideImage = new Bitmap(projectionSize.Width, projectionSize.Height);
                        using var g = Graphics.FromImage(resizedOriginalSlideImage);

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
                    .ToArray();
            });

            var closestSlides = Enumerable
                .Range(0, ((maxFrame - startFrame) / frequency) + 1) // Note: +1 in Enumerable.Range's count value so that the maxFrame is included in the list of indexes to look at
                .Select(i => (i * frequency) + startFrame)
                .AsParallel()
                .Select(frameIndex =>
                {
                    using var image = new Bitmap(@"D:\Downloads\4K Video Downloader\Extracted\frame_" + frameIndex + ".png");
                    using var projection = SimplePerspectiveCorrection.ExtractAndPerspectiveCorrect(image, topLeft, topRight, bottomRight, bottomLeft);
                    projection.Save($"Projection Frame {frameIndex}.png", ImageFormat.Png); // This is just for visual checking / debugging

                    return (
                        FrameIndex: frameIndex,
                        ClosestSlide: originalSlideVectors.Value
                            .Select(slide => (slide.Name, Distance: VectorDistanceFunctions.Cosine(projection.GetVector(), slide.Vector)))
                            .OrderBy(slide => slide.Distance)
                            .First()
                            .Name
                    );
                })
                .OrderBy(entry => entry.FrameIndex);

            foreach (var (frameIndex, closestSlide) in closestSlides)
                Console.WriteLine("Frame " + frameIndex + ": " + closestSlide);

            Console.WriteLine();
            Console.WriteLine("Finished! Press [Enter] to terminate.. ");
            Console.ReadLine();
        }

        /// <summary>
        /// Helper method to leverage compiler type inference
        /// </summary>
        private static Lazy<T> Lazy<T>(Func<T> valueFactory) => new Lazy<T>(valueFactory);
    }
}