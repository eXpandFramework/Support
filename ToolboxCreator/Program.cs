using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DevExpress.Utils;
using Microsoft.Win32;

namespace Xpand.ToolboxCreator{
    class Program{
        private const string Toolboxcreatorlog = "toolboxcreator.log";
        private static readonly int[] _vsVersions = {10, 11, 12, 14, 16};

        static void Main(string[] args){
            
            var error = false;
            Trace.AutoFlush = true;
            Trace.Listeners.Add(new TextWriterTraceListener(Toolboxcreatorlog){Name = "FileLog"});
            Trace.Listeners.Add(new ConsoleTraceListener{Name = "ConsoleLog"});
            
            
            string wow = Environment.Is64BitProcess ? @"Wow6432Node\" : null;
            var registryKeys = RegistryKeys(wow);

            if (args.Length == 1 && args[0] == "u"){
                try{
                    DeleteXpandEntries(registryKeys);
                    var assemblyFolderExKey = GetAssemblyFolderExKey(wow);
                    assemblyFolderExKey.DeleteSubKeyTree("Xpand", false);
                    assemblyFolderExKey.Close();
                    VSIXInstaller(@"/u:""Xpand.VSIX.Apostolis Bekiaris.4ab62fb3-4108-4b4d-9f45-8a265487d3dc""", wow);
                    Console.WriteLine("Unistalled");
                }
                catch (Exception e){
                    Trace.TraceError(e.ToString());
                    MessageBox.Show("Error logged in toolboxcreator.log");
                    var process = new Process{
                        StartInfo =
                            new ProcessStartInfo(Toolboxcreatorlog){WorkingDirectory = Environment.CurrentDirectory}
                    };
                    process.Start();
                }
                return;
            }
            try{
                var vsixPath = Path.GetFullPath(AppDomain.CurrentDomain.SetupInformation.ApplicationBase+ @"..\");
                var vsix = @"""" + Directory.GetFiles(vsixPath,"*.vsix").First()+ @"""" ;
                VSIXInstaller(vsix,wow);
                CreateAssemblyFoldersKey(wow);
            }
            catch (Exception e){
                error = true;
                Trace.TraceError(e.ToString());
            }
            var version = new Version();

            var files =
                Directory.EnumerateFiles(AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                    "Xpand.ExpressApp*.dll").ToArray();
            Trace.TraceInformation(files.Length + " eXpand Assemblies found");
            foreach (var file in files){
                try{
                    var assembly = Assembly.LoadFrom(file);
                    if (version == new Version()){
                        version = new Version(FileVersionInfo.GetVersionInfo(assembly.Location + "").FileVersion);
                    }
                    foreach (var type in assembly.GetTypes()){
                        var toolboxItemAttribute =
                            type.GetCustomAttributes(typeof(ToolboxItemAttribute), true)
                                .OfType<ToolboxItemAttribute>()
                                .FirstOrDefault();
                        if (!String.IsNullOrEmpty(toolboxItemAttribute?.ToolboxItemTypeName)){
                            Register(type, file, registryKeys);
                            Trace.TraceInformation("Toolbox-->" + type.FullName);
                        }
                    }
                }
                catch (Exception exception){
                    error = true;
                    var reflectionTypeLoadException = exception as ReflectionTypeLoadException;
                    Trace.TraceError(reflectionTypeLoadException?.LoaderExceptions[0].Message ?? exception.ToString());
                }
            }
            if (error){
                MessageBox.Show("Error logged in toolboxcreator.log");
                var process = new Process{
                    StartInfo = new ProcessStartInfo(Toolboxcreatorlog){WorkingDirectory = Environment.CurrentDirectory}
                };
                process.Start();
            }
            DeleteXpandEntries(registryKeys, s => !s.Contains(version.ToString()));

            foreach (var vsVersion in _vsVersions){
                NotifyVS(wow, vsVersion.ToString());
            }
        }

        private static void VSIXInstaller(string args, string wow) {
            var vsxInstallerPath = VisxInstallerPath(wow);
            Trace.TraceInformation("vsxInstallerPath="+ vsxInstallerPath);
            var processStartInfo = new ProcessStartInfo(vsxInstallerPath, "/a /q " + args){
                WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
            };
            Trace.TraceInformation("WorkingDirectory="+ AppDomain.CurrentDomain.SetupInformation.ApplicationBase);
            Process.Start(processStartInfo);
        }

        private static string VisxInstallerPath(string wow){
            var vsVersions = new[] { @"VisualStudio",  @"VCSExpress", "VBExpress" };
            foreach (var vsVersion in vsVersions){
                string keyPath = $@"SOFTWARE\{wow}Microsoft\{vsVersion}";
                var registryBase32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                var subKey = registryBase32.OpenSubKey(keyPath);
                if (subKey != null)
                    foreach (var subKeyName in subKey.GetSubKeyNames()) {
                        var registryKey = subKey.OpenSubKey(subKeyName);
                        if (registryKey != null){
                            var installDir = registryKey.GetValue("InstallDir") + "";
                            if (Directory.Exists(installDir)) {
                                var installer = Path.Combine(installDir + "VSIXInstaller.exe");
                                if (File.Exists(installer))
                                    return installer;
                            }
                        }
                    }
            }
            throw new FileNotFoundException("VSIXInstaller");
        }

        private static void NotifyVS(string wow, string vsVersion){
            var openSubKey =
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\" + wow + @"Microsoft\VisualStudio\" + vsVersion + @".0\",
                    true);
            openSubKey?.SetValue("ConfigurationChanged", DateTime.Now.ToFileTime(), RegistryValueKind.QWord);
        }

        static void CreateAssemblyFoldersKey(string wow){
            var registryKeys = new[]{
                GetAssemblyFolderExKey(wow),
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\" + wow + @"Microsoft\.NETFramework\AssemblyFolders", true)
            };
            foreach (var registryKey in registryKeys){
                CreateXpandKey(registryKey);
            }
        }

        static void CreateXpandKey(RegistryKey assemblyFoldersKey){
            var registryKey = assemblyFoldersKey?.CreateSubKey("Xpand");
            registryKey?.SetValue(null, AppDomain.CurrentDomain.SetupInformation.ApplicationBase);
        }

        static RegistryKey GetAssemblyFolderExKey(string wow){
            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\" + wow + @"Microsoft\.NETFramework",
                true);
            string minimumClrVersion = MinimumCLRVersion(registryKey);
            var subKey = registryKey?.OpenSubKey(minimumClrVersion + @"\AssemblyFoldersEx", true);
            if (subKey != null){
                return subKey;
            }
            throw new KeyNotFoundException(minimumClrVersion + @"\AssemblyFoldersEx");
        }

        static string MinimumCLRVersion(RegistryKey registryKey){
            return registryKey.GetSubKeyNames().First(s => s.StartsWith("v4"));
        }

        static void DeleteXpandEntries(IEnumerable<RegistryKey> keys, Func<string, bool> func = null){
            foreach (var registryKey in keys){
                var names = registryKey.GetSubKeyNames()
                    .Where(s => s.StartsWith("Xpand") && ((func == null) || func(s)));
                foreach (var name in names){
                    registryKey.DeleteSubKeyTree(name);
                }
                registryKey.Close();
            }
        }

        static List<RegistryKey> RegistryKeys(string wow){
            var registryKeys = new List<RegistryKey>();
            foreach (var version in _vsVersions){
                registryKeys.AddRange(new[]{
                    Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\" + wow + @"Microsoft\VisualStudio\" + version + @".0\ToolboxControlsInstaller\",
                        true),
                    Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\VisualStudio\" + version + @".0_Config\ToolboxControlsInstaller\", true)
                });
            }
            return registryKeys.Where(key => key != null).ToList();
        }

        static void Register(Type type, string file, IEnumerable<RegistryKey> registryKeys){
            foreach (var registryKey in registryKeys){
                var subKey = registryKey.OpenSubKey(type.Assembly.FullName, true) ??
                             registryKey.CreateSubKey(type.Assembly.FullName);
                Debug.Assert(subKey != null, "subKey != null");
                subKey.SetValue("CodeBase", file);
                subKey = subKey.OpenSubKey("ItemCategories", true) ?? subKey.CreateSubKey("ItemCategories");
                Debug.Assert(subKey != null, "subKey2 != null");
                var toolboxTabNameAttribute =
                    type.GetCustomAttributes(typeof(ToolboxTabNameAttribute), false)
                        .OfType<ToolboxTabNameAttribute>()
                        .FirstOrDefault();
                if (toolboxTabNameAttribute != null)
                    subKey.SetValue(type.FullName, toolboxTabNameAttribute.TabName);
            }
        }

    }
}