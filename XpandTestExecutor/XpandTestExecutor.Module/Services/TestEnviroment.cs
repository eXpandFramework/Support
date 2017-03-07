using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xpand.Utils.Helpers;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Services {
    public static class TestEnviroment {
        public static void KillRDClient() {
            var processes = Process.GetProcesses().Where(process => process.ProcessName.Contains("RDClient")).ToArray();
            foreach (var process in processes) {
                process.Kill();
            }
        }

        public static void KillWebDev(string name) {
            EnviromentEx.KillProccesses(name, i => Process.GetProcessById(i).ProcessName.StartsWith("WebDev.WebServer40"));
        }

        public static void Unlink(this EasyTestExecutionInfo info){
            Setup(info, true);
        }

        public static void Setup(this EasyTestExecutionInfo info){
            Setup(info,false);
            var path = Path.Combine(Path.GetDirectoryName(info.EasyTest.FileName)+"","testslog.xml");
            if (File.Exists(path))
                File.Delete(path);

        }

        static void Setup(this EasyTestExecutionInfo info,bool unlink) {
            if (!unlink)
                info.Setup(true);
            TestUpdater.UpdateTestConfig(info, unlink);
            AppConfigUpdater.Update(info,unlink);
            TestUpdater.UpdateTestFile(info,unlink);
        }

        public static void Setup(EasyTest[] easyTests,ExecutionInfo executionInfo) {
            var users = executionInfo.WindowsUsers.Select(user => user.Name).ToArray();
            EnviromentEx.LogOffAllUsers(users);
            OptionsProvider.Init(easyTests.Select(test => test.FileName).ToArray());
            KillRDClient();
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(easyTests.First().FileName)+"","rdclient.exe"))) {
                var fileName = Path.GetFullPath(@"..\CopyEasyTestReqs.bat");
                var processStartInfo = new ProcessStartInfo(fileName) {
                    WorkingDirectory = Path.GetDirectoryName(fileName) + ""
                };
                var process = new Process { StartInfo = processStartInfo };
                process.Start();
                process.WaitForExit();
            }
            var directories = Directory.GetDirectories(@"..\",TestExecutor.EasyTestUsersDir,SearchOption.AllDirectories);
            foreach (var directory in directories){
                Directory.Delete(directory,true);
            }
        }

        public static void Terminate(){
            throw new Exception();
        }

    }
}