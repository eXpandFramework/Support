﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExecutorWrapper {
    class Program {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;

        static void Main(string[] args){
            Trace.AutoFlush = true;
            Trace.UseGlobalLock = false;
            var directoryName = Path.GetDirectoryName(typeof(Program).Assembly.Location) + "";
            var streamWriter = File.CreateText(Path.Combine(directoryName, "executorwrapper.log"));
            Trace.Listeners.Add(new TextWriterTraceListener(streamWriter));
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
            Trace.WriteLine(string.Join(" ",args));
            var arguments = @""""+args[1]+@""" "+ (args.Length > 2? args[2]:null);
            var debugMode = arguments.Contains("-d:");
            var processStartInfo = new ProcessStartInfo { FileName = args[0], Arguments =  arguments , UseShellExecute = debugMode, CreateNoWindow = debugMode, RedirectStandardOutput = !debugMode };
            var process = new Process(){StartInfo = processStartInfo};
            process.Start();
            if (!debugMode){
                var readToEnd = process.StandardOutput.ReadToEnd();
                Trace.WriteLine(readToEnd);
            }
            process.WaitForExit();
        }
    }
}
