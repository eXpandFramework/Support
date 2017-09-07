using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Xpand.ToolboxCreator {
    class Program {
        private const string Toolboxcreatorlog = "toolboxcreator.log";

        static void Main(string[] args) {

            var error = false;
            Trace.AutoFlush = true;
            Trace.Listeners.Add(new TextWriterTraceListener(Toolboxcreatorlog) { Name = "FileLog" });
            Trace.Listeners.Add(new ConsoleTraceListener { Name = "ConsoleLog" });


            string wow = Environment.Is64BitProcess ? @"Wow6432Node\" : null;

            if (args.Length == 1 && args[0] == "u") {
                try {
                    var assemblyFolderExKey = GetAssemblyFolderExKey(wow);
                    assemblyFolderExKey.DeleteSubKeyTree("Xpand", false);
                    assemblyFolderExKey.Close();
                    VSIXInstaller(@"/u:""Xpand.VSIX.Apostolis Bekiaris.4ab62fb3-4108-4b4d-9f45-8a265487d3dc""");
                    Console.WriteLine("Unistalled");
                }
                catch (Exception e) {
                    Trace.TraceError(e.ToString());
                    MessageBox.Show("Error logged in toolboxcreator.log");
                    var process = new Process {
                        StartInfo =
                            new ProcessStartInfo(Toolboxcreatorlog) { WorkingDirectory = Environment.CurrentDirectory }
                    };
                    process.Start();
                }
                return;
            }
            try {
                var vsixPath = Path.GetFullPath(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + @"..\");
                var vsix = @"""" + Directory.GetFiles(vsixPath, "*.vsix").FirstOrDefault() + @"""";
                VSIXInstaller(vsix);
                CreateAssemblyFoldersKey(wow);
            }
            catch (Exception e) {
                error = true;
                Trace.TraceError(e.ToString());
            }

            if (error) {
                MessageBox.Show("Error logged in toolboxcreator.log");
                var process = new Process {
                    StartInfo = new ProcessStartInfo(Toolboxcreatorlog) { WorkingDirectory = Environment.CurrentDirectory }
                };
                process.Start();
            }
        }

        private static void VSIXInstaller(string args) {
            var processStartInfo = new ProcessStartInfo("VSIXBootstrapper.exe", "/a /q " + args) {
                WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
            };
            Trace.TraceInformation("WorkingDirectory=" + AppDomain.CurrentDomain.SetupInformation.ApplicationBase);
            Process.Start(processStartInfo);
        }


        static void CreateAssemblyFoldersKey(string wow) {
            var registryKeys = new[]{
                GetAssemblyFolderExKey(wow),
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\" + wow + @"Microsoft\.NETFramework\AssemblyFolders", true)
            };
            foreach (var registryKey in registryKeys) {
                CreateXpandKey(registryKey);
            }
        }

        static void CreateXpandKey(RegistryKey assemblyFoldersKey) {
            var registryKey = assemblyFoldersKey?.CreateSubKey("Xpand");
            registryKey?.SetValue(null, AppDomain.CurrentDomain.SetupInformation.ApplicationBase);
        }

        static RegistryKey GetAssemblyFolderExKey(string wow) {
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\" + wow + @"Microsoft\.NETFramework",
                true);
            string minimumClrVersion = MinimumCLRVersion(registryKey);
            var subKey = registryKey?.OpenSubKey(minimumClrVersion + @"\AssemblyFoldersEx", true);
            if (subKey != null) {
                return subKey;
            }
            throw new KeyNotFoundException(minimumClrVersion + @"\AssemblyFoldersEx");
        }

        static string MinimumCLRVersion(RegistryKey registryKey) {
            return registryKey.GetSubKeyNames().First(s => s.StartsWith("v4"));
        }
    }
}