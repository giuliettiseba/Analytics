using AForge.Imaging;
using AForge.Imaging.Filters;
using Analytics;
using Analytics.Background;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoOS.Platform;
using VideoOS.Platform.Data;
using VideoOS.Platform.Live;
using VideoOS.Platform.Messaging;
using VideoOS.Platform.Metadata;
using VideoOS.Platform.Util.AdaptiveStreaming;

namespace AnalyticServiceProto
{
    public partial class Main : Form
    {



        ///  LiveView

        private Item _selectItem;
        private JPEGLiveSource _jpegLiveSource;
        private DateTime _currentShownTime;


        /// HTTP Server
        private MediaProviderService _metadataProviderService;
        private MetadataProviderChannel _metadataProviderChannel;
        private readonly MetadataSerializer _metadataSerializer = new MetadataSerializer();



        ///  Metadata

        private readonly TimeSpan _timeBetweenMetadata = TimeSpan.FromSeconds(0.5);

        private CancellationTokenSource _cts;
        private Task _senderTask;


        private int _count = 0;
        /// HeatMap





        /// <summary>
        /// 
        /// </summary>
        // Communication 
        private MessageCommunication _messageCommunication;
        private object _heatmapSearchFilter;

        public Main()
        {
            InitializeComponent();

            // Test Camera comment on full demo
            Item _newItem = Configuration.Instance.GetItem(new Guid("d198ae21-1aba-48fa-83d5-f0aa191439f9"), new Guid("5135ba21-f1dc-4321-806a-6ce2017343c0"));
            OpenStream(_newItem);


            // Start Metadata Device service
            OpenHTTPService();


            // Start Communication Manager 
            MessageCommunicationManager.Start(EnvironmentManager.Instance.MasterSite.ServerId);
            _messageCommunication = MessageCommunicationManager.Get(EnvironmentManager.Instance.MasterSite.ServerId);

            // Create a fiter to get messages from Smart Client Plugin
            _heatmapSearchFilter = _messageCommunication.RegisterCommunicationFilter(HeatMapSearchHandler, new VideoOS.Platform.Messaging.CommunicationIdFilter(AnalyticsDefinition.analyticsHeatMapSearchFilterID));
        }

        private void StartHeatMap()
        {
            bitmapHeatMap = new Bitmap(referenceBitmap);
        }

        private object HeatMapSearchHandler(VideoOS.Platform.Messaging.Message message, FQID destination, FQID sender)
        {
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

        private void SetStreamType(int width, int height)
        {
            if (null == _jpegLiveSource)
                return;

            _jpegLiveSource.StreamSelectionParams.StreamSelectionType = StreamSelectionType.DefaultStream;

        }


        private void OpenStream(Item _selectItem)
        {

            _currentShownTime = new DateTime(2020, 06, 16, 20, 23, 27, 685);

            textBoxFQID.Text = _selectItem.FQID.ToString();
            textBoxName.Text = _selectItem.Name.ToString();


            _jpegLiveSource = new JPEGLiveSource(_selectItem);
            int width = 800;
            int height = 600;

            try
            {


                _jpegLiveSource.Width = width;
                _jpegLiveSource.Height = height;
                _jpegLiveSource.SetWidthHeight();
                SetStreamType(width, height);
                _jpegLiveSource.LiveModeStart = true;
                checkBoxAspect.Enabled = false;

                //       _jpegLiveSource.Width = pictureBoxOriginal.Width;
                //        _jpegLiveSource.Height = pictureBoxOriginal.Height;

                //       SetStreamType(pictureBoxOriginal.Width, pictureBoxOriginal.Height);

                _jpegLiveSource.Init();
                _jpegLiveSource.LiveContentEvent += JpegLiveSource1LiveNotificationEvent;

                textBoxCount.Text = "0";
                _count = 0;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not Init:" + ex.Message);
                _jpegLiveSource = null;
            }
        }

        Bitmap referenceBitmap;

        private bool OnMainThread = false;
        /// <summary>
        /// This event is called when JPEG is available or some exception has occurred
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void JpegLiveSource1LiveNotificationEvent(object sender, EventArgs e)
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
                        textBoxLength.Text = "" + args.LiveContent.Content.Length;

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
                            if (!checkBoxAspect.Checked && (newBitmap.Width != pictureBoxOriginal.Width || newBitmap.Height != pictureBoxOriginal.Height))
                            {
                                pictureBoxOriginal.Image = new Bitmap(newBitmap, pictureBoxOriginal.Size);
                            }
                            else
                            {
                                pictureBoxOriginal.Image = newBitmap;
                            }
                        }


                        if (args.LiveContent.CroppingDefined)
                        {
                            textBoxCropRect.Text = "" + args.LiveContent.CropWidth + "x" + args.LiveContent.CropHeight;
                        }
                        else
                        {
                            textBoxCropRect.Text = "--";
                        }
                        textBoxDecodingStatus.Text = args.LiveContent.HardwareDecodingStatus;

                        ms.Close();
                        ms.Dispose();

                        _count++;
                        textBoxCount.Text = "" + _count;

                        args.LiveContent.Dispose();


                        /// PocessImage
                        //                       pictureBoxProcessed.Image = ProcessImage(testBox());
                        pictureBoxProcessed.Image = ProcessImage(newBitmap);

                    }
                    else if (args.Exception != null)
                    {
                        // Handle any exceptions occurred inside toolkit or on the communication to the VMS

                        Bitmap bitmap = new Bitmap(320, 240);
                        Graphics g = Graphics.FromImage(bitmap);
                        g.FillRectangle(Brushes.Black, 0, 0, bitmap.Width, bitmap.Height);
                        g.DrawString("Connection lost to server ...", new Font(FontFamily.GenericMonospace, 12), Brushes.White, new PointF(20, pictureBoxOriginal.Height / 2 - 20));
                        g.Dispose();
                        pictureBoxOriginal.Image = new Bitmap(bitmap, pictureBoxOriginal.Size);
                        bitmap.Dispose();
                    }

                }
                OnMainThread = false;
            }
        }



        private void OpenHTTPService()
        {

            // Open the HTTP Service
            if (_metadataProviderService == null)
            {
                var hardwareDefinition = new HardwareDefinition(
                    PhysicalAddress.Parse("001122334455"),
                    "MetadataProvider")
                {
                    Firmware = "v10",
                    MetadataDevices = { MetadataDeviceDefintion.CreateBoundingBoxDevice() }
                };

                _metadataProviderService = new MediaProviderService();
                _metadataProviderService.Init(52123, "password", hardwareDefinition);
            }
            // Create a provider to handle channel 1
            _metadataProviderChannel = _metadataProviderService.CreateMetadataProvider(1);
            _metadataProviderChannel.SessionOpening += MetadataProviderSessionOpening;
            _metadataProviderChannel.SessionClosed += MetadataProviderSessionClosed;
        }


        void MetadataProviderSessionOpening(MediaProviderSession session)
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
                    new System.Drawing.Rectangle(0, 0, width, height),
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
                    new System.Drawing.Rectangle(0, 0, width, height),
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


            //motionObjectsImage = currentFrame;



            // process blobs
            blobCounter.FilterBlobs = true;
            blobCounter.MaxHeight = 100;
            blobCounter.MaxWidth = 100;

            blobCounter.ProcessImage(motionObjectsImage);
            Blob[] blobs = blobCounter.GetObjectsInformation();
            blobCounter.ObjectsOrder = ObjectsOrder.Size;



            try
            {
                if (blobs[0] != null)
                {
                    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[0], false);
                    pictureBoxBlob1.Image = blobs[0].Image.ToManagedImage();
                    textBoxAreaBlob1.Text = blobs[0].Area.ToString();
                    textBoxXBlob1.Text = blobs[0].CenterOfGravity.X.ToString();
                    textBoxYBlob1.Text = blobs[0].CenterOfGravity.Y.ToString();


                }

                if (blobs[1] != null)
                {
                    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[1], false);
                    pictureBoxBlob2.Image = blobs[1].Image.ToManagedImage();
                    textBoxAreaBlob2.Text = blobs[1].Area.ToString();
                    textBoxXBlob2.Text = blobs[1].CenterOfGravity.X.ToString();
                    textBoxYBlob2.Text = blobs[1].CenterOfGravity.Y.ToString();

                }

                if (blobs[2] != null)
                {
                    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[2], false);
                    pictureBoxBlob3.Image = blobs[2].Image.ToManagedImage();
                    textBoxAreaBlob3.Text = blobs[2].Area.ToString();
                    textBoxXBlob3.Text = blobs[2].CenterOfGravity.X.ToString();
                    textBoxYBlob3.Text = blobs[2].CenterOfGravity.Y.ToString();

                }

                if (blobs[3] != null)
                {
                    blobCounter.ExtractBlobsImage(motionObjectsImage, blobs[3], false);
                    pictureBoxBlob4.Image = blobs[3].Image.ToManagedImage();
                    textBoxAreaBlob4.Text = blobs[3].Area.ToString();
                    textBoxXBlob4.Text = blobs[3].CenterOfGravity.X.ToString();
                    textBoxYBlob4.Text = blobs[3].CenterOfGravity.Y.ToString();
                }

            }
            catch (Exception r)
            {

                Console.WriteLine(r.Message);
            }


            SendMetadataBox(blobs);
            paintHeatMap(blobs);

            return motionObjectsImage.ToManagedImage();
        }

        Bitmap bitmapHeatMap;
        Dictionary<Vector, int> heatmapData;

        private void paintHeatMap(Blob[] blobs)
        {
            if (bitmapHeatMap == null) StartHeatMap();

            if (heatmapData == null) StartHeatMapData();


            try
            {

                foreach (Blob blob in blobs)
                    addHeatMapValue((int)blob.CenterOfGravity.X, (int)blob.CenterOfGravity.Y);

            }
            catch (Exception)
            {


            }

            //pictureBoxHeatMap.Image = testBox();


        }


        const int clusterSize = 10;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>

        private void addHeatMapValue(int x, int y)
        {

            Vector vector = new Vector() { X = x, Y = y };



            int maxNearValue = 0;
            Vector maxVector = vector;
            // int value = 1;
            /// Clustering
            /// 

            for (int j = -(clusterSize / 2); j < (clusterSize / 2); j++)
                for (int i = -(clusterSize / 2); i < (clusterSize / 2); i++)
                {
                    int tempValue = 0;
                    Vector tmpVector = new Vector() { X = x + i, Y = y + j};

                    if (heatmapData.TryGetValue(tmpVector, out tempValue))
                        if (tempValue > maxNearValue)
                        {
                            maxNearValue = tempValue;

                            maxVector = tmpVector;
                        };

                }


            /*   int value = 0;
               if (heatmapData.TryGetValue(vector, out value))
                   value = value + 1;

               else value = 1;


               heatmapData[vector] = value;
            */

            heatmapData[maxVector] = maxNearValue + 1;


            Graphics heatmapGraphics = Graphics.FromImage(bitmapHeatMap);

            Brush brush;

            if (heatmapData[maxVector] < 20) brush = Brushes.Blue;
            else
                if (heatmapData[maxVector] < 50) brush = Brushes.Yellow;
            else
                brush = Brushes.Red;

            //heatmapGraphics.FillRectangle(brush, maxVector.X, maxVector.Y, 5, 5);
            heatmapGraphics.FillEllipse(brush, new RectangleF(maxVector.X, maxVector.Y, 10, 10));
            pictureBoxHeatMap.Image = bitmapHeatMap;

        }

        private void StartHeatMapData()
        {
            heatmapData = new Dictionary<Vector, int>();
        }

        float i = -50f;
        float j = -50f;

        float step = 10f;
        bool rev_i = false;
        bool rev_j = false;

        private Bitmap testBox()
        {

            if (rev_i) i = i - step;
            else
                i = i + step;
            if (rev_j) j = j - step;
            else
                j = j + step;

            int w = _jpegLiveSource.Width;
            int h = _jpegLiveSource.Height;
            Bitmap flag = new Bitmap(w, h);
            Graphics flagGraphics = Graphics.FromImage(flag);
            flagGraphics.FillRectangle(Brushes.Red, i, j, 10, 10);


            if (i > w) rev_i = true;
            if (i < 00) rev_i = false;

            if (j > h) rev_j = true;
            if (j < 00) rev_j = false;

            return flag;
        }

        Dictionary<double, double> reciprocals = new Dictionary<double, double>();

        private double Reciprocal(double val)
        {
            double reciprocal;
            if (reciprocals.TryGetValue(val, out reciprocal))
            {
                return reciprocal;
            }

            reciprocal = 1 / val;
            reciprocals[reciprocal] = val;
            return reciprocal;
        }



        private OnvifObject CreateOnvifObject(float x, float y, float area, string n, int id)
        {
            area = area / 50;
            float BoundingBoxSize = 0.01f;
            float r_x = (float)Reciprocal(width);
            float r_y = (float)Reciprocal(height);
            float r_xx = r_x * 2;
            float r_yy = r_y * 2;


            var centerOfGravity = new Vector { X = x, Y = y };

            var blob = new OnvifObject(id)
            {



                Appearance = new VideoOS.Platform.Metadata.Appearance
                {
                    Shape = new Shape
                    {
                        BoundingBox = new VideoOS.Platform.Metadata.Rectangle
                        {
                            Bottom = height - y - area / 2,
                            Left = x - area / 2,
                            Top = height - y + area / 2,
                            Right = x + area / 2
                        },
                        CenterOfGravity = centerOfGravity
                    },
                    Description = new DisplayText
                    {
                        Value = n
                    },
                    Transformation = new Transformation
                    {
                        Translate = new Vector { X = -1, Y = -1 },
                        Scale = new Vector { X = r_xx, Y = r_yy }
                    }
                }
            };


            return blob;
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            DictToCsv(heatmapData, @"C:\Temp\heatMap.cvs");
        }


        public static void DictToCsv(Dictionary<Vector, int> dict, string filePath)
        {
            try
            {
                var csvLines = String.Join(Environment.NewLine,
                       dict.Select(d => d.Key.X + "," + d.Key.Y + "," + d.Value));
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, csvLines);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void SendMetadataBox(Blob[] blobs)
        {
            try
            {
                OnvifObject blob1 = CreateOnvifObject(blobs[0].CenterOfGravity.X, blobs[0].CenterOfGravity.Y, blobs[0].Area, blobs[0].ID.ToString(), 1);
                OnvifObject blob2 = CreateOnvifObject(blobs[1].CenterOfGravity.X, blobs[1].CenterOfGravity.Y, blobs[1].Area, blobs[1].ID.ToString(), 2);
                OnvifObject blob3 = CreateOnvifObject(blobs[2].CenterOfGravity.X, blobs[2].CenterOfGravity.Y, blobs[2].Area, blobs[2].ID.ToString(), 3);
                OnvifObject blob4 = CreateOnvifObject(blobs[3].CenterOfGravity.X, blobs[3].CenterOfGravity.Y, blobs[3].Area, blobs[3].ID.ToString(), 4);


                List<OnvifObject> blobList = new List<OnvifObject>();


                MetadataStream metadata = new MetadataStream
                {
                    VideoAnalyticsItems =
                {
                    new VideoAnalytics
                    {
                        Frames =
                        {
                            new Frame(DateTime.UtcNow)
                            {
                                Objects =
                                {
                                           blob1,blob2,blob3,blob4
                                }
                            }
                        }
                    }
                }
                };

                var result = _metadataProviderChannel.QueueMetadata(metadata, DateTime.UtcNow);
                if (result == false)
                    Console.WriteLine(string.Format("{0}: Failed to write to channel", DateTime.UtcNow));
                else
                {
                    textBoxMetadata.Text = _metadataSerializer.WriteMetadataXml(metadata);
                    Console.WriteLine(DateTime.Now.ToString("HH.mm.ss:fff"));
                }

            }
            catch (Exception e)
            {

                Console.WriteLine(e.Message);
            }

        }


        private void StopMetadata()
        {
            if (_senderTask != null)
            {
                _cts.Cancel();
                _senderTask = null;

            }
        }




    }
}