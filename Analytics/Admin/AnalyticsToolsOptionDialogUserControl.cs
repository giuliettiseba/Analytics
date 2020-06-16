using VideoOS.Platform.Admin;

namespace Analytics.Admin
{
    public partial class AnalyticsToolsOptionDialogUserControl : ToolsOptionsDialogUserControl
    {
        public AnalyticsToolsOptionDialogUserControl()
        {
            InitializeComponent();
        }

        public override void Init()
        {
        }

        public override void Close()
        {
        }

        public string MyPropValue
        {
            set { textBoxPropValue.Text = value ?? ""; }
            get { return textBoxPropValue.Text; }
        }
    }
}
