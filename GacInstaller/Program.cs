using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;

namespace GacInstaller {
    internal class Program {
        private static readonly object _locker=new object();

        static void Main(string[] args){
            Trace.AutoFlush = true;
            Trace.Listeners.Add(new TextWriterTraceListener("GacInstaller.log") { TraceOutputOptions = TraceOptions.DateTime });
            Trace.Listeners.Add(new ConsoleTraceListener{Name = "Console",TraceOutputOptions = TraceOptions.None});
            var options = new Options();
            bool arguments = Parser.Default.ParseArguments(args, options);
            if (!arguments){
                Trace.TraceInformation(options.GetUsage());
                Console.ReadKey();
                return;
            }
            string gacUtilPath = LocateGacUtil();
            bool error=false;
            var tasks = new List<Task>();
            foreach (var file in GetFiles()) {
                if (options.Regex == null || Regex.IsMatch(Path.GetFileNameWithoutExtension(file) + "", options.Regex)) {
                    var fileName = options.Mode == Mode.Install ? Path.GetFileName(file) : Path.GetFileNameWithoutExtension(file);
                    string arg = options.Mode == Mode.Install ? "ir" : "ur";
                    var task = Task.Factory.StartNew(() => {
                        try{
                            var processStartInfo = new ProcessStartInfo(Path.Combine(gacUtilPath, ""), "/" + arg + " " + fileName + @" UNINSTALL_KEY eXpandFramework ""eXpandFramework""") { WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase, UseShellExecute = false, RedirectStandardOutput = true };
                            var process = Process.Start(processStartInfo);
                            Debug.Assert(process != null, "process != null");
                            var readToEnd = process.StandardOutput.ReadToEnd();
                            lock (_locker) {
                                if (!readToEnd.Contains("successfully")) {
                                    Trace.TraceInformation(fileName + "");
                                    Trace.TraceInformation(readToEnd);
                                }
                                else {
                                    string action = options.Mode == Mode.Install ? " installed" : " uninstalled";
                                    Trace.TraceInformation(fileName + action + " succefully");
                                }
                            }
                            process.WaitForExit();

                        }
                        catch (Exception e){
                            error = true;
                            lock (_locker){
                                var foregroundColor = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Trace.TraceError("ERROR in " + Path.GetFileNameWithoutExtension(file));
                                Trace.TraceError(e.ToString());
                                Console.ForegroundColor = foregroundColor;
                            }
                        }
                    });
                    tasks.Add(task);
                }
            }
            Task.WaitAll(tasks.ToArray());
            if (error)
                Console.ReadKey();
        }

        private static string LocateGacUtil(){
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var windowsPath = Path.Combine(programFiles,@"Microsoft SDKs\Windows");
            foreach (var directory in Directory.GetDirectories(windowsPath).OrderByDescending(s => s)){
                var binDir = Path.Combine(directory,"bin");
                if (Directory.Exists(binDir)){
                    var gacUtilPath = Directory.GetFiles(binDir,"gacutil.exe",SearchOption.AllDirectories).FirstOrDefault();
                    if (gacUtilPath != null)
                        return gacUtilPath;
                }
            }
            throw new ArgumentException("GacUtil not found");
        }

        static IEnumerable<string> GetFiles() {
            return Directory.GetFiles(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "*.dll").Where(IsSigned);
        }

        private static bool IsSigned(string s){
            try{
                return Assembly.ReflectionOnlyLoadFrom(s).GetName().GetPublicKeyToken().Length>0;
            }
            catch (BadImageFormatException){
                return false;
            }
        }
    }
}