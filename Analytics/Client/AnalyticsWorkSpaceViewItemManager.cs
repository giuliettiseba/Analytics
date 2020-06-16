using VideoOS.Platform.Client;

namespace Analytics.Client
{
    public class AnalyticsWorkSpaceViewItemManager : ViewItemManager
    {
        public AnalyticsWorkSpaceViewItemManager() : base("AnalyticsWorkSpaceViewItemManager")
        {
        }

        public override ViewItemWpfUserControl GenerateViewItemWpfUserControl()
        {
            return new AnalyticsHeatMapWpfUserControl();
        }
    }
}
