using AForge.Imaging;
using AForge.Imaging.Filters;
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Analytics
{
    public class AnalyticsProcess
    {



        // background and previous frames
        private UnmanagedImage backgroundFrame = null;
        private UnmanagedImage currentFrame = null;
        private UnmanagedImage motionObjectsImage = null;

        // filters used to do image processing
        private Difference differenceFilter = new Difference();
        private Threshold thresholdFilter = new Threshold(25);
        private Opening openingFilter = new Opening();


        private BlobCounter blobCounter = new BlobCounter();
        private System.Drawing.Imaging.BitmapData bitmapData;

        private MoveTowards moveTowardsFilter = new MoveTowards();
        private int width, height, frameSize;




        public Bitmap ProcessImage(Bitmap image)
        {


            if (backgroundFrame == null)
            {
                // save image dimension
                width = image.Width;
                height = image.Height;
                frameSize = width * height;

                // create initial backgroung image
                bitmapData = image.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                // apply grayscale filter getting unmanaged image
                backgroundFrame = Grayscale.CommonAlgorithms.BT709.Apply(new UnmanagedImage(bitmapData));
                // unlock source image
                image.UnlockBits(bitmapData);
            }

            // preallocate some images
            currentFrame = UnmanagedImage.Create(width, height, PixelFormat.Format8bppIndexed);
            //  previousFrame = UnmanagedImage.Create(width, height, PixelFormat.Format8bppIndexed);
            motionObjectsImage = UnmanagedImage.Create(width, height, PixelFormat.Format8bppIndexed);
            //   betweenFramesMotion = UnmanagedImage.Create(width, height, PixelFormat.Format8bppIndexed);



            // lock source image
            bitmapData = image.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            // // apply the grayscale filter
            Grayscale.CommonAlgorithms.BT709.Apply(new UnmanagedImage(bitmapData), currentFrame);

            // // unlock source image
            image.UnlockBits(bitmapData);


            // // set backgroud frame as an overlay for difference filter
            differenceFilter.UnmanagedOverlayImage = backgroundFrame;

            // // apply difference filter
            differenceFilter.Apply(currentFrame, motionObjectsImage);

            // // apply threshold filter
            thresholdFilter.ApplyInPlace(motionObjectsImage);

            // // apply opening filter to remove noise
            openingFilter.ApplyInPlace(motionObjectsImage);


            // process blobs
            blobCounter.ProcessImage(motionObjectsImage);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            blobCounter.ObjectsOrder = ObjectsOrder.Size;

            int maxSize = 0;
            Blob maxObject = new Blob(0, new Rectangle(0, 0, 0, 0));


            //try
            //{
            //    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[0], false);
            //    blobviewer.Source = ConverBitmapToBitmapImage(blobs[0].Image.ToManagedImage());

            //    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[1], false);
            //    blobviewer2.Source = ConverBitmapToBitmapImage(blobs[1].Image.ToManagedImage());

            //    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[2], false);
            //    blobviewer3.Source = ConverBitmapToBitmapImage(blobs[2].Image.ToManagedImage());

            //    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[3], false);
            //    blobviewer4.Source = ConverBitmapToBitmapImage(blobs[3].Image.ToManagedImage());

            //    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[4], false);
            //    blobviewer5.Source = ConverBitmapToBitmapImage(blobs[4].Image.ToManagedImage());

            //}
            //catch (Exception r)
            //{

            //    ShowError(r.Message);
            //}



            //    Bitmap blobImage = maxObject.Image.ToManagedImage();



            //// find biggest blob
            //if (blobs != null)
            //{
            //    foreach (Blob blob in blobs)
            //    {
            //        int blobSize = blob.Rectangle.Width * blob.Rectangle.Height;

            //        if (blobSize > maxSize)
            //        {
            //            maxSize = blobSize;
            //            maxObject = blob;
            //        }
            //    }
            //}

            //try
            //{
            //    blobCounter.ExtractBlobsImage(motionObjectsImage, maxObject, false);
            //    Bitmap blobImage = maxObject.Image.ToManagedImage();
            //    blobviewer.Source = ConverBitmapToBitmapImage(blobImage);

            //}
            //catch (Exception r)
            //{

            //    ShowError(r.Message);
            //  //  throw;
            //}


            //// get objects' information (blobs without image)
            //// process blobs
            //foreach (Blob blob in blobs)
            //{
            //    // check blob's properties
            //    if (blob.Rectangle.Width > 50)
            //    {
            //        // the blob looks interesting, let's extract it
            //        blobCounter.ExtractBlobsImage(motionObjectsImage, blob, false);
            //        Bitmap blobImage = blob.Image.ToManagedImage();
            //        blobviewer.Source = ConverBitmapToBitmapImage(blobImage);
            //    }
            //}


            return motionObjectsImage.ToManagedImage();


        }
    }
}