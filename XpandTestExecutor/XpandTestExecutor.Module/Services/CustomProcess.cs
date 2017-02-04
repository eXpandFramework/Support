using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Persistent.Base;
using Xpand.Utils.Threading;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Services{
    internal class CustomProcess:Process{
        private readonly bool _rdc;
        private readonly bool _debugMode;
        private readonly EasyTest _easyTest;
        private readonly WindowsUser _windowsUser;
        private NamedPipeServerStream _serverStream;

        public CustomProcess(EasyTest easyTest, WindowsUser windowsUser, bool rdc, bool debugMode){
            _debugMode = debugMode;
            _easyTest = easyTest;
            _rdc = rdc;
            _windowsUser = windowsUser;
        }

        public void Start(CancellationToken token, int timeout){
            if (_rdc){
                Tracing.Tracer.LogValue("StartServerStream", _windowsUser.Name);
                _serverStream = new NamedPipeServerStream(_windowsUser.Name, PipeDirection.InOut, 1);
                Task.Factory.StartNew(() => StartClient(token,timeout), token,TaskCreationOptions.AttachedToParent,TaskScheduler.Current).TimeoutAfter(timeout).ContinueWith(
                    task =>Tracing.Tracer.LogValue("StartClient-Timeout",_windowsUser.Name),token);
                Tracing.Tracer.LogValue("WaitForConnection", _windowsUser.Name);
                _serverStream.WaitForConnection();
                Tracing.Tracer.LogValue("GetSessionId", _windowsUser.Name);
                var sessionId = GetSessionId();
                StartInfo=CreateStartInfo(sessionId);
            }
            else{
                StartInfo=CreateStartInfo();
            }
            Start();
        }

        private void StartClient(CancellationToken token, int timeout){
            token.ThrowIfCancellationRequested();
            string domain =!string.IsNullOrEmpty(WindowsUser.Domain)? " -d " + WindowsUser.Domain:null;
            var processStartInfo = new ProcessStartInfo("RDClient.exe",
                "-u " + _windowsUser.Name + " -p " + _windowsUser.Password +" -m "+timeout+ domain){
                    FileName = "RDClient.exe",
                    CreateNoWindow = true,WorkingDirectory = Path.GetDirectoryName(_easyTest.FileName)+""
                };
            var rdClientProcess = new Process {
                StartInfo = processStartInfo
            };
            rdClientProcess.Start();
            rdClientProcess.WaitForExit(timeout);
        }

        private int GetSessionId(){
            var streamString = new StreamString(_serverStream);
            return Convert.ToInt32(streamString.ReadString());
        }

        public void CloseRDClient(int timeout){
            if (_serverStream != null){
                var wait = Task.Factory.StartNew(() =>{
                    var streamString = new StreamString(_serverStream);
                    streamString.WriteString(true.ToString());
                    _serverStream.WaitForPipeDrain();
                    _serverStream.Close();
                    _serverStream.Dispose();
                }).Wait(timeout);
                if (!wait){
                    Tracing.Tracer.LogValue("RDClient-_serverStream", _easyTest.Name);
                }
            }
        }


        private ProcessStartInfo CreateStartInfo(int sessionId=0){
            var workingDirectory = Path.GetDirectoryName(_easyTest.FileName) + "";
            var executorWrapper = "executorwrapper.exe";
            var testExecutor = $"TestExecutor.v{AssemblyInfo.VersionShort}.exe";
            var debugModeArgs = _debugMode ? @""" -d:""" : null;
            var testExecutorArgs =@""""+Path.Combine(workingDirectory,_easyTest.FileName)+@"""";
            var arguments =$"/accepteula -u {WindowsUser.Domain}\\{_windowsUser.Name} -p {_windowsUser.Password} -w {@"""" + workingDirectory + @""""} -h -i {sessionId} {@"""" + Path.Combine(workingDirectory, executorWrapper) + @""" " + testExecutor + " " + testExecutorArgs + debugModeArgs}";
            return new ProcessStartInfo {
                WorkingDirectory = workingDirectory,
                FileName = _rdc ? "psexec" : testExecutor,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        public class StreamString{
            private readonly Stream _ioStream;
            private readonly UnicodeEncoding _streamEncoding;

            public StreamString(Stream ioStream){
                _ioStream = ioStream;
                _streamEncoding = new UnicodeEncoding();
            }

            public string ReadString(){
                var len = _ioStream.ReadByte()*256;
                len += _ioStream.ReadByte();
                var inBuffer = new byte[len];
                _ioStream.Read(inBuffer, 0, len);

                return _streamEncoding.GetString(inBuffer);
            }

            public int WriteString(string outString){
                var outBuffer = _streamEncoding.GetBytes(outString);
                var len = outBuffer.Length;
                if (len > ushort.MaxValue){
                    len = ushort.MaxValue;
                }
                _ioStream.WriteByte((byte) (len/256));
                _ioStream.WriteByte((byte) (len & 255));
                _ioStream.Write(outBuffer, 0, len);
                _ioStream.Flush();

                return outBuffer.Length + 2;
            }
        }
    }
}