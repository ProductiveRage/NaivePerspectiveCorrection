# A "naive" approach to perspective correction

I am trying to write some code that will take frames from a video of a presentation that I gave and match each frame up to the original slide. I want to do this because the the slides [in the video](https://www.youtube.com/watch?v=qUCoVAGNCe8) are too fuzzy due to camera placement and intermittent focus issues. If I can achieve this then I intend to overlay the original slide images over the top right quadrant so that the blurry projections can no longer be seen and crisp renders take their place. Then they can be stitched back together, with the audio, into an improved version of the video.

I had initially hoped to be able to do this by just looking for frames when the slide appears to have changed and to then match those change times to the original slides. Unfortunately, in the talk, I jump back and then forward again a few times in the video and so something that simple will not work - I need to compare the video images to the original slides and try to match them up and this should be simpler when the slides shown in the video are manipulated back into rectangles; trying to "undo" the visual effects that perspective has had on them.

This doesn't seem to be a common problem with a NuGet-installable-library solution in the .NET world (unless my Googling failed me) and it also doesn't appear to be explained in a particularly straight forward manner anywhere. The correct way to do it is to find the "vanishing point" where the lines from the top and bottom edges of the slides in the videos would meet and to determine the angle that the wall must be facing in order for the perspective shown to be observed. This requires maths and then more will be required to transform the images from the video back into rectangle - I started reading up on this and felt like I'd forgotten all of the required maths skills to do this correctly and so I wondered if I could get good enough results from the "naive" approach of trying to stretch out the area of the perspective-affected image into a rectangle.

If it isn't sufficient then I'll have to dust off the old maths! But if it *is* sufficient then that would be very handy. And, either way, perhaps this will be useful to anyone doing similar and can deal with the approximation of "perspective correction" that this code will produce.

![Example](README%20Image.jpg)

![Second (blurrier) example](README%20Image%202.jpg)
