using System;
using System.Windows;

using VideoOS.Platform.Client;
using VideoOS.Platform;
using VideoOS.Platform.Messaging;
using VideoOS.Platform.UI;
using Analytics.Background;
using VideoOS.Platform.Data;
using System.Threading;
using System.IO;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;


namespace Analytics.Client
{
    /// <summary>
    /// This UserControl contains the visible part of the AnalyticsWorkSpaceViewItemPlugin. <br/>
    /// Este UserControl contiene la parte visible del panel de AnalyticsWorkSpaceViewItemPlugin <br/>
    /// 
    public partial class AnalyticsHeatMapWpfUserControl : ViewItemWpfUserControl
    {
        #region private fields

        private MessageCommunication _messageCommunication;
        private Item _selectItem;
        private FQID _playbackFQID;
        private bool _updatingStreamsFromCode = false;

        private TimeLineUserControl timeLineUserControl1;
        private PlaybackTimeInformationData _currentTimeInformation;


        private MyPlayCommand _nextCommand = MyPlayCommand.None;
        private string _mode = PlaybackPlayModeData.Stop;
        enum MyPlayCommand
        {
            None,
            Start,
            End,
            NextSequence,
            PrevSequence,
            NextFrame,
            PrevFrame
        }


        #endregion

        #region Initialization & Dispose

        /// <summary>
        /// This class is created by the ViewItemManager.  
        public AnalyticsHeatMapWpfUserControl()
        {
            InitializeComponent();
            SetupControls();
        }

        /// <summary>
        /// Initialize the components Controls, listeners and Comunication Manager. 
        /// 
        /// </summary>
        public override void Init()
        {

            // SetUpApplicationEventListeners();
            SetUpComunicationManager();
        }


        /// <summary>
        /// Initialize the Comunication Manager. 
        /// 
        /// </summary>
        private void SetUpComunicationManager()
        {
            MessageCommunicationManager.Start(EnvironmentManager.Instance.MasterSite.ServerId);
            _messageCommunication = MessageCommunicationManager.Get(EnvironmentManager.Instance.MasterSite.ServerId);
        }

        /// <summary>
        /// Perform any cleanup stuff and event -=
        /// </summary>
        public override void Close()
        {
            _stop = true;
            _messageCommunication.Dispose();
        }

        private void SetUpApplicationEventListeners()
        {
            //set up ViewItem event listeners
        }

        private void SetupControls()
        {

            _analyticsProcess = new AnalyticsProcess();
            this.timeLineUserControl1 = new TimeLineUserControl();

            //// In this sample we create a specific PlaybackController.
            //// All commands to this controller needs to be sent via messages with the destination as _playbackFQID.
            //// All message Indications coming from this controller will have sender as _playbackController.
            _playbackFQID = ClientControl.Instance.GeneratePlaybackController();
            EnvironmentManager.Instance.RegisterReceiver(PlaybackTimeChangedHandler,
                                             new MessageIdFilter(MessageId.SmartClient.PlaybackCurrentTimeIndication));
            _fetchThread = new Thread(JPEGFetchThread);
            _fetchThread.Start();


        }
        private Item _selectedItem;
        JPEGVideoSource _jpegVideoSource = null;
        private DateTime _currentShownTime = DateTime.MinValue;
        /// <summary>
        /// All calls to the Media Toolkit is done through the JPEGVideoSource within this thread.
        /// </summary>
        private bool _stop;
        private Thread _fetchThread;
        private DateTime _nextToFetchTime = DateTime.MinValue;
        private bool _requestInProgress = false;
        private bool _performCloseVideoSource = false;
        private int _newHeight = 0;
        private int _newWidth = 0;
        private bool _setNewResolution = false;


        private void JPEGFetchThread()
        {
            ShowError("JPEGFetchThread. Start.");
            bool errorRecovery = false;
            while (!_stop)
            {
                if (_performCloseVideoSource)
                {
                    if (_jpegVideoSource != null)
                    {
                        _jpegVideoSource.Close();
                        _jpegVideoSource = null;
                    }
                    _performCloseVideoSource = false;
                }

                if (_selectItem != null)
                {
                    _selectedItem = _selectItem;
                    _jpegVideoSource = new JPEGVideoSource(_selectedItem);
                    //if (checkBoxAspect.Checked)
                    //{
                    //    // Keeping aspect ratio can only work when the Media Toolkit knows the actual displayed area
                    //    _jpegVideoSource.Width = pictureBox.Width;
                    //    _jpegVideoSource.Height = pictureBox.Height;
                    //    _jpegVideoSource.SetKeepAspectRatio(checkBoxAspect.Checked, checkBoxFill.Checked);  // Must be done before Init
                    //}
                    try
                    {
                        _jpegVideoSource.Init();
                        JPEGData jpegData = _currentShownTime == DateTime.MinValue ? _jpegVideoSource.GetBegin() : _jpegVideoSource.GetAtOrBefore(_currentShownTime) as JPEGData;
                        if (jpegData != null)
                        {
                            _requestInProgress = true;
                            ShowJPEG(jpegData);
                        }
                        else
                        {
                            ShowError("");      // Clear any error messages
                        }
                        // _selectItem = null;
                        errorRecovery = false;
                    }
                    catch (Exception ex)
                    {
                        if (ex is CommunicationMIPException)
                        {
                            ShowError("Connection lost to server ...");
                        }
                        else
                        {
                            ShowError(ex.ToString());
                        }
                        errorRecovery = true;
                        _jpegVideoSource = null;
                        _selectItem = _selectedItem;     // Redo the Initialization
                    }
                }

                if (errorRecovery)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                if (_setNewResolution && _jpegVideoSource != null && _requestInProgress == false)
                {
                    try
                    {
                        _jpegVideoSource.Width = _newWidth;
                        _jpegVideoSource.Height = _newHeight;
                        _jpegVideoSource.SetWidthHeight();
                        _setNewResolution = false;
                        JPEGData jpegData;
                        jpegData = _jpegVideoSource.GetAtOrBefore(_currentShownTime) as JPEGData;
                        if (jpegData != null)
                        {
                            _requestInProgress = true;
                            _currentShownTime = DateTime.MinValue;
                            ShowJPEG(jpegData);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is CommunicationMIPException)
                            ShowError("Connection lost to recorder...");
                        else
                            ShowError(ex.Message);
                        errorRecovery = true;
                        _jpegVideoSource = null;
                        _selectItem = _selectedItem;     // Redo the Initialization
                    }
                }


                if (_requestInProgress == false && _jpegVideoSource != null && _nextCommand != MyPlayCommand.None)
                {
                    JPEGData jpegData = null;

                    try
                    {
                        switch (_nextCommand)
                        {
                            case MyPlayCommand.Start:
                                jpegData = _jpegVideoSource.GetBegin();
                                break;
                            case MyPlayCommand.NextFrame:
                                jpegData = _jpegVideoSource.GetNext() as JPEGData;
                                break;
                            case MyPlayCommand.NextSequence:
                                jpegData = _jpegVideoSource.GetNextSequence();
                                break;
                            case MyPlayCommand.PrevFrame:
                                jpegData = _jpegVideoSource.GetPrevious();
                                break;
                            case MyPlayCommand.PrevSequence:
                                jpegData = _jpegVideoSource.GetPreviousSequence();
                                break;
                            case MyPlayCommand.End:
                                jpegData = _jpegVideoSource.GetEnd();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is CommunicationMIPException)
                            ShowError("Connection lost to recorder...");
                        else
                            ShowError(ex.Message);
                        errorRecovery = true;
                        _jpegVideoSource = null;
                        _selectItem = _selectedItem;     // Redo the Initialization
                    }
                    if (jpegData != null)
                    {
                        _requestInProgress = true;
                        ShowJPEG(jpegData);
                    }

                    _nextCommand = MyPlayCommand.None;
                }

                if (_nextToFetchTime != DateTime.MinValue && _requestInProgress == false && _jpegVideoSource != null)
                {
                    bool willResultInSameFrame = false;
                    // Lets validate if we are just asking for the same frame
                    if (_currentTimeInformation != null)
                    {
                        if (_currentTimeInformation.PreviousTime < _nextToFetchTime &&
                            _currentTimeInformation.NextTime > _nextToFetchTime)
                            willResultInSameFrame = true;
                    }
                    if (willResultInSameFrame)
                    {
                        //   showmessage("Now Fetch ignored: " + _nextToFetchTime.ToLongTimeString() + " - nextToFetch=" + _nextToFetchTime.ToLongTimeString());
                        // Same frame -> Ignore request
                        _requestInProgress = false;
                        _nextToFetchTime = DateTime.MinValue;
                    }
                    else
                    {
                        //showmessage("Now Fetch: " + _nextToFetchTime.ToLongTimeString());
                        DateTime time = _nextToFetchTime;
                        _nextToFetchTime = DateTime.MinValue;

                        try
                        {
                            DateTime localTime = time.Kind == DateTimeKind.Local ? time : time.ToLocalTime();
                            DateTime utcTime = time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : time;



                            /*Dispatcher.BeginInvoke(
                                new Action(delegate () { textBoxAsked.Text = localTime.ToString("yyyy-MM-dd HH:mm:ss.fff"); }));
                            */
                            JPEGData jpegData;
                            jpegData = _jpegVideoSource.GetAtOrBefore(utcTime) as JPEGData;
                            if (jpegData == null && _mode == PlaybackPlayModeData.Stop)
                            {
                                jpegData = _jpegVideoSource.GetNearest(utcTime) as JPEGData;
                            }

                            if (_mode == PlaybackPlayModeData.Reverse)
                            {
                                while (jpegData != null && jpegData.DateTime > utcTime)
                                    jpegData = _jpegVideoSource.GetPrevious();
                            }
                            else if (_mode == PlaybackPlayModeData.Forward)
                            {
                                if (jpegData != null && jpegData.DateTime < utcTime)
                                {
                                    jpegData = _jpegVideoSource.Get(utcTime) as JPEGData;
                                }
                            }
                            if (jpegData != null)
                            {
                                _requestInProgress = true;
                                ShowJPEG(jpegData);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex is CommunicationMIPException)
                            {
                                ShowError("Connection lost to server ...");
                            }
                            else
                            {
                                ShowError(ex.Message);
                            }
                            errorRecovery = true;
                            _jpegVideoSource = null;
                            _selectItem = _selectedItem;     // Redo the Initialization
                        }
                    }
                }
                Thread.Sleep(5);
            }
            _fetchThread = null;
        }

        private object PlaybackTimeChangedHandler(Message message, FQID destination, FQID sender)
        {
            // Only pick up messages coming from my own PlaybackController (sender is null for the common PlaybackController)
            if (_playbackFQID.EqualGuids(sender))
            {
                DateTime time = (DateTime)message.Data;
                //Debug.WriteLine("PlaybackTimeChangedHandler: " + time.ToLongTimeString());

                TimeChangedHandler(time);

                //  timeLineUserControl1.SetShowTime(time);
            }
            return null;
        }



        private void TimeChangedHandler(DateTime time)
        {
            if (_currentShownTime != time)
            {
                _nextToFetchTime = time;
                //Debug.WriteLine("TimeChangedHandler: " + _nextToFetchTime.ToLongTimeString());
            }
        }


        #endregion


        #region Button Actions
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            SearchData data = new SearchData();
            data.Camera = _selectItem.Name as string;
            data.ItemFQID = _selectItem.FQID.ToString();
            data.Initial = initial.SelectedDate;
            data.End = end.SelectedDate;

            try
            {
                _messageCommunication.TransmitMessage(new VideoOS.Platform.Messaging.Message(AnalyticsDefinition.analyticsHeatMapSearchFilterID, data), null, null, null);
                MessageBox.Show("Success" + data.Camera + "" + data.ToString());
            }
            catch (Exception)
            {
                MessageBox.Show("error");
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ItemPickerForm cameraPiker = new ItemPickerForm();
            cameraPiker.KindFilter = Kind.Camera;
            cameraPiker.Init(Configuration.Instance.GetItems());

            if (cameraPiker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _selectItem = cameraPiker.SelectedItem;

                try
                {
                    ShowError(_selectItem.Name);

                    _selectCameraButton.Content = _selectItem.Name;
                    timeLineUserControl1.Item = _selectItem;
                    timeLineUserControl1.CurrentTime = DateTime.Now;
                    //timeLineUserControl1.MouseSetTimeEvent += new EventHandler(timeLineUserControl1_MouseSetTimeEvent);                    
                    //checkBoxAspect.Enabled = false;
                    //checkBoxFill.Enabled = false;

                    var list = _selectItem.GetDataSource().GetTypes();
                    foreach (DataType dt in list)
                    {
                        System.Diagnostics.Trace.WriteLine("Datasource " + dt.Id + "  " + dt.Name);
                    }
                }
                catch (Exception r)
                {

                    MessageBox.Show(r.Message);
                }
            }
        }



        #endregion



        #region ShowJPEG

        // private delegate void ShowJpegDelegate(JPEGData jpegData);

        private void ShowJPEG(JPEGData jpegData)
        {
            /// RETRABAJAR EL DISPATCHER EN EL BACKGROUNG<
            /*  if (!this.Dispatcher.CheckAccess())
              {
                  showmessage("1");
                  Dispatcher.BeginInvoke(new ShowJpegDelegate(ShowJPEG), jpegData);
              }
              else*/

            this.Dispatcher.Invoke(() =>


            {
                //                showmessage("ShowJPEG imagetime:" + jpegData.DateTime.ToLocalTime());
                //               showmessage("ShowJPEG imagetime:" + jpegData.DateTime.ToLocalTime() + ", Decoding:" + jpegData.HardwareDecodingStatus);
                if (jpegData.DateTime != _currentShownTime && _selectedItem != null)
                {
                    //showmessage("2");
                    MemoryStream ms = new MemoryStream(jpegData.Bytes);
                    Bitmap newBitmap_original = new Bitmap(ms);

                    pictureBoxOriginal.Source = ConverBitmapToBitmapImage(newBitmap_original);

                    //*** HERE IS WHERE MAGIC IS MADE
                    Bitmap newBitmap = _analyticsProcess.ProcessImage(newBitmap_original);


                    if (newBitmap.Width != pictureBox.Width || newBitmap.Height != pictureBox.Height)
                    {

                        pictureBox.Source = ConverBitmapToBitmapImage(newBitmap);
                    }
                    else
                    {
                        pictureBox.Source = ConverBitmapToBitmapImage(newBitmap);
                    }

                    if (jpegData.CroppingDefined)
                    {
                        System.Console.WriteLine("Image has been cropped: " + jpegData.CropWidth + "x" + jpegData.CropHeight);
                    }

                    ms.Close();
                    ms.Dispose();

                    textBoxTime.Text = jpegData.DateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");

                    // Inform the PlybackController of the time information - so skipping can be done correctly
                    _currentTimeInformation = new PlaybackTimeInformationData()
                    {
                        Item = _selectedItem.FQID,
                        CurrentTime = jpegData.DateTime,
                        NextTime = jpegData.NextDateTime,
                        PreviousTime = jpegData.PreviousDateTime
                    };
                    EnvironmentManager.Instance.SendMessage(
                        new VideoOS.Platform.Messaging.Message(MessageId.SmartClient.PlaybackTimeInformation, _currentTimeInformation), _playbackFQID);

                    _currentShownTime = jpegData.DateTime;
                    if (_mode == PlaybackPlayModeData.Stop)
                    {
                        //showmessage("3");
                        // When playback is stopped, we move the time to where the user have scrolled, or if the user pressed 
                        // one of the navigation buttons (Next..., Prev...)
                        EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(MessageId.SmartClient.PlaybackCommand,
                                                                new PlaybackCommandData()
                                                                {
                                                                    Command = PlaybackData.Goto,
                                                                    DateTime = jpegData.DateTime
                                                                }),
                                                                    _playbackFQID);
                    }
                    System.Console.WriteLine("Image time: " + jpegData.DateTime.ToLocalTime().ToString("HH.mm.ss.fff") + ", Mode=" + _mode);
                }
                _requestInProgress = false;

            });
        }



  

        /// <summary>
        /// New code as from MIPSDK 4.0 - to handle connection issues
        /// </summary>
        /// <param name="errorText"></param>
        private delegate void ShowErrorDelegate(String errorText);
        private void ShowError(String errorText)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new ShowErrorDelegate(ShowError), errorText);
            }
            else
            {
                Font font = new Font("Arial", 12);

                Bitmap bitmap = new Bitmap(800, 600);
                Graphics g = Graphics.FromImage(bitmap);
                g.FillRectangle(Brushes.Black, 0, 0, bitmap.Width, bitmap.Height);

                g.DrawString(errorText, font, Brushes.White, new PointF(5, 5));
                g.Dispose();
                pictureBox.Source = ConverBitmapToBitmapImage(bitmap);
                bitmap.Dispose();
            }
        }


        #endregion


        ///// My best friend 

        private void showmessage(string message)
        {
            this.Dispatcher.Invoke(() =>
                            {
                                textConsole.Text += message;
                            });
        }


        private BitmapImage ConverBitmapToBitmapImage(System.Drawing.Bitmap bmp)
        {
            MemoryStream stream = new MemoryStream();
            bmp.Save(stream, ImageFormat.Png);
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            return bitmapImage;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _nextCommand = MyPlayCommand.PrevFrame;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            _nextCommand = MyPlayCommand.NextFrame;
        }

        private void DBstart_Click(object sender, RoutedEventArgs e)
        {
            _nextCommand = MyPlayCommand.Start;
        }


        private double _speed = 1.0;
        private AnalyticsProcess _analyticsProcess ;

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_speed == 0.0)
                _speed = 1.0;
            else
                _speed *= 2;
            _mode = PlaybackPlayModeData.Forward;
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                        MessageId.SmartClient.PlaybackCommand,
                                                        new PlaybackCommandData() { Command = PlaybackData.PlayForward, Speed = _speed }), _playbackFQID);

        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                  MessageId.SmartClient.PlaybackCommand,
                                                  new PlaybackCommandData() { Command = PlaybackData.PlayStop }), _playbackFQID);
            EnvironmentManager.Instance.Mode = Mode.ClientPlayback;
            _speed = 0.0;
            _mode = PlaybackPlayModeData.Stop;
        }
    }
}
