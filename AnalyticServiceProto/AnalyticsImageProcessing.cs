using AForge.Imaging; 
using AForge.Imaging.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnalyticServiceProto
{
    class AnalyticsImageProcessing
    {

        /// Image Process

        // background and previous frames
        private UnmanagedImage backgroundFrame = null;
        private UnmanagedImage currentFrame = null;
        private UnmanagedImage motionObjectsImage = null;


        // filters used to do image processing
        private Difference differenceFilter = new Difference();
        private Threshold thresholdFilter = new Threshold(25);
        private Opening openingFilter = new Opening();
        private int backgoundMantainance = 10;

        public Bitmap ProcessImage(Bitmap image)
        {

            // save image dimension
            int width = image.Width;
            int height = image.Height;

            if (backgoundMantainance++ == 10)
            {
                backgroundFrame = null;
                backgoundMantainance = 0;
            }
            if (backgroundFrame == null)
            {
                

                // create initial backgroung image
                BitmapData bitmapDataBackGround = image.LockBits(
                            new System.Drawing.Rectangle(0, 0, width, height),
                            ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                // apply grayscale filter getting unmanaged image
                backgroundFrame = Grayscale.CommonAlgorithms.BT709.Apply(new UnmanagedImage(bitmapDataBackGround));
                // unlock source image
                image.UnlockBits(bitmapDataBackGround);
            }

            // preallocate some images
            currentFrame = UnmanagedImage.Create(width, height, PixelFormat.Format8bppIndexed);
            motionObjectsImage = UnmanagedImage.Create(width, height, PixelFormat.Format8bppIndexed);

            // lock source image
            BitmapData bitmapData = image.LockBits(
                    new System.Drawing.Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            // apply the grayscale filter
            Grayscale.CommonAlgorithms.BT709.Apply(new UnmanagedImage(bitmapData), currentFrame);

            // unlock source image
            image.UnlockBits(bitmapData);


            // set backgroud frame as an overlay for difference filter
            differenceFilter.UnmanagedOverlayImage = backgroundFrame;

            // // apply difference filter
            differenceFilter.Apply(currentFrame, motionObjectsImage);

            // // apply threshold filter
            thresholdFilter.ApplyInPlace(motionObjectsImage);

            // // apply opening filter to remove noise
            openingFilter.ApplyInPlace(motionObjectsImage);




            return motionObjectsImage.ToManagedImage();
        }



        public Blob[] GetBlobs(Bitmap image, BlobCounter blobCounter)
        {

            // process blobs
            blobCounter.FilterBlobs = true;
            blobCounter.MaxHeight = 100;
            blobCounter.MaxWidth = 100;

            blobCounter.ProcessImage(image);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            blobCounter.ObjectsOrder = ObjectsOrder.Size;

            return blobs;
        }


       

    }
}
