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





        public Bitmap diff(Bitmap frame, Bitmap background)
        {
            UnmanagedImage unmanagedframe = UnmanagedImage.FromManagedImage(frame);
            UnmanagedImage unmanagedbackground = UnmanagedImage.FromManagedImage(background);

            differenceFilter.UnmanagedOverlayImage = unmanagedbackground;
            // // apply difference filter
            return differenceFilter.Apply(unmanagedframe).ToManagedImage();
        }

        public Blob[] GetBlobs(Bitmap image, BlobCounter blobCounter)
        {

            // process blobs
            blobCounter.FilterBlobs = true;
            blobCounter.MaxHeight = 150;
            blobCounter.MaxWidth = 150;

            blobCounter.ProcessImage(image);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            blobCounter.ObjectsOrder = ObjectsOrder.Size;

            return blobs;
        }


        static int no = 120;
        Bitmap[] lastFrames = new Bitmap[no];
        int contador = 0;
        bool full = false;
        int[,] matrixAvg = null;

        int i = 0;
        internal Bitmap GetBackGound(Bitmap newBitmap)
        {
              if (contador++ == 240)
              {
                  lastFrames = new Bitmap[no];
                  full = false; matrixAvg = null;
                  contador = 0;
                  i = 0;
              }
            
            if (!full)
            {
                lastFrames[i++] = newBitmap;
            }

            if (i == no) full = true;


            if (full && matrixAvg == null)
            {
                UnmanagedImage unmanagedImage = UnmanagedImage.FromManagedImage(lastFrames[0]);
                matrixAvg = GetMatrix(unmanagedImage);








                for (int t = 1; t < no; t++)
                {

                    unmanagedImage = UnmanagedImage.FromManagedImage(lastFrames[t]);
                    int[,] matrix = GetMatrix(unmanagedImage);

                    for (int row = 0; row < matrix.GetLength(0); row++)
                    {
                        for (int col = 0; col < matrix.GetLength(1); col++)
                        {


                            matrixAvg[row, col] = (matrixAvg[row, col] + matrix[row, col]);
                        }
                    }
                }


                for (int row = 0; row < matrixAvg.GetLength(0); row++)
                {
                    for (int col = 0; col < matrixAvg.GetLength(1); col++)
                    {


                        matrixAvg[row, col] = matrixAvg[row, col] / no;
                    }
                }

            }

            if (matrixAvg != null)
            {
                return DrawGrayScaleMatrix(matrixAvg);
            }



            return null;
        }





        public Bitmap DrawGrayScaleMatrix(int[,] matrix)
        {
            int width = matrix.GetLength(0);
            int height = matrix.GetLength(1);

            Bitmap bm = new Bitmap(width, height);
            using (Graphics gr = Graphics.FromImage(bm))
            {

                {
                    for (int x = 1; x < width; x += 1)
                    {
                        for (int y = 1; y < height; y += 1)
                        {
                            Brush myBrush = new System.Drawing.SolidBrush(Color.FromArgb(matrix[x, y], matrix[x, y], matrix[x, y]));
                            gr.FillRectangle(myBrush, x, y, 1, 1);
                        }
                    }
                }
            }


            return bm;
        }




        public int[,] GetMatrix(UnmanagedImage unmanagedImage)
        {

            int[,] matrix = new int[unmanagedImage.Width, unmanagedImage.Height];
            for (int x = 0; x < unmanagedImage.Width; x++)
            {
                for (int y = 0; y < unmanagedImage.Height; y++)
                {
                    matrix[x, y] = (int)unmanagedImage.GetPixel(x, y).R;
                }
            }
            return matrix;
        }


































    }
}
