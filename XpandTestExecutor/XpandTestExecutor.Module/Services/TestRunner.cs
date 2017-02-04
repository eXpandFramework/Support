using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.EasyTest.Framework;
using DevExpress.ExpressApp.Utils;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Utils.Helpers;
using Xpand.Utils.Threading;
using XpandTestExecutor.Module.BusinessObjects;
using XpandTestExecutor.Module.Controllers;

namespace XpandTestExecutor.Module.Services {
    public class TestRunner {
        public const string EasyTestUsersDir = "EasyTestUsers";
        private static readonly object _locker = new object();

        private static bool ExecutionFinished(IDataLayer dataLayer, Guid executionInfoKey, int testsCount) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey, true);
                var finishedCount = executionInfo.FinishedTests.Count;
                var allFinished = finishedCount == testsCount;
                var failedAgain = executionInfo.FailedAgain();
                var ret = allFinished || failedAgain;
                if (ret) {
                    string reason = null;
                    if (allFinished)
                        reason = "allFinished ";
                    if (failedAgain)
                        reason += "failedAgain";
                    Tracing.Tracer.LogText("ExecutionFinished for Seq " + executionInfo.Sequence + " reason:" + reason);
                }
                return ret;
            }
        }

        
        private static void RunTest(Guid easyTestKey, IDataLayer dataLayer, bool rdc, bool debugMode, CancellationToken token) {
            CustomProcess process;
            int timeout;
            string easyTestName;
            lock (_locker) {
                Tracing.Tracer.LogText(nameof(RunTest));
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);


                    timeout = easyTest.Options.DefaultTimeout*60*1000;
                    try {
                        
                        var lastEasyTestExecutionInfo = easyTest.LastEasyTestExecutionInfo;
                        var user = lastEasyTestExecutionInfo.WindowsUser;
                        easyTestName = easyTest.Name + "/" + easyTest.Application+"/"+user;
                        Tracing.Tracer.LogValue("RunTest", easyTestName);
                        easyTest.LastEasyTestExecutionInfo.Setup(false);
                        process = new CustomProcess(easyTest,user,rdc,debugMode);

                        process.Start(token,timeout);
                        
                        lastEasyTestExecutionInfo =
                            unitOfWork.GetObjectByKey<EasyTestExecutionInfo>(lastEasyTestExecutionInfo.Oid, true);
                        lastEasyTestExecutionInfo.Update(EasyTestState.Running);
                        unitOfWork.ValidateAndCommitChanges();

                        Thread.Sleep(2000);
                    }
                    catch (Exception e) {
                        LogErrors(easyTest, e);
                        throw;
                    }
                }
            }
            token.ThrowIfCancellationRequested();


            var complete = process.WaitForExit(timeout);
            if (!complete)
                Tracing.Tracer.LogValue("TimeOut", easyTestName);

            try {
                Tracing.Tracer.LogValue("CloseRDClient", easyTestName);
                process.CloseRDClient(timeout);
                AfterProcessExecute(dataLayer, easyTestKey);
            }
            catch (Exception e) {
                process.CloseRDClient(timeout);
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    LogErrors(easyTest, e);
                }
                throw;
            }
            
        }

        private static void LogErrors(EasyTest easyTest, Exception e) {
            lock (_locker) {
                Tracing.Tracer.LogSeparator("LogErrors");
                Tracing.Tracer.LogError(e);
                LogErrorsCore(easyTest, e);
            }

        }

        private static void LogErrorsCore(EasyTest easyTest, Exception e) {
            Tracing.Tracer.LogSeparator("LogErrorsCore");
            try {
                easyTest.LastEasyTestExecutionInfo.Update(EasyTestState.Failed);
                easyTest.Session.ValidateAndCommitChanges();
                EnviromentEx.LogOffUser(easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                easyTest.LastEasyTestExecutionInfo.Setup(true);
                var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                var logTests = new LogTests();
                foreach (var application in easyTest.Options.Applications.Cast<TestApplication>()) {
                    var logTest = new LogTest { ApplicationName = application.Name, Result = "Failed" };
                    var logError = new LogError { Message = { Text = e.ToString() } };
                    logTest.Errors.Add(logError);
                    logTests.Tests.Add(logTest);
                }
                logTests.Save(Path.Combine(directoryName, "TestsLog.xml"));

            }
            catch (Exception exception) {
                Tracing.Tracer.LogError(exception);
                throw;
            }
        }
        private static void AfterProcessExecute(IDataLayer dataLayer, Guid easyTestKey) {
            lock (_locker) {
                using (var unitOfWork = new UnitOfWork(dataLayer)) {
                    var easyTest = unitOfWork.GetObjectByKey<EasyTest>(easyTestKey, true);
                    Tracing.Tracer.LogValue("In AfterProcessExecute", easyTest.Name + "/" + easyTest.Application);
                    var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                    CopyXafLogs(directoryName);
                    var logTests = easyTest.GetLogTests();
                    var state = EasyTestState.Passed;
                    if (logTests.Any(test => test.Result != "Passed")) {
                        state = EasyTestState.Failed;
                    }
                    Tracing.Tracer.LogValue("State", state.ToString());
                    Tracing.Tracer.LogValue("Update", easyTest.Name + "/" + easyTest.Application);
                    easyTest.LastEasyTestExecutionInfo.Update(state);
                    easyTest.Session.ValidateAndCommitChanges();
                    if (easyTest.LastEasyTestExecutionInfo.ExecutedFromOtherUser()) {
                        EnviromentEx.LogOffUser(easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                        Tracing.Tracer.LogValue("LogOffUser", easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                        easyTest.LastEasyTestExecutionInfo.Setup(true);
                        easyTest.IgnoreApplications(logTests,easyTest.LastEasyTestExecutionInfo.WindowsUser.Name);
                        Tracing.Tracer.LogValue("Execution", "Reset");
                    }
                    Tracing.Tracer.LogValue("Out AfterProcessExecute", easyTest.Name+"/"+easyTest.Application);
                }
            }
        }

        private static void CopyXafLogs(string directoryName) {
            string fileName = Path.Combine(directoryName, "config.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Options options = Options.LoadOptions(optionsStream, null, null, directoryName);
                foreach (var alias in options.Aliases.Cast<TestAlias>().Where(alias => alias.ContainsAppPath())) {
                    var suffix = alias.IsWinAppPath() ? "_win" : "_web";
                    var sourceFileName = Path.Combine(alias.Value, "eXpressAppFramework.log");
                    if (File.Exists(sourceFileName)) {
                        File.Copy(sourceFileName, Path.Combine(directoryName, "eXpressAppFramework" + suffix + ".log"), true);
                    }
                }
            }
        }

        public static void Execute(string fileName, bool rdc) {
            var easyTests = GetEasyTests(fileName);
            Execute(easyTests, rdc,false, task => { });
        }

        public static EasyTest[] GetEasyTests(string fileName) {
            var fileNames = File.ReadAllLines(fileName).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            ApplicationHelper.Instance.Application.ObjectSpaceProvider.UpdateSchema();
            var objectSpace = ApplicationHelper.Instance.Application.ObjectSpaceProvider.CreateObjectSpace();
            OptionsProvider.Init(fileNames);
            var easyTests = EasyTest.GetTests(objectSpace, fileNames);
            objectSpace.Session().ValidateAndCommitChanges();
            return easyTests;
        }

        public static CancellationTokenSource Execute(EasyTest[] easyTests, bool rdc, bool debugMode, Action<Task> continueWith ) {
            Tracing.Tracer.LogValue("EasyTests.Count", easyTests.Length);
            if (easyTests.Any()) {
                TestEnviroment.Setup(easyTests);
                var tokenSource = new CancellationTokenSource();
                // ReSharper disable once MethodSupportsCancellation
                Task.Factory.StartNew(() => ExecuteCore(easyTests, rdc,  tokenSource.Token,debugMode),tokenSource.Token,TaskCreationOptions.AttachedToParent, TaskScheduler.Current).ContinueWith(task =>{
                    Tracing.Tracer.LogText("Main thread finished");
                    continueWith(task);
                });
                Thread.Sleep(100);
                return tokenSource;
            }
            return null;
        }

        private static void LastCleanup(EasyTest[] easyTests, CancellationToken tokenSourceToken){
            try{
                if (tokenSourceToken.IsCancellationRequested){
                    var dataLayer = GetDatalayer();
                    var unitOfWork = new UnitOfWork(dataLayer);
                    var sequence = unitOfWork.Query<ExecutionInfo>().Max(info => info.Sequence);
                    var executionInfo = unitOfWork.Query<ExecutionInfo>().First(info => info.Sequence == sequence);
                    var users = executionInfo.EasyTestExecutionInfos.Select(info => info.WindowsUser?.Name).Where(s => s != null).Distinct().ToArray();
                    EnviromentEx.LogOffAllUsers(users);
                    
//                    TestEnviroment.KillRDClient();
                    unitOfWork.Delete(executionInfo);
                    unitOfWork.CommitChanges();
                }
            }
            catch (Exception e){
                Tracing.Tracer.LogText(nameof(LastCleanup));
                Tracing.Tracer.LogError(e);
                throw;
            }
            TestEnviroment.Cleanup(easyTests);
        }

        private static void ExecuteCore(EasyTest[] easyTests, bool rdc, CancellationToken token, bool debugMode) {
            string fileName = null;
            try {
                var dataLayer = GetDatalayer();
                var executionInfoKey = CreateExecutionInfoKey(dataLayer, rdc, easyTests);
                do {
                    token.ThrowIfCancellationRequested();
                    var easyTest = GetNextEasyTest(executionInfoKey, easyTests, dataLayer, rdc);
                    if (easyTest != null) {
                        fileName = easyTest.FileName;
                        Task.Factory.StartNew(() => RunTest(easyTest.Oid, dataLayer, rdc,debugMode,token),token, TaskCreationOptions.AttachedToParent,TaskScheduler.Current).TimeoutAfter(easyTest.Options.DefaultTimeout*60*1000);
                    }
                    Thread.Sleep(2000);
                } while (!ExecutionFinished(dataLayer, executionInfoKey, easyTests.Length));
            }
            catch (Exception e) {
                LastCleanup(easyTests, token);
                Tracing.Tracer.LogError(new Exception("ExecutionCore Exception on " + fileName,e));
                throw;
            }
        }

        private static IDataLayer GetDatalayer() {
            var xpObjectSpaceProvider = new XPObjectSpaceProvider(new ConnectionStringDataStoreProvider(ApplicationHelper.Instance.Application.ConnectionString), true);
            return xpObjectSpaceProvider.CreateObjectSpace().Session().DataLayer;
        }

        private static Guid CreateExecutionInfoKey(IDataLayer dataLayer, bool rdc, EasyTest[] easyTests) {
            Guid executionInfoKey;
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = ExecutionInfo.Create(unitOfWork, rdc, ((IModelOptionsTestExecutor)CaptionHelper.ApplicationModel.Options).ExecutionRetries);
                if (rdc)
                    EnviromentEx.LogOffAllUsers(executionInfo.WindowsUsers.Select(user => user.Name).ToArray());
                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();
                foreach (var easyTest in easyTests) {
                    easyTest.CreateExecutionInfo(rdc, executionInfo);
                }
                unitOfWork.ValidateAndCommitChanges();
                CurrentSequenceOperator.CurrentSequence = executionInfo.Sequence;
                Tracing.Tracer.LogText("CurrentSequence", CurrentSequenceOperator.CurrentSequence);
                executionInfoKey = executionInfo.Oid;
            }
            return executionInfoKey;
        }

        private static EasyTest GetNextEasyTest(Guid executionInfoKey, EasyTest[] easyTests, IDataLayer dataLayer, bool rdc) {
            using (var unitOfWork = new UnitOfWork(dataLayer)) {
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey);

                easyTests = easyTests.Select(test => unitOfWork.GetObjectByKey<EasyTest>(test.Oid)).ToArray();
                var runningInfosCount = executionInfo.EasyTestRunningInfos.Count();
                if (runningInfosCount < executionInfo.WindowsUsers.Count()) {
                    var easyTest = GetFirstRunEasyTest(easyTests, executionInfo, rdc) ?? GetFailedEasyTest(easyTests, executionInfo, rdc);
                    if (easyTest != null) {
                        easyTest.LastEasyTestExecutionInfo.State = EasyTestState.Running;
                        easyTest.Session.ValidateAndCommitChanges();
                        return easyTest;
                    }
                }
            }
            return null;
        }

        private static EasyTest GetFailedEasyTest(EasyTest[] easyTests, ExecutionInfo executionInfo, bool useCustomPort) {
            for (int i = 0; i < executionInfo.Retries; i++) {
                var easyTest = GetEasyTest(easyTests, executionInfo, i + 1);
                if (easyTest != null) {
                    var windowsUser = executionInfo.GetNextUser(easyTest);
                    if (windowsUser != null) {
                        easyTest.CreateExecutionInfo(useCustomPort, executionInfo, windowsUser);
                        return easyTest;
                    }
                    return null;
                }
            }
            return null;
        }

        private static EasyTest GetFirstRunEasyTest(IEnumerable<EasyTest> easyTests, ExecutionInfo executionInfo, bool useCustomPort) {
            var easyTest = GetEasyTest(easyTests, executionInfo, 0);
            if (easyTest != null) {
                var windowsUser = executionInfo.GetNextUser(easyTest);
                if (windowsUser != null) {
                    if (executionInfo.FinishedTests.Contains(easyTest.LastEasyTestExecutionInfo.EasyTest))
                        easyTest.CreateExecutionInfo(useCustomPort, executionInfo, windowsUser);
                    easyTest.LastEasyTestExecutionInfo.WindowsUser = windowsUser;
                    
                    return easyTest;
                }
            }
            return null;
        }

        private static EasyTest GetEasyTest(IEnumerable<EasyTest> easyTests, ExecutionInfo executionInfo, int i) {
            return executionInfo.GetTestsToExecute(i).FirstOrDefault(easyTests.Contains);
        }


        public static void Execute(string fileName, bool rdc, Action<Task> continueWith, bool debugMode) {
            var easyTests = GetEasyTests(fileName);
            Execute(easyTests, rdc, debugMode,continueWith);
        }
    }
}