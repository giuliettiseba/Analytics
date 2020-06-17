using System;
using System.Windows;

using VideoOS.Platform.Client;
using VideoOS.Platform;
using VideoOS.Platform.Messaging;
using VideoOS.Platform.UI;
using Analytics.Background;
using System.Windows.Media.Media3D;
using VideoOS.Platform.Data;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Analytics.Client
{
    /// <summary>
    /// This UserControl contains the visible part of the Property panel during Setup mode. <br/>
    /// If no properties is required by this ViewItemPlugin, the GeneratePropertiesUserControl() method on the ViewItemManager can return a value of null.
    /// <br/>
    /// When changing properties the ViewItemManager should continuously be updated with the changes to ensure correct saving of the changes.
    /// <br/>
    /// As the user click on different ViewItem, the displayed property UserControl will be disposed, and a new one created for the newly selected ViewItem.
    /// </summary>
    public partial class AnalyticsHeatMapWpfUserControl : ViewItemWpfUserControl
    {
        #region private fields


        private MessageCommunication _messageCommunication;

        //  private ImageViewerWpfControl _imageViewerControl;
        private Item _selectedCameraItem;
        private Item _selectItem;

        private FQID _playbackFQID;


        #endregion

        #region Initialization & Dispose

        /// <summary>
        /// This class is created by the ViewItemManager.  
        /// </summary>
        /// <param name="viewItemManager"></param>
        public AnalyticsHeatMapWpfUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Setup events and message receivers and load stored configuration.
        /// </summary>
        public override void Init()
        {
            SetupControls();
            SetUpApplicationEventListeners();


            MessageCommunicationManager.Start(EnvironmentManager.Instance.MasterSite.ServerId);
            _messageCommunication = MessageCommunicationManager.Get(EnvironmentManager.Instance.MasterSite.ServerId);



            //  _imageViewerControl = ClientControl.Instance.(WindowInformation);


            //         panel2 = ImageViewerWpfControl();
            //            panel2.Controls.Add(_imageViewerControl);
            //          _imageViewerControl.Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Perform any cleanup stuff and event -=
        /// </summary>
        public override void Close()
        {
            _imageViewerControl.Disconnect();
            _imageViewerControl.Close();
            _imageViewerControl.Dispose();
            if (_playbackFQID != null)
            {
                ClientControl.Instance.ReleasePlaybackController(_playbackFQID);
                _playbackFQID = null;
            }
        }
        #endregion

        private void SetUpApplicationEventListeners()
        {
            //set up ViewItem event listeners

        }



        #region Select camera and setup controls
        private void SetupControls()
        {
            _imageViewerControl.Disconnect();

            _imageViewerControl.EnableDigitalZoom = true;
            _imageViewerControl.MaintainImageAspectRatio = true;
            _imageViewerControl.EnableVisibleHeader = true;
            _imageViewerControl.EnableVisibleCameraName = true;
            _imageViewerControl.EnableVisibleLiveIndicator = true;
            _imageViewerControl.EnableVisibleTimestamp = true;

            if (_playbackFQID == null)
            {
                _playbackFQID = ClientControl.Instance.GeneratePlaybackController();
                _playbackUserControl.Init(_playbackFQID);
                SetPlaybackSkipMode();
            }

        }


        #endregion

        #region Event handling
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {


            SearchData data = new SearchData();
            data.Camera = _selectedCameraItem.Name as string;
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
        #endregion


        private bool _updatingStreamsFromCode = false;
        private IList<DataType> _streams;


        #region Button Actions
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ItemPickerForm cameraPiker = new ItemPickerForm();
            cameraPiker.KindFilter = Kind.Camera;
            cameraPiker.Init(Configuration.Instance.GetItems());

            if (cameraPiker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {

                /*
                                _updatingStreamsFromCode = true;

                                var streamDataSource = new StreamDataSource(_selectItem);
                                _streams = streamDataSource.GetTypes();
                                _streamsComboBox.ItemsSource = _streams;
                                foreach (DataType stream in _streamsComboBox.Items)
                                {
                                    if (stream.Properties.ContainsKey("Default"))
                                    {
                                        if (stream.Properties["Default"] == "Yes")
                                        {
                                            _streamsComboBox.SelectedItem = stream;
                                        }
                                    }
                                }
                                _updatingStreamsFromCode = false;
                                */

                try
                {
                    _selectItem = cameraPiker.SelectedItem;
                    _selectCameraButton.Content = _selectItem.Name;
                    _playbackUserControl.SetCameras(new List<FQID>() { _selectItem.FQID });
                    _imageViewerControl.CameraFQID = _selectItem.FQID;
                    _imageViewerControl.PlaybackControllerFQID = _playbackFQID;


                    if (_streamsComboBox.SelectedItem != null)
                    {
                        _imageViewerControl.StreamId = ((DataType)_streamsComboBox.SelectedItem).Id;
                    }
                    _imageViewerControl.Initialize();
                    _imageViewerControl.Connect();
                }
                catch (Exception r )
                {

                    MessageBox.Show(r.Message);
                }

            


            }
        }



        private void _streamsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_updatingStreamsFromCode && _imageViewerControl != null && _imageViewerControl.CameraFQID != null && _streamsComboBox.SelectedItem != null)
            {
                _imageViewerControl.Disconnect();
                DataType selectStream = (DataType)_streamsComboBox.SelectedItem;

                _imageViewerControl.StreamId = selectStream.Id;
                _imageViewerControl.Connect();
            }
        }



        #endregion



        #region helper method
        private void SetPlaybackSkipMode()
        {
            if (_skipRadioButton.IsChecked.Value)
            {
                EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                                VideoOS.Platform.Messaging.MessageId.SmartClient.PlaybackSkipModeCommand,
                                                                PlaybackSkipModeData.Skip), _playbackFQID);
            }
            else if (_noSkipRadioButton.IsChecked.Value)
            {
                EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                                             VideoOS.Platform.Messaging.MessageId.SmartClient.PlaybackSkipModeCommand,
                                                                             PlaybackSkipModeData.Noskip), _playbackFQID);
            }
            else if (_stopRadioButton.IsChecked.Value)
            {
                EnvironmentManager.Instance.SendMessage(new VideoOS.Platform.Messaging.Message(
                                                                             VideoOS.Platform.Messaging.MessageId.SmartClient.PlaybackSkipModeCommand,
                                                                             PlaybackSkipModeData.StopAtSequenceEnd), _playbackFQID);
            }
        }




        #region Plackback Controller properties
        private void _checkAllRadioButtonsChecked(object sender, RoutedEventArgs e)
        {
            SetPlaybackSkipMode();
        }
        #endregion










        #endregion




    }
}
