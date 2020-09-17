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

        public Bitmap diff(Bitmap frame, Bitmap background)
        {

            // create filter


            ThresholdedDifference filter = new ThresholdedDifference(60);
            // apply the filter
            filter.OverlayImage = background;
            return filter.Apply(frame);


        }

        public Blob[] GetBlobs(Bitmap image, BlobCounter blobCounter)
        {

            // process blobs
            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = 10;
            blobCounter.MinWidth = 10;
            blobCounter.MaxHeight = 250;
            blobCounter.MaxWidth = 250;

            blobCounter.ProcessImage(image);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            blobCounter.ObjectsOrder = ObjectsOrder.XY;

            return blobs;
        }







        internal void resetBackground()
        {
            lastFrames = new Bitmap[no];
            tempMatrix = null;
            full = false; 
            i = 0;
        }


        int i = 0;
        int[,][] tempMatrix = null;
        static int no = 240;
        Bitmap[] lastFrames = new Bitmap[no];
        bool full = false;

        internal Bitmap GetBackGound(Bitmap newBitmap)
        {

            if (!full)
            {
                lastFrames[i++] = newBitmap;
            }

            if (i == no) full = true;

            if (full && tempMatrix == null)
            {
                tempMatrix = new int[newBitmap.Width, newBitmap.Height][];

                for (int row = 0; row < tempMatrix.GetLength(0); row++)
                {
                    for (int col = 0; col < tempMatrix.GetLength(1); col++)
                    {
                        tempMatrix[row, col] = new int[no];
                    }
                }

                for (int t = 0; t < no; t++)
                {
                    int[,] matrix = GetMatrix(UnmanagedImage.FromManagedImage(lastFrames[t]));

                    for (int row = 0; row < matrix.GetLength(0); row++)
                    {
                        for (int col = 0; col < matrix.GetLength(1); col++)
                        {
                            tempMatrix[row, col][t] = matrix[row, col];
                        }
                    }
                }
            }

            if (tempMatrix != null)
            {
                int[,] medianMatrix = new int[tempMatrix.GetLength(0), tempMatrix.GetLength(1)];
                for (int row = 0; row < tempMatrix.GetLength(0); row++)
                {
                    for (int col = 0; col < tempMatrix.GetLength(1); col++)
                    {
                        medianMatrix[row, col] = GetMedian(tempMatrix[row, col]);
                    }
                }
                tempMatrix = null;
                return DrawGrayScaleMatrix(medianMatrix);
            }

            return null;
        }

        public static int GetMedian(int[] sourceNumbers)
        {
            //Framework 2.0 version of this method. there is an easier way in F4        
            if (sourceNumbers == null || sourceNumbers.Length == 0)
                throw new System.Exception("Median of empty array not defined.");

            //make sure the list is sorted, but use a new array
            int[] sortedPNumbers = (int[])sourceNumbers.Clone();
            Array.Sort(sortedPNumbers);

            //get the median
            int size = sortedPNumbers.Length;
            int mid = size / 2;
            int median = (size % 2 != 0) ? (int)sortedPNumbers[mid] : ((int)sortedPNumbers[mid] + (int)sortedPNumbers[mid - 1]) / 2;
            return median;
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
