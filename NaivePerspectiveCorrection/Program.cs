using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NaivePerspectiveCorrection
{
    internal static class Program
    {
        static void Main()
        {
            // I'm using this code to try to fix blurry slides in a video of a presentation that I gave; I want to match the slide shown in each frame to the original
            // slide images and then overlay the original over the projected slides in the video as they're too blurry to read
            
            // All of the individual frame images should have file names of the format "frame_123.jpg", so search for them and parse out the frame number for each
            var frameIndexMatcher = new Regex(@"frame_(\d+)\.jpg", RegexOptions.IgnoreCase);
            var files = new DirectoryInfo("Frames")
                .EnumerateFiles("*.jpg")
                .Select(file =>
                {
                    var frameIndexMatch = frameIndexMatcher.Match(file.Name);
                    return frameIndexMatch.Success
                        ? (file.FullName, FrameIndex: int.Parse(frameIndexMatch.Groups[1].Value))
                        : default;
                })
                .Where(entry => entry != default)
                .ToArray(); // Call ToArray to evaluate immediately instead of repeating the file system access every time that "files" is enumerated

            // Identify the largest bright area in each of the frames..
            var allFrameHighlightedAreas = files
                .AsParallel()
                .Select(file =>
                {
                    using var image = new Bitmap(file.FullName);
                    return (file.FrameIndex, HighlightedArea: IlluminatedAreaLocator.GetMostHighlightedArea(image));
                })
                .ToArray(); // Call ToArray to evaluate immediately to avoid repeating this work every time that "allFrameHighlightedAreas" is enumerated

            // .. then look for the area that appears most commonly (there will be some variance as the camera filming the presentation loses and regains focus -
            // blurrier images will have larger brightest areas)..
            var (topLeft, topRight, bottomRight, bottomLeft) = allFrameHighlightedAreas
                .GroupBy(entry => entry.HighlightedArea)
                .OrderByDescending(group => group.Count())
                .First()
                .Key;

            // .. then look for the first and last frames that have this most common brightest area (within some margin of error) - frames before or after this
            // range are likely to be part of the intro and outro animations
            var highlightedAreaWidth = Math.Max(topRight.X, bottomRight.X) - Math.Min(topLeft.X, bottomLeft.X);
            var highlightedAreaHeight = Math.Max(bottomLeft.Y, bottomRight.Y) - Math.Min(topLeft.Y, topRight.Y);
            const double maxProportionOfAreaDimensionToConsiderEquivalent = 0.2;
            var frameIndexesThatHaveTheMostCommonHighlightedArea = allFrameHighlightedAreas
                .Where(entry =>
                {
                    var (entryTL, entryTR, entryBR, entryBL) = entry.HighlightedArea;
                    var xVariance = new[] { entryBL.X - bottomLeft.X, entryBR.X - bottomRight.X, entryTL.X - topLeft.X, entryTR.X - topRight.X, }.Sum(Math.Abs);
                    var yVariance = new[] { entryBL.Y - bottomLeft.Y, entryBR.Y - bottomRight.Y, entryTL.Y - topLeft.Y, entryTR.Y - topRight.Y }.Sum(Math.Abs);
                    return
                        (xVariance <= highlightedAreaWidth * maxProportionOfAreaDimensionToConsiderEquivalent) &&
                        (yVariance <= highlightedAreaHeight * maxProportionOfAreaDimensionToConsiderEquivalent);
                })
                .Select(entry => entry.FrameIndex);
            var firstFrameIndex = frameIndexesThatHaveTheMostCommonHighlightedArea.Min();
            var lasttFrameIndex = frameIndexesThatHaveTheMostCommonHighlightedArea.Max();

            // Presuming that the most common brightest area is a projection of a slide onto a wall, causing the rectangular slide to become a quadrilateral
            // with sloped sides due to perspective, estimate what dimensions / aspect ratio the original content would have had (in the video, the camera
            // position means that part of the projection is cut off and so the aspect ratio of the projection captured in the video isn't going to be the
            // same as the aspect ratio of the original slide images)
            var projectionSize = SimplePerspectiveCorrection.GetProjectionSize(topLeft, topRight, bottomRight, bottomLeft);

            // Read in every slide image, crop them to match the aspect ratio / dimensions calculated above and then generate a vector that describes it,
            // based upon how pixels get lighter and darker across the image
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

                    // TODO: The code here presumes that if the slide was cropped out of the frame that it will be the right hand side of it that is cut off,
                    // which is correct for my video but that may not always be the case and so there should be some logic to try to guess what is most likely
                    // to have been cropped off based upon the four points that are known to frame the projected slide in the video
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
                .ToArray(); // Call ToArray to evaluate immediately and not repeat this work each time that "originalSlideVectors" is enumerated

            // Finally, read the original frame images, extract the content from the area that was found to be the most common bright area across them all
            // and stretch the quarilateral back into a rectangle - this will match the projectionSize calculated earlier that the slide images were cropped
            // and resized to. Then vectors are generated for the rectangular content extracted from each image and compared to the vectors generated for the
            // slide images - the closer that vectors are to each other, the more similar the images should be that the vectors represent (it's important that
            // the vectors are all the same length and so it's important that all the images are the same size - which they all are, as the slides were cropped
            // and resized to the calculated "projectionSize" above and the frame images will be manipulated into the same size below).
            var closestSlides = files
                .AsParallel()
                .Select(file =>
                {
                    string? closestSlide;
                    if ((file.FrameIndex < firstFrameIndex) || (file.FrameIndex > lasttFrameIndex))
                    {
                        // If this frame is part of the intro / outro animation then don't try to match it to a slide because it's not part of the presentation
                        closestSlide = null;
                    }
                    else
                    {
                        using var image = new Bitmap(file.FullName);
                        using var projection = SimplePerspectiveCorrection.ExtractAndPerspectiveCorrect(image, topLeft, topRight, bottomRight, bottomLeft);
                        var projectionVector = projection.GetVector();
                        closestSlide = originalSlideVectors
                            .Select(slide => (slide.Name, Distance: VectorDistanceFunctions.Cosine(projectionVector, slide.Vector)))
                            .OrderBy(slide => slide.Distance)
                            .First()
                            .Name;

                        // This image saving is just for visual checking / debugging
                        var fileName = file.FullName.Split(Path.DirectorySeparatorChar).Last();
                        projection.Save("Projection of " + fileName);
                    }

                    return (file.FrameIndex, ClosestSlide: closestSlide);
                });

            foreach (var (frameIndex, closestSlide) in closestSlides.OrderBy(entry => entry.FrameIndex))
                Console.WriteLine($"Frame {frameIndex}: {closestSlide ?? "- No Projection -"}");

            Console.WriteLine();
            Console.WriteLine("Finished! Press [Enter] to terminate.. ");
            Console.ReadLine();
        }
    }
}