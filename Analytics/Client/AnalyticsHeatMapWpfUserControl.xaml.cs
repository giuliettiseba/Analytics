using System;
using System.Windows;

using VideoOS.Platform.Client;
using VideoOS.Platform;
using VideoOS.Platform.Messaging;
using VideoOS.Platform.UI;
using Analytics.Background;

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

            SetUpApplicationEventListeners();


            MessageCommunicationManager.Start(EnvironmentManager.Instance.MasterSite.ServerId);
            _messageCommunication = MessageCommunicationManager.Get(EnvironmentManager.Instance.MasterSite.ServerId);


        }

        /// <summary>
        /// Perform any cleanup stuff and event -=
        /// </summary>
        public override void Close()
        {
        }
        #endregion

        private void SetUpApplicationEventListeners()
        {
            //set up ViewItem event listeners

        }

        #region Event handling

        #endregion
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {


            SearchData data = new SearchData();
            data.Camera = CamaraSeleccionada.Content as string;
            data.Initial = initial.Value;
            data.End = end.Value;




            try
            {
                _messageCommunication.TransmitMessage(new VideoOS.Platform.Messaging.Message(AnalyticsBackgroundPlugin.analyticsHeatMapSearchFilterID, data), null, null, null);
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
            //cameraPiker.AutoAccept = true;
            cameraPiker.Init(Configuration.Instance.GetItems());
            if (cameraPiker.ShowDialog() == System.Windows.Forms.DialogResult.OK) 
            {

                CamaraSeleccionada.Content = cameraPiker.SelectedItem.Name;
            }
        }
    }

}
