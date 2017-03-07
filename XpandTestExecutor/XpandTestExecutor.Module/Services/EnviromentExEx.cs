using System;
using System.Linq;
using System.Threading;
using Cassia;
using Xpand.Utils.Helpers;

namespace XpandTestExecutor.Module.Services{
    public class EnviromentExEx{
        public static void LogOffUser(string userName){
            EnviromentEx.LogOffUser(userName);
//            var isLoggedIn = IsLoggedIn(userName);
//            while (IsLoggedIn(userName)){
//                Thread.Sleep(2000);
//            }
        }

        public static bool IsLoggedIn(string userName){
            ITerminalServicesManager manager = new TerminalServicesManager();
            bool isLoggedIn;
            using (var server = manager.GetRemoteServer(Environment.MachineName)){
                server.Open();
                isLoggedIn = server.GetSessions().Any(session => session.UserName == userName);
            }
            return isLoggedIn;
        }

    }
}
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Runtime.InteropServices;
//using System.Threading;
//using DevExpress.Persistent.Base;
//using Xpand.Utils.Helpers;
//
//namespace XpandTestExecutor.Module.Services{
//    internal class EnviromentExEx{
//        public static void LogOffUser(string windowsUserName){
//            Tracing.Tracer.LogValue("LogOffUser",windowsUserName);
//            EnviromentEx.LogOffUser(windowsUserName);
//            while (ListUsers(Environment.MachineName).Contains(windowsUserName))
//                Thread.Sleep(2000);
//        }
//
//        public enum WTS_CONNECTSTATE_CLASS{
//            WTSActive,
//            WTSConnected,
//            WTSConnectQuery,
//            WTSShadow,
//            WTSDisconnected,
//            WTSIdle,
//            WTSListen,
//            WTSReset,
//            WTSDown,
//            WTSInit
//        }
//
//        public enum WTS_INFO_CLASS{
//            WTSInitialProgram,
//            WTSApplicationName,
//            WTSWorkingDirectory,
//            WTSOEMId,
//            WTSSessionId,
//            WTSUserName,
//            WTSWinStationName,
//            WTSDomainName,
//            WTSConnectState,
//            WTSClientBuildNumber,
//            WTSClientName,
//            WTSClientDirectory,
//            WTSClientProductId,
//            WTSClientHardwareId,
//            WTSClientAddress,
//            WTSClientDisplay,
//            WTSClientProtocolType
//        }
//
//        [DllImport("wtsapi32.dll")]
//        private static extern IntPtr WTSOpenServer([MarshalAs(UnmanagedType.LPStr)] string pServerName);
//
//        [DllImport("wtsapi32.dll")]
//        private static extern void WTSCloseServer(IntPtr hServer);
//
//        [DllImport("wtsapi32.dll")]
//        private static extern int WTSEnumerateSessions(
//            IntPtr hServer,
//            [MarshalAs(UnmanagedType.U4)] int reserved,
//            [MarshalAs(UnmanagedType.U4)] int version,
//            ref IntPtr ppSessionInfo,
//            [MarshalAs(UnmanagedType.U4)] ref int pCount);
//
//        [DllImport("wtsapi32.dll")]
//        private static extern void WTSFreeMemory(IntPtr pMemory);
//
//        [DllImport("Wtsapi32.dll")]
//        private static extern bool WTSQuerySessionInformation(
//            IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out uint pBytesReturned);
//
//
//        public static IntPtr OpenServer(string name){
//            var server = WTSOpenServer(name);
//            return server;
//        }
//
//        public static void CloseServer(IntPtr serverHandle){
//            WTSCloseServer(serverHandle);
//        }
//
////        public static IEnumerable<string> ListUsers(){
////            return ListUsers(Environment.MachineName);
////        }
//
//        public static IEnumerable<string> ListUsers(string serverName){
//            var serverHandle = OpenServer(serverName);
//
//            try{
//                var sessionInfoPtr = IntPtr.Zero;
//                var sessionCount = 0;
//                var retVal = WTSEnumerateSessions(serverHandle, 0, 1, ref sessionInfoPtr, ref sessionCount);
//                var dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
//                var currentSession = (int) sessionInfoPtr;
//
//                if (retVal != 0){
//                    for (var i = 0; i < sessionCount; i++){
//                        var si =
//                            (WTS_SESSION_INFO) Marshal.PtrToStructure((IntPtr) currentSession, typeof(WTS_SESSION_INFO));
//                        currentSession += dataSize;
//
//                        IntPtr userPtr;
//                        uint bytes;
//                        WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.WTSUserName, out userPtr,
//                            out bytes);
//                        IntPtr domainPtr;
//                        WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.WTSDomainName,
//                            out domainPtr, out bytes);
//
//                        yield return Marshal.PtrToStringAnsi(userPtr);
//
//                        WTSFreeMemory(userPtr);
//                        WTSFreeMemory(domainPtr);
//                    }
//
//                    WTSFreeMemory(sessionInfoPtr);
//                }
//            }
//            finally{
//                CloseServer(serverHandle);
//            }
//        }
//
//        [StructLayout(LayoutKind.Sequential)]
//        public struct WTS_SESSION_INFO{
//            public readonly int SessionID;
//
//            [MarshalAs(UnmanagedType.LPStr)]
//            public readonly string pWinStationName;
//
//            public readonly WTS_CONNECTSTATE_CLASS State;
//        }
//    }
//}