using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using DevExpress.Persistent.Base;
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
                Tracing.Tracer.LogValue(_easyTest, "StartServerStream");
                var pipeName = Guid.NewGuid().ToString();
                _serverStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1);
                StartClient(token, timeout, pipeName);
//                Task.Factory.StartNew(() => StartClient(token,timeout), token,TaskCreationOptions.AttachedToParent,TaskScheduler.Current);
                Tracing.Tracer.LogValue( _easyTest, "WaitForConnection");
                _serverStream.WaitForConnection();
                Tracing.Tracer.LogValue(_easyTest, "GetSessionId");
                var sessionId = GetSessionId();
                StartInfo=CreateStartInfo(sessionId);
            }
            else{
                StartInfo=CreateStartInfo();
            }
            Start();
        }

        protected override void Dispose(bool disposing){
            base.Dispose(disposing);
            if (_serverStream != null){
                if (_serverStream.IsConnected)
                    _serverStream.Disconnect();
                _serverStream.Close();
            }
        }

        private void StartClient(CancellationToken token, int timeout, string pipeName){
            token.ThrowIfCancellationRequested();
            string domain =!string.IsNullOrEmpty(WindowsUser.Domain)? " -d " + WindowsUser.Domain:null;
            var processStartInfo = new ProcessStartInfo("RDClient.exe",
                " -n "+pipeName+" -u " + _windowsUser.Name + " -p " + _windowsUser.Password +" -m "+timeout+ domain){
                    FileName = "RDClient.exe",
                    CreateNoWindow = true,WorkingDirectory = Path.GetDirectoryName(_easyTest.FileName)+""
                };
            var rdClientProcess = new Process {
                StartInfo = processStartInfo
            };
            rdClientProcess.Start();
//            rdClientProcess.WaitForExit(timeout);
        }

        private int GetSessionId(){
            var streamString = new StreamString(_serverStream);
            return Convert.ToInt32(streamString.ReadString());
        }

        public void CloseRDClient(){
            if (_serverStream != null){
                var streamString = new StreamString(_serverStream);
                Tracing.Tracer.LogValue(_easyTest, "WriteString");
                streamString.WriteString(true.ToString());
                Tracing.Tracer.LogValue(_easyTest, "WaitForPipeDrain");
                _serverStream.WaitForPipeDrain();
                Tracing.Tracer.LogValue(_easyTest, "PipeDrain");
                _serverStream.Disconnect();
                _serverStream.Close();
                Tracing.Tracer.LogValue(_easyTest, "Dispose");
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