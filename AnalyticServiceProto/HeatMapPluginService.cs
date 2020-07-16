using AForge.Imaging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VideoOS.Platform;
using VideoOS.Platform.Data;
using VideoOS.Platform.Live;
using VideoOS.Platform.Messaging;
using VideoOS.Platform.Metadata;
using VideoOS.Platform.Util.AdaptiveStreaming;

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

        public HeatMapPluginService()
        {
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
                MessageBox.Show("Could not Init:" + ex.Message);
                _jpegLiveSource = null;
            }
        }


        /// <summary>
        /// This event is called when JPEG is available or some exception has occurred
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

                        ms.Close();
                        ms.Dispose();

                        _count++;
                        textBoxCount.Text = "" + _count;

                        args.LiveContent.Dispose();

                        /// PocessImage

                        Bitmap motionObjectsImage = analyticsImageProcessing.ProcessImage(newBitmap);
                        pictureBoxProcessed.Image = motionObjectsImage;
                        Blob[] blobs = analyticsImageProcessing.GetBlobs(motionObjectsImage, blobCounter);
                        textBoxMetadata.Text = metadataHandler.SendMetadataBox(blobs, _jpegLiveSource.Width, _jpegLiveSource.Height);
                        PaintHeatMap(blobs);


                        /// Debug tool

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
            pictureBoxHeatMap.Image = bitmapHeatMap;
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
            Bitmap data = (Bitmap)pictureBoxHeatMap.Image;
            _messageCommunication.TransmitMessage(new VideoOS.Platform.Messaging.Message("heatmapPic", data), null, null, null);
        }


    }
}