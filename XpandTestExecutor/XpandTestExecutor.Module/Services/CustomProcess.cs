using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using DevExpress.Persistent.Base;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Services{
    internal class CustomProcess:Process{
        private readonly bool _debugMode;
        private readonly EasyTestExecutionInfo _easyTestExecutionInfo;
        private NamedPipeServerStream _serverStream;

        public CustomProcess(EasyTestExecutionInfo easyTestExecutionInfo,  bool debugMode){
            _debugMode = debugMode;
            _easyTestExecutionInfo = easyTestExecutionInfo;
        }

        public void Start( int timeout){
            Tracing.Tracer.LogValue(_easyTestExecutionInfo, "StartServerStream");
            var pipeName = Guid.NewGuid().ToString();
            _serverStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1);
            StartClient(timeout, pipeName);
            Tracing.Tracer.LogValue(_easyTestExecutionInfo, "WaitForConnection");
            int sessionId = 0;
            Task.Factory.StartNew(() => {
                _serverStream.WaitForConnection();
                Tracing.Tracer.LogValue(_easyTestExecutionInfo, "GetSessionId");
                sessionId = GetSessionId();
            }).Wait(10000);
            
            Tracing.Tracer.LogValue(_easyTestExecutionInfo, "CreateStartInfo");
            StartInfo = CreateStartInfo(sessionId);
            Start();
            WaitForExit(timeout);
        }

        private void StartClient(int timeout, string pipeName){
            string domain =!string.IsNullOrEmpty(WindowsUser.Domain)? " -d " + WindowsUser.Domain:null;
            var processStartInfo = new ProcessStartInfo("RDClient.exe",
                " -n "+ pipeName + " -u " + _easyTestExecutionInfo.WindowsUser.Name + " -p " + _easyTestExecutionInfo.WindowsUser.Password +" -m "+timeout+ domain){
                    FileName = "RDClient.exe",
                    CreateNoWindow = true,WorkingDirectory = Path.GetDirectoryName(_easyTestExecutionInfo.EasyTest.FileName)+""
                };
            var rdClientProcess = new Process {
                StartInfo = processStartInfo
            };
            rdClientProcess.Start();
        }

        private int GetSessionId(){
            var streamString = new StreamString(_serverStream);
            return Convert.ToInt32(streamString.ReadString());
        }

        public void CloseRDClient(){
            try{
                Task.Factory.StartNew(CloseClient).Wait(5000);
            }
            catch {
            }
            try{
                _serverStream?.Disconnect();
            }
            catch {
            }
            try{
                _serverStream?.Close();
            }
            catch {
            }
        }

        private void CloseClient(){
            if (_serverStream != null){
                var streamString = new StreamString(_serverStream);
                streamString.WriteString(true.ToString());
                _serverStream.WaitForPipeDrain();
            }
        }

        private ProcessStartInfo CreateStartInfo(int sessionId=0){
            var workingDirectory = Path.GetDirectoryName(_easyTestExecutionInfo.EasyTest.FileName) + "";
            var executorWrapper = "executorwrapper.exe";
            var testExecutor = $"TestExecutor.v{AssemblyInfo.VersionShort}.exe";
            var debugModeArgs = _debugMode ? @""" -d:""" : null;
            var testExecutorArgs =@""""+Path.Combine(workingDirectory,_easyTestExecutionInfo.EasyTest.FileName)+@"""";
            var arguments =
                $"/accepteula -u {WindowsUser.Domain}\\{_easyTestExecutionInfo.WindowsUser.Name} -p {_easyTestExecutionInfo.WindowsUser.Password} -w {@"""" + workingDirectory + @""""} -h -i {sessionId} {@"""" + Path.Combine(workingDirectory, executorWrapper) + @""" " + testExecutor + " " + testExecutorArgs + debugModeArgs}";
            return new ProcessStartInfo {
                WorkingDirectory = workingDirectory,
                FileName = "psexec",
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