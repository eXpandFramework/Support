﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommandLine;

namespace RDClient {
    class Program {
        [STAThread]
        static void Main(string[] args){
            Trace.AutoFlush = true;
            Trace.UseGlobalLock = false;
            var directoryName = Path.GetDirectoryName(typeof(Program).Assembly.Location) + "";
            var streamWriter = File.CreateText(Path.Combine(directoryName, "rdclient.log"));
            Trace.Listeners.Add(new TextWriterTraceListener(streamWriter));
            bool arguments = Parser.Default.ParseArguments(args, Options.Instance);
            try{
                if (arguments) {
                    var pipeClient = new NamedPipeClientStream(".", Options.Instance.UserName,
                        PipeDirection.InOut, PipeOptions.None,
                        TokenImpersonationLevel.Impersonation);
                    pipeClient.Connect();
                    var streamString = new StreamString(pipeClient);
                    
                    var rdClient = new RDClient();
                    rdClient.Rdp.OnLoginComplete += (sender, eventArgs) =>{
                        streamString.WriteString(RDSHelper.GetSessionId(Options.Instance.UserName));
                        pipeClient.WaitForPipeDrain();
                        var task = Task.Factory.StartNew(() => {
                            var readString = streamString.ReadString();
                            while (readString != true.ToString()) {
                                readString = streamString.ReadString();
                                Thread.Sleep(5000);
                            }
                        });
                        var waitAll = Task.WaitAll(new [] { task },Options.Instance.TimeOut);
                        if (!waitAll)
                            Trace.WriteLine("Timeout",Options.Instance.UserName);
                        pipeClient.Close();
                        pipeClient.Dispose();
                        rdClient.Close();
                    };
                    Application.Run(rdClient);
                }
                else{
                    throw new ArgumentException(Options.Instance.GetUsage());
                }
            }
            catch (Exception e){
                Trace.WriteLine(e.ToString());
                throw;
            }
            finally{
                Trace.Close();
            }
        }
    }
}
