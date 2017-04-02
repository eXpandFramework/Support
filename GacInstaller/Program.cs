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
            var tasks = new List<Task<string>>();
            bool error = false;
            foreach (var file in GetFiles()) {
                if (options.Regex == null || Regex.IsMatch(Path.GetFileNameWithoutExtension(file) + "", options.Regex)) {
                    var fileName = options.Mode == Mode.Install ? Path.GetFileName(file) : Path.GetFileNameWithoutExtension(file);
                    string arg = options.Mode == Mode.Install ? "ir" : "ur";
                    var task = new Task<string>(() =>{
                            var keyValuePair = GACUpdate(gacUtilPath, arg, fileName, options, file);
                            error = keyValuePair.Value;
                            return keyValuePair.Key;
                        }
                    );
                    task.Start();
                    tasks.Add(task);
                }
            }
            while (tasks.Count>0){
                var array = tasks.Cast<Task>().ToArray();
                var i = Task.WaitAny(array);
                var task = tasks[i];
                Trace.TraceInformation(task.Result);
                tasks.Remove(task);
            }
            
            if (error)
                Console.ReadKey();
        }

        private static KeyValuePair<string,bool> GACUpdate(string gacUtilPath, string arg, string fileName, Options options, string file){
            bool error = false;
            string msg = null;
            try{
                var processStartInfo = new ProcessStartInfo(Path.Combine(gacUtilPath, ""),
                    "/" + arg + " " + fileName + @" UNINSTALL_KEY eXpandFramework ""eXpandFramework"""){
                    WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                var process = Process.Start(processStartInfo);
                Debug.Assert(process != null, "process != null");
                var readToEnd = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (!readToEnd.Contains("successfully")) {
                    msg = fileName + Environment.NewLine + readToEnd;
                }
                else {
                    string action = options.Mode == Mode.Install ? " installed" : " uninstalled";
                    msg = fileName + action + " succefully";
                }
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
            return new KeyValuePair<string, bool>(msg,error);
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