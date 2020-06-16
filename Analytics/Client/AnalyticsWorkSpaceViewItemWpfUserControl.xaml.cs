using System;
using VideoOS.Platform.Client;

namespace Analytics.Client
{
    /// <summary>
    /// Interaction logic for AnalyticsWorkSpaceViewItemWpfUserControl.xaml
    /// </summary>
    public partial class AnalyticsWorkSpaceViewItemWpfUserControl : ViewItemWpfUserControl
    {
        public AnalyticsWorkSpaceViewItemWpfUserControl()
        {
            InitializeComponent();
        }

        public override void Init()
        {
        }

        public override void Close()
        {
        }

        /// <summary>
        /// Do not show the sliding toolbar!
        /// </summary>
        public override bool ShowToolbar
        {
            get { return false; }
        }

        private void ViewItemWpfUserControl_ClickEvent(object sender, EventArgs e)
        {
            FireClickEvent();
        }

        private void ViewItemWpfUserControl_DoubleClickEvent(object sender, EventArgs e)
        {
            FireDoubleClickEvent();
        }
    }
}
