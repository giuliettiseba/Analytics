using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoOS.Platform;
using VideoOS.Platform.Login;
using VideoOS.Platform.SDK.UI.LoginDialog;

namespace AnalyticServiceProto
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
          	Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

            
			VideoOS.Platform.SDK.Environment.Initialize();		// Initialize the standalone Environment
			VideoOS.Platform.SDK.Media.Environment.Initialize();        // Initialize the standalone Environment

            EnvironmentManager.Instance.EnvironmentOptions[EnvironmentOptions.HardwareDecodingMode] = "Auto";
            // EnvironmentManager.Instance.EnvironmentOptions[EnvironmentOptions.HardwareDecodingMode] = "Off";
			// EnvironmentManager.Instance.EnvironmentOptions["ToolkitFork"] = "No";

			EnvironmentManager.Instance.TraceFunctionCalls = true;

			DialogLoginForm loginForm = new DialogLoginForm(SetLoginResult);
			Application.Run(loginForm);
			if (Connected)
			{
				Application.Run(new Main());
			}
        }

        private static bool Connected = false;
        private static void SetLoginResult(bool connected)
        {
            Connected = connected;
        }
    }
}
