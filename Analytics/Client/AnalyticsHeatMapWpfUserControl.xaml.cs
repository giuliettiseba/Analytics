using VideoOS.Platform.Client;
using VideoOS.Platform;
using VideoOS.Platform.Messaging;
using Analytics.Background;
using System.Windows;
using System;
using VideoOS.Platform.UI;

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
            _messageCommunication.Dispose();
        }

        private void SetUpApplicationEventListeners()
        {
            //set up ViewItem event listeners
        }

        private void SetupControls()
        {

            //    _analyticsProcess = new AnalyticsProcess();
            this.timeLineUserControl1 = new TimeLineUserControl();

            //// In this sample we create a specific PlaybackController.
            //// All commands to this controller needs to be sent via messages with the destination as _playbackFQID.
            //// All message Indications coming from this controller will have sender as _playbackController.


        }

        private void Button_Click_1(object sender, System.Windows.RoutedEventArgs e)
        {
            SearchData data = new SearchData();
            data.Camera = _selectItem.Name as string;
            data.ItemFQID = _selectItem.FQID.ToString();
            data.Initial = initial.SelectedDate;
            data.End = end.SelectedDate;
            data.ObjectID = _selectItem.FQID.ObjectId.ToString();
            data.ObjectKind = _selectItem.FQID.Kind.ToString();


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
                    _selectCameraButton.Content = _selectItem.Name;
                }
                catch (Exception r)
                {

                    MessageBox.Show(r.Message);
                }
            }

        }
    }
}
