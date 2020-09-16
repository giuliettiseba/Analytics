using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Vision.Motion;
using Microsoft.ML;
using OnnxObjectDetection;
using SharpGL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using VideoOS.Platform;
using VideoOS.Platform.Data;
using VideoOS.Platform.Live;
using VideoOS.Platform.Messaging;
using VideoOS.Platform.Metadata;
using VideoOS.Platform.Util.AdaptiveStreaming;
using Brushes = System.Drawing.Brushes;
using Color = System.Drawing.Color;
using FontFamily = System.Drawing.FontFamily;
using Pen = System.Drawing.Pen;
using Vector = VideoOS.Platform.Metadata.Vector;

namespace AnalyticServiceProto
{
    public partial class HeatMapPluginService : Form
    {

        ///  LiveView
        private JPEGLiveSource _jpegLiveSource;



        /// HeatMap
        private Bitmap referenceBitmap;
        private Bitmap bitmapHeatMap;

        private Dictionary<Vector, KeyValuePair<int, DateTime>> heatmapData;
        private const int clusterSize = 5;
        private const int gridSize = 5;
        private const int yellowThreshold = 5;
        private const int redThreshold = 10;
        private const int circleSize = 15;



        /// ML
        private OnnxOutputParser outputParser;
        private PredictionEngine<ImageInputData, TinyYoloPrediction> tinyYoloPredictionEngine;
        private PredictionEngine<ImageInputData, CustomVisionPrediction> customVisionPredictionEngine;




        private BlobCounter blobCounter;


        ///  Metadata
        private int _count = 0;
        private MetadataProviderChannel _metadataProviderChannel;

        private bool OnMainThread = false;
        private AnalyticsImageProcessing analyticsImageProcessing;

        /// <summary>
        /// 
        /// </summary>
        // Communication 
        private MessageCommunication _messageCommunication;
        private MetadataHandler metadataHandler;

        private static readonly string modelsDirectory = Path.Combine(Environment.CurrentDirectory, @"ML\OnnxModels");



        private void LoadModel()
        {
            // Check for an Onnx model exported from Custom Vision
            var customVisionExport = Directory.GetFiles(modelsDirectory, "*.zip").FirstOrDefault();

            // If there is one, use it.
            if (customVisionExport != null)
            {
                var customVisionModel = new CustomVisionModel(customVisionExport);
                var modelConfigurator = new OnnxModelConfigurator(customVisionModel);

                outputParser = new OnnxOutputParser(customVisionModel);
                customVisionPredictionEngine = modelConfigurator.GetMlNetPredictionEngine<CustomVisionPrediction>();
            }
            else // Otherwise default to Tiny Yolo Onnx model
            {
                var tinyYoloModel = new TinyYoloModel(Path.Combine(modelsDirectory, "TinyYolo2_model.onnx"));
                var modelConfigurator = new OnnxModelConfigurator(tinyYoloModel);

                outputParser = new OnnxOutputParser(tinyYoloModel);
                tinyYoloPredictionEngine = modelConfigurator.GetMlNetPredictionEngine<TinyYoloPrediction>();
            }
        }


















        public HeatMapPluginService()
        {

            //load Model 
            //LoadModel();


            InitializeComponent();


            // Test Parameters 
            Item _newItem = Configuration.Instance.GetItem(new Guid("d198ae21-1aba-48fa-83d5-f0aa191439f9"), new Guid("5135ba21-f1dc-4321-806a-6ce2017343c0"));
            OpenStream(_newItem);

            blobCounter = new BlobCounter();

            // Initialize Analyc Image Process 
            analyticsImageProcessing = new AnalyticsImageProcessing();

            // Start Metadata Device service
            metadataHandler = new MetadataHandler();
            _metadataProviderChannel = metadataHandler.OpenHTTPService();

            _metadataProviderChannel.SessionOpening += MetadataProviderSessionOpening;
            _metadataProviderChannel.SessionClosed += MetadataProviderSessionClosed;

            // Start Communication Manager 
            MessageCommunicationManager.Start(EnvironmentManager.Instance.MasterSite.ServerId);
            _messageCommunication = MessageCommunicationManager.Get(EnvironmentManager.Instance.MasterSite.ServerId);

            // Create a fiter to get messages from Smart Client Plugin
            _messageCommunication.RegisterCommunicationFilter(HeatMapSearchHandler, new VideoOS.Platform.Messaging.CommunicationIdFilter("analyticsHeatMapSearch"));
        }


        private void MetadataProviderSessionOpening(MediaProviderSession session)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<MediaProviderSession>(MetadataProviderSessionOpening), session);
            }
            else
            {
                textBoxSessionCount.Text = "" + _metadataProviderChannel.ActiveSessions;
            }
        }

        void MetadataProviderSessionClosed(MediaProviderSession session)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<MediaProviderSession>(MetadataProviderSessionClosed), session);
            }
            else
            {
                textBoxSessionCount.Text = "" + _metadataProviderChannel.ActiveSessions;
            }
        }


        [Serializable]
        public class SearchData
        {
            public string Entry { get; set; }
            public string Camera { get; set; }
            public DateTime? End { get; internal set; }
            public DateTime? Initial { get; internal set; }
            public String ItemFQID { get; internal set; }
            public String ObjectID { get; internal set; }
            public String ObjectKind { get; internal set; }
        }

        private object HeatMapSearchHandler(VideoOS.Platform.Messaging.Message message, FQID destination, FQID sender)
        {
            referenceBitmap = null;
            bitmapHeatMap = null;
            heatmapData = null;

            SearchData data = (message.Data as SearchData);
            Item _newItem = Configuration.Instance.GetItem(new Guid(data.ObjectID), new Guid(data.ObjectKind));

            if (this.labelCamaraName.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    OpenStream(_newItem);
                    labelCamaraName.Text = data.Camera;
                }));
            }
            else
            {
                OpenStream(_newItem);
                labelCamaraName.Text = data.Camera;
            }
            return null;
        }

        private void SetStreamType()
        {
            if (null == _jpegLiveSource)
                return;
            _jpegLiveSource.StreamSelectionParams.StreamSelectionType = StreamSelectionType.DefaultStream;
        }

        private void OpenStream(Item _selectItem)
        {
            _jpegLiveSource = new JPEGLiveSource(_selectItem);

            int width = 800;
            int height = 600;

            try
            {
                _jpegLiveSource.Width = width;
                _jpegLiveSource.Height = height;
                _jpegLiveSource.SetWidthHeight();
                SetStreamType();
                _jpegLiveSource.LiveModeStart = true;
                _jpegLiveSource.Init();
                _jpegLiveSource.LiveContentEvent += JpegLiveSource1LiveNotificationEvent;
                textBoxCount.Text = "0";
                _count = 0;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Could not Init:" + ex.Message);
                _jpegLiveSource = null;
            }
        }


        /// <summary>
        /// This event is called when JPEG is available or some exception has occurred
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// 

        Bitmap background2 = null;
        Bitmap foreground = null;
        private void JpegLiveSource1LiveNotificationEvent(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                if (OnMainThread)
                {
                    LiveContentEventArgs args = e as LiveContentEventArgs;
                    if (args != null && args.LiveContent != null)
                    {
                        // UI thread is too busy - discard this frame from display
                        args.LiveContent.Dispose();
                    }
                    return;
                }
                OnMainThread = true;
                // Make sure we execute on the UI thread before updating UI Controls
                BeginInvoke(new EventHandler(JpegLiveSource1LiveNotificationEvent), new[] { sender, e });
            }
            else
            {
                LiveContentEventArgs args = e as LiveContentEventArgs;
                if (args != null)
                {
                    if (args.LiveContent != null)
                    {
                        // Display the received JPEG
                        //textBoxLength.Text = "" + args.LiveContent.Content.Length;

                        int width = args.LiveContent.Width;
                        int height = args.LiveContent.Height;

                        MemoryStream ms = new MemoryStream(args.LiveContent.Content);
                        //Bitmap newBitmap = testBox();
                        Bitmap newBitmap = new Bitmap(ms);

                        if (referenceBitmap == null)
                            referenceBitmap = newBitmap;

                        textBoxResolution.Text = "" + width + "x" + height;

                        if (pictureBoxOriginal.Size.Width != 0 && pictureBoxOriginal.Size.Height != 0)
                        {
                            if ((newBitmap.Width != pictureBoxOriginal.Width || newBitmap.Height != pictureBoxOriginal.Height))
                            {
                                pictureBoxOriginal.Image = new Bitmap(newBitmap, pictureBoxOriginal.Size);
                            }
                            else
                            {
                                pictureBoxOriginal.Image = newBitmap;
                            }
                        }

                        textBoxDecodingStatus.Text = args.LiveContent.HardwareDecodingStatus;



                        textBoxRx.Text = rx.ToString();
                        textBoxRy.Text = ry.ToString();
                        textBoxRz.Text = rz.ToString();

                        textBoxPx.Text = px.ToString();
                        textBoxPy.Text = py.ToString();
                        textBoxPz.Text = pz.ToString();

                        textBoxStep.Text = step.ToString();

                        ms.Close();
                        ms.Dispose();

                        _count++;
                        textBoxCount.Text = "" + _count;

                        args.LiveContent.Dispose();

                        Grayscale gfilter = new Grayscale(0.2125, 0.7154, 0.0721);
                        Bitmap grayImage = gfilter.Apply(newBitmap);
                        pictureBoxGray.Image = grayImage;

                        try
                        {

                            if (background == null)
                            {
                                background = analyticsImageProcessing.GetBackGound(grayImage);


                                Bitmap bitmap = new Bitmap(320, 240);
                                Graphics g = Graphics.FromImage(bitmap);
                                g.FillRectangle(System.Drawing.Brushes.Black, 0, 0, bitmap.Width, bitmap.Height);
                                g.DrawString("Processing Background...", new Font(FontFamily.GenericMonospace, 12), Brushes.White, new PointF(20, pictureBoxOriginal.Height / 2 - 20));
                                g.Dispose();
                                pictureBoxBackgound.Image = new Bitmap(bitmap, pictureBoxOriginal.Size);
                                bitmap.Dispose();


                            }

                            else if (background2 == null)
                            {
                                unmanagedImage = UnmanagedImage.FromManagedImage(background);
                                background2 = gfilter.Apply(background);
                                pictureBoxBackgound.Image = background;
                            }
                            else
                            {

                                foreground = analyticsImageProcessing.diff(grayImage, background2);

                                Threshold filter = new Threshold(100);
                                // apply the filter
                                filter.ApplyInPlace(foreground);

                                // create filter

                             //   short[,] se = { { 5, 5 } }; 
                                Dilatation dfilter = new Dilatation();
                                // apply the filter

                                dfilter.ApplyInPlace(foreground);
                                dfilter.ApplyInPlace(foreground);
                                dfilter.ApplyInPlace(foreground);
                                dfilter.ApplyInPlace(foreground);
                                dfilter.ApplyInPlace(foreground);

                                pictureBoxProcessed.Image = foreground;



                            }



                            /// PocessImage
                            if (foreground != null) { 
                            Blob[] blobs = analyticsImageProcessing.GetBlobs(foreground, blobCounter);
                            textBoxMetadata.Text = metadataHandler.SendMetadataBox(blobs, _jpegLiveSource.Width, _jpegLiveSource.Height);
                            PaintHeatMap(blobs);
                                pictureBoxHeatmap.Image = bitmapHeatMap;


                            /// Debug tool


                                if (blobs[0] != null)
                            {
                                blobCounter.ExtractBlobsImage(foreground, blobs[0], false);
                                pictureBoxBlob1.Image = blobs[0].Image.ToManagedImage();
                                textBoxAreaBlob1.Text = blobs[0].Area.ToString();
                                textBoxXBlob1.Text = blobs[0].CenterOfGravity.X.ToString();
                                textBoxYBlob1.Text = blobs[0].CenterOfGravity.Y.ToString();
                            }

                            if (blobs[1] != null)
                            {
                                blobCounter.ExtractBlobsImage(foreground, blobs[1], false);
                                pictureBoxBlob2.Image = blobs[1].Image.ToManagedImage();
                                textBoxAreaBlob2.Text = blobs[1].Area.ToString();
                                textBoxXBlob2.Text = blobs[1].CenterOfGravity.X.ToString();
                                textBoxYBlob2.Text = blobs[1].CenterOfGravity.Y.ToString();

                            }

                            if (blobs[2] != null)
                            {
                                blobCounter.ExtractBlobsImage(foreground, blobs[2], false);
                                pictureBoxBlob3.Image = blobs[2].Image.ToManagedImage();
                                textBoxAreaBlob3.Text = blobs[2].Area.ToString();
                                textBoxXBlob3.Text = blobs[2].CenterOfGravity.X.ToString();
                                textBoxYBlob3.Text = blobs[2].CenterOfGravity.Y.ToString();

                            }

                            if (blobs[3] != null)
                            {
                                blobCounter.ExtractBlobsImage(foreground, blobs[3], false);
                                pictureBoxBlob4.Image = blobs[3].Image.ToManagedImage();
                                textBoxAreaBlob4.Text = blobs[3].Area.ToString();
                                textBoxXBlob4.Text = blobs[3].CenterOfGravity.X.ToString();
                                textBoxYBlob4.Text = blobs[3].CenterOfGravity.Y.ToString();
                            }
                            }
                        }
                        catch (Exception r)
                        {

                            Console.WriteLine(r.Message);
                        }


                    }
                    else if (args.Exception != null)
                    {
                        // Handle any exceptions occurred inside toolkit or on the communication to the VMS

                        Bitmap bitmap = new Bitmap(320, 240);
                        Graphics g = Graphics.FromImage(bitmap);
                        g.FillRectangle(System.Drawing.Brushes.Black, 0, 0, bitmap.Width, bitmap.Height);
                        g.DrawString("Connection lost to server ...", new Font(FontFamily.GenericMonospace, 12), Brushes.White, new PointF(20, pictureBoxOriginal.Height / 2 - 20));
                        g.Dispose();
                        pictureBoxOriginal.Image = new Bitmap(bitmap, pictureBoxOriginal.Size);
                        bitmap.Dispose();
                    }

                }
                OnMainThread = false;
            }
        }




        private void DrawBoxes(Bitmap bitmap, System.Drawing.Rectangle[] boxes)
        {
            //WebCamCanvas.Children.Clear();

            //    pictureBoxProcessed.Image = bitmap;

            System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);

            if (boxes != null)
                foreach (System.Drawing.Rectangle box in boxes)
                {

                    graphics.DrawRectangle(System.Drawing.Pens.Blue, box);


                }
            pictureBoxGray.Image = bitmap;
        }

        UnmanagedImage unmanagedImage;
        int[,] matrix;
        private void GetBitmapColorMatix(Bitmap b1)
        {

            // create grayscale filter(BT709)
            Grayscale filter = new Grayscale(0.2125, 0.7154, 0.0721);
            // apply the filter
            System.Drawing.Bitmap grayImage = filter.Apply(b1);

            pictureBoxGray.Image = grayImage;
            unmanagedImage = UnmanagedImage.FromManagedImage(grayImage);


            matrix = new int[unmanagedImage.Width, unmanagedImage.Height];
            for (int x = 0; x < unmanagedImage.Width; x++)
            {

                for (int y = 0; y < unmanagedImage.Height; y++)
                {
                    matrix[x, y] = (int)unmanagedImage.GetPixel(x, y).R;
                }
            }

        }

        private void ParseFrame(Bitmap bitmap)
        {
            if (customVisionPredictionEngine == null && tinyYoloPredictionEngine == null)
                return;

            var frame = new ImageInputData { Image = bitmap };
            var filteredBoxes = DetectObjectsUsingModel(frame);

            DrawOverlays(bitmap, filteredBoxes, pictureBoxGray.Height, pictureBoxGray.Width);

        }




        private void DrawOverlays(Bitmap bitmap, List<OnnxObjectDetection.BoundingBox> filteredBoxes, double originalHeight, double originalWidth)
        {
            //WebCamCanvas.Children.Clear();

            //    pictureBoxProcessed.Image = bitmap;

            System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);


            foreach (var box in filteredBoxes)
            {
                // process output boxes
                double x = Math.Max(box.Dimensions.X, 0);
                double y = Math.Max(box.Dimensions.Y, 0);
                double width = Math.Min(originalWidth - x, box.Dimensions.Width);
                double height = Math.Min(originalHeight - y, box.Dimensions.Height);

                // fit to current image size
                x = originalWidth * x / ImageSettings.imageWidth;
                y = originalHeight * y / ImageSettings.imageHeight;
                width = originalWidth * width / ImageSettings.imageWidth;
                height = originalHeight * height / ImageSettings.imageHeight;

                graphics.DrawRectangle(System.Drawing.Pens.Blue, 0, 0, (float)width, (float)height);


            }
            pictureBoxGray.Image = bitmap;
        }


        public List<OnnxObjectDetection.BoundingBox> DetectObjectsUsingModel(ImageInputData imageInputData)
        {
            var labels = customVisionPredictionEngine?.Predict(imageInputData).PredictedLabels ?? tinyYoloPredictionEngine?.Predict(imageInputData).PredictedLabels;
            var boundingBoxes = outputParser.ParseOutputs(labels);
            var filteredBoxes = outputParser.FilterBoundingBoxes(boundingBoxes, 5, 0.5f);
            return filteredBoxes;
        }




        private void PaintHeatMap(Blob[] blobs)
        {


            if (bitmapHeatMap == null && referenceBitmap != null) bitmapHeatMap = new Bitmap(referenceBitmap);
            if (heatmapData == null) heatmapData = new Dictionary<Vector, KeyValuePair<int, DateTime>>();
            try
            {
                foreach (Blob blob in blobs)
                    AddHeatMapValue((int)blob.CenterOfGravity.X, (int)blob.CenterOfGravity.Y, DateTime.Now);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }


        private void AddHeatMapValue(int x, int y, DateTime timeStamp)
        {
            // get box middle
            x -= (x % gridSize);
            y -= (y % gridSize);

            Vector vector = new Vector() { X = x, Y = y };
            int maxNearValue = 0;
            Vector maxVector = vector;

            /// Clustering
            /// 

            for (int j = -(clusterSize / 2); j < (clusterSize / 2); j++)
                for (int i = -(clusterSize / 2); i < (clusterSize / 2); i++)
                {
                    Vector tmpVector = new Vector() { X = x + i, Y = y + j };

                    if (heatmapData.TryGetValue(tmpVector, out KeyValuePair<int, DateTime> tempValue))
                        if (tempValue.Key > maxNearValue)
                        {
                            maxNearValue = tempValue.Key;
                            maxVector = tmpVector;
                        };
                }

            // Time filter  
            if (!heatmapData.ContainsKey(maxVector) || (timeStamp - heatmapData[maxVector].Value).TotalSeconds > 20)
            {
                // Write new value
                heatmapData[maxVector] = new KeyValuePair<int, DateTime>(maxNearValue + 1, timeStamp);

                // Paint 
                Graphics heatmapGraphics = Graphics.FromImage(bitmapHeatMap);
                Color color;
                if (heatmapData[maxVector].Key < yellowThreshold)
                    color = System.Drawing.Color.Blue;
                else
                    if (heatmapData[maxVector].Key < redThreshold) color = System.Drawing.Color.Yellow;
                else
                    color = System.Drawing.Color.Red;
                //Paint Aux Method
                FillEllipseDifusse(heatmapGraphics, color, (int)maxVector.X, (int)maxVector.Y);
            }
            //  pictureBoxHeatMap.Image = bitmapHeatMap;
        }

        private void FillEllipseDifusse(Graphics heatmapGraphics, Color color, int x, int y)
        {
            for (int i = 0; i < circleSize; i += 2)
            {
                int xx = x - i / 2;
                int yy = y - i / 2;
                Pen pen = new Pen(
                    Color.FromArgb(255 - ((i * 255) / circleSize), color)
                    );
                heatmapGraphics.DrawEllipse(pen, xx, yy, i, i);
            }
        }


        private void ButtonExport_Click(object sender, EventArgs e)
        {
            DictToCsv(heatmapData, @"C:\Temp\heatMap.cvs");
        }

        public static void DictToCsv(Dictionary<Vector, KeyValuePair<int, DateTime>> dict, string filePath)
        {
            try
            {
                var csvLines = String.Join(Environment.NewLine,
                       dict.Select(d => d.Key.X + "," + d.Key.Y + "," + d.Value.Key + "," + d.Value));
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, csvLines);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void SendHeatMap_Button_Click(object sender, EventArgs e)
        {
            //  Bitmap data = (Bitmap)pictureBoxHeatMap.Image;
            //   _messageCommunication.TransmitMessage(new VideoOS.Platform.Messaging.Message("heatmapPic", data), null, null, null);
        }


        int step = 1;

        float rx = 119;
        float ry = 1;
        float rz = 21;

        float px = -1.2f;
        float py = 0.5f;
        float pz = -5.6f;



        OpenGL gl;
        private Bitmap background;

        private void draw3d(int width, int height, int[,] matrix)
        {
            //  Get the OpenGL object, just to clean up the code.

            gl = this.openGLControl1.OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);  // Clear The Screen And The Depth Buffer
            gl.LoadIdentity();                  // Reset The View


            gl.Translate(px, py, pz);               // Move Left And Into The Screen
            gl.Rotate(rx, ry, rz);
            //  gl.Begin(OpenGL.GL_LINES);               
            gl.Begin(OpenGL.GL_POINTS);
            //gl.Begin(OpenGL.GL_TRIANGLES);              
            // gl.Begin(OpenGL.GL_QUADS);


            for (int x = 0; x < width; x += step)
            {
                for (int y = 0; y < height; y += step)
                {
                    float inte = matrix[x, y] / 255f;
                    gl.Color(inte, inte, inte);

                    //     gl.Vertex(x / 200f, y / 200f, 0);
                    gl.Vertex(x / 200f, y / 200f, -inte);

                }
            }
            gl.End();

            gl.Flush();


        }
        private void redraw()
        {
            draw3d(unmanagedImage.Width, unmanagedImage.Height, matrix);

        }


        private void button9_Click(object sender, EventArgs e)
        {
            rx--;
            redraw();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            ry--;
            redraw();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            rz--;
            redraw();
        }

        private void HeatMapPluginService_Load(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            rx++;
            redraw();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            ry++;
            redraw();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            rz++;
            redraw();
        }

        private void button11_Click(object sender, EventArgs e)
        {
            px -= 0.2f;
        }

        private void button10_Click(object sender, EventArgs e)
        {
            py -= 0.2f;
            redraw();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            pz -= 0.2f;
            redraw();
        }

        private void button13_Click(object sender, EventArgs e)
        {
            px += 0.2f;
            redraw();
        }

        private void button12_Click(object sender, EventArgs e)
        {
            py += 0.2f;
            redraw();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            pz += 0.2f;
            redraw();
        }

        private void button14_Click(object sender, EventArgs e)
        {
            step++;
            redraw();
        }

        private void button15_Click(object sender, EventArgs e)
        {
            if (step > 1) step--;
            redraw();
        }
    }

    internal static class ColorExtensions
    {
        internal static System.Windows.Media.Color ToMediaColor(this System.Drawing.Color drawingColor)
        {
            return System.Windows.Media.Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        }
    }

}