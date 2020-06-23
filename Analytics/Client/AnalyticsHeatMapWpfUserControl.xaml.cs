using VideoOS.Platform.Client;
using VideoOS.Platform;
using VideoOS.Platform.Messaging;
using Analytics.Background;
using System.Windows;
using System;
using VideoOS.Platform.UI;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.IO;
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

        #endregion

        /// <summary>
        /// This class is created by the ViewItemManager.  
        public AnalyticsHeatMapWpfUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize the components Controls, listeners and Comunication Manager. 
        /// 
        /// </summary>
        public override void Init()
        {
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

            // Create a fiter to get messages from Smart Client Plugin
            Object _heatmapSearchFilter = _messageCommunication.RegisterCommunicationFilter(HeatMapPicHandler, new VideoOS.Platform.Messaging.CommunicationIdFilter("heatmapPic"));

        }

        private object HeatMapPicHandler(Message message, FQID destination, FQID sender)
        {
            Bitmap data = (message.Data as Bitmap);

            if (!Dispatcher.CheckAccess())
                this.Dispatcher.Invoke(() =>
            {
                heatMapImage.Source = ConverBitmapToBitmapImage(data);
            });
            else
                heatMapImage.Source = ConverBitmapToBitmapImage(data);

            return null;
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

        /// <summary>
        /// Perform any cleanup stuff and event -=
        /// </summary>
        public override void Close()
        {
            _messageCommunication.Dispose();
        }


        private void Button_Click_1(object sender, System.Windows.RoutedEventArgs e)
        {
            SearchData data = new SearchData
            {
                Camera = _selectItem.Name as string,
                ItemFQID = _selectItem.FQID.ToString(),
                Initial = initial.SelectedDate,
                End = end.SelectedDate,
                ObjectID = _selectItem.FQID.ObjectId.ToString(),
                ObjectKind = _selectItem.FQID.Kind.ToString()
            };

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
            ItemPickerForm cameraPiker = new ItemPickerForm
            {
                KindFilter = Kind.Camera
            };

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
