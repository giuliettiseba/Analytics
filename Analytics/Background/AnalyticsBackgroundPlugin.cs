using System;
using System.Collections.Generic;
using System.Threading;
using System.Xml;
using VideoOS.Platform;
using VideoOS.Platform.Background;
using VideoOS.Platform.Client;
using VideoOS.Platform.Messaging;

namespace Analytics.Background
{
    /// <summary>
    /// A background plugin will be started during application start and be running until the user logs off or application terminates.<br/>
    /// The Environment will call the methods Init() and Close() when the user login and logout, 
    /// so the background task can flush any cached information.<br/>
    /// The base class implementation of the LoadProperties can get a set of configuration, 
    /// e.g. the configuration saved by the Options Dialog in the Smart Client or a configuration set saved in one of the administrators.  
    /// Identification of which configuration to get is done via the GUID.<br/>
    /// The SaveProperties method can be used if updating of configuration is relevant.
    /// <br/>
    /// The configuration is stored on the server the application is logged into, and should be refreshed when the ApplicationLoggedOn method is called.
    /// Configuration can be user private or shared with all users.<br/>
    /// <br/>
    /// This plugin could be listening to the Message with MessageId == Server.ConfigurationChangedIndication to when when to reload its configuration.  
    /// This event is send by the environment within 60 second after the administrator has changed the configuration.
    /// </summary>
    public class AnalyticsBackgroundPlugin : BackgroundPlugin
    {
        private bool _stop = false;
        private Thread _thread;
        private MessageCommunication _messageCommunication;
        private object _heatmapSearchFilter;

        /// <summary>
        /// Gets the unique id identifying this plugin component
        /// </summary>
        public override Guid Id
        {
            get { return AnalyticsDefinition.AnalyticsBackgroundPlugin; }
        }

        /// <summary>
        /// The name of this background plugin
        /// </summary>
        public override String Name
        {
            get { return "Analytics BackgroundPlugin"; }
        }

        /// <summary>
        /// Called by the Environment when the user has logged in.
        /// </summary>
        public override void Init()
        {

            //initiates the message communication
            //    MessageCommunicationManager.Start(EnvironmentManager.Instance.MasterSite.ServerId);
            MessageCommunicationManager.Start(EnvironmentManager.Instance.MasterSite.ServerId);
            _messageCommunication = MessageCommunicationManager.Get(EnvironmentManager.Instance.MasterSite.ServerId);
            _heatmapSearchFilter = _messageCommunication.RegisterCommunicationFilter(HeatMapSearchHandler, new VideoOS.Platform.Messaging.CommunicationIdFilter(AnalyticsDefinition.analyticsHeatMapSearchFilterID));


            _stop = false;
            _thread = new Thread(new ThreadStart(Run));
            _thread.Name = "Analytics Background Thread";
            _thread.Start();
        }

        /// <summary>
        /// Called by the Environment when the user log's out.
        /// You should close all remote sessions and flush cache information, as the
        /// user might logon to another server next time.
        /// </summary>
        public override void Close()
        {
            _stop = true;
        }

        /// <summary>
        /// Define in what Environments the current background task should be started.
        /// </summary>
        public override List<EnvironmentType> TargetEnvironments
        {
            get { return new List<EnvironmentType>() { EnvironmentType.Service }; } // Default will run in the Event Server
        }



     
        /// <summary>
        /// the thread doing the work
        /// </summary>
        private void Run()
        {
            EnvironmentManager.Instance.Log(false, "Analytics background thread", "Now starting...", null);



            while (!_stop)
            {
                // Do some work here.

                Thread.Sleep(2000);
            }
            EnvironmentManager.Instance.Log(false, "Analytics background thread", "Now stopping...", null);
            _thread = null;
        }





        private object HeatMapSearchHandler(Message message, FQID destination, FQID sender)
        {

            SearchData data = (message.Data as SearchData);

            EnvironmentManager.Instance.Log(false , "Heatmap", message.ToString());

            EnvironmentManager.Instance.Log(false, "Camara: ", data.Camera);
            EnvironmentManager.Instance.Log(false, "Camara: ", data.End.ToString());
            EnvironmentManager.Instance.Log(false, "Camara: ", data.Initial.ToString());
            

            return null;
        }


    }


    [Serializable]
    internal class SearchData
    {
        public string Entry { get; set; }
        public string Camera { get; set; }
      public DateTime? End  { get; internal set; }
        public DateTime? Initial { get; internal set; }
    }
}
