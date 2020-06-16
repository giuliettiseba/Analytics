using System;
using System.Collections.Generic;
using VideoOS.Platform;
using VideoOS.Platform.Client;

namespace Analytics.Client
{
    class AnalyticsWorkSpaceToolbarPluginInstance : WorkSpaceToolbarPluginInstance
    {
        private Item _window;

        public AnalyticsWorkSpaceToolbarPluginInstance()
        {
        }

        public override void Init(Item window)
        {
            _window = window;

            Title = "Analytics";
        }

        public override void Activate()
        {
            // Here you should put whatever action that should be executed when the toolbar button is pressed
        }

        public override void Close()
        {
        }

    }

    class AnalyticsWorkSpaceToolbarPlugin : WorkSpaceToolbarPlugin
    {
        public AnalyticsWorkSpaceToolbarPlugin()
        {
        }

        public override Guid Id
        {
            get { return AnalyticsDefinition.AnalyticsWorkSpaceToolbarPluginId; }
        }

        public override string Name
        {
            get { return "Analytics"; }
        }

        public override void Init()
        {
            // TODO: remove below check when AnalyticsDefinition.AnalyticsWorkSpaceToolbarPluginId has been replaced with proper GUID
            if (Id == new Guid("15975315-9753-1597-5315-159753159753"))
            {
                System.Windows.MessageBox.Show("Default GUID has not been replaced for AnalyticsWorkSpaceToolbarPluginId!");
            }

            WorkSpaceToolbarPlaceDefinition.WorkSpaceIds = new List<Guid>() { ClientControl.LiveBuildInWorkSpaceId, ClientControl.PlaybackBuildInWorkSpaceId, AnalyticsDefinition.AnalyticsWorkSpacePluginId };
            WorkSpaceToolbarPlaceDefinition.WorkSpaceStates = new List<WorkSpaceState>() { WorkSpaceState.Normal };
        }

        public override void Close()
        {
        }

        public override WorkSpaceToolbarPluginInstance GenerateWorkSpaceToolbarPluginInstance()
        {
            return new AnalyticsWorkSpaceToolbarPluginInstance();
        }
    }
}
