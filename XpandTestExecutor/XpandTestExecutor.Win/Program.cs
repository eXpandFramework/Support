using System;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Forms;
using DevExpress.ExpressApp.Security;
using XpandTestExecutor.Module.Controllers;
using XpandTestExecutor.Module.Services;

namespace XpandTestExecutor.Win {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args){
            


#if EASYTEST
            DevExpress.ExpressApp.Win.EasyTest.EasyTestRemotingRegistration.Register();
#endif

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            EditModelPermission.AlwaysGranted = Debugger.IsAttached;
            var winApplication = new XpandTestExecutorWindowsFormsApplication();
            // Refer to the http://documentation.devexpress.com/#Xaf/CustomDocument2680 help article for more details on how to provide a custom splash form.
            //winApplication.SplashScreen = new DevExpress.ExpressApp.Win.Utils.DXSplashScreen("YourSplashImage.png");
            if(ConfigurationManager.ConnectionStrings["ConnectionString"] != null) {
                winApplication.ConnectionString = ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString;
            }
#if EASYTEST
            if(ConfigurationManager.ConnectionStrings["EasyTestConnectionString"] != null) {
                winApplication.ConnectionString = ConfigurationManager.ConnectionStrings["EasyTestConnectionString"].ConnectionString;
            }
#endif
            try {
                
                winApplication.Setup();
//                args=new string[]{"easytests.txt"};
                if (args.Length > 0){
                    winApplication.CreateObjectSpace();
                    TestExecutor.Execute(args[0], ((IModelOptionsTestExecutor) winApplication.Model.Options).ExecutionRetries);
                }
                else
                    winApplication.Start();
            }
            catch(Exception e) {
                winApplication.HandleException(e);
            }
        }


    }
}
