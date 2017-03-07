using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DevExpress.EasyTest.Framework;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using XpandTestExecutor.Module.BusinessObjects;
using Task = System.Threading.Tasks.Task;

namespace XpandTestExecutor.Module.Services{
    struct TestData{
        public IDataLayer DataLayer;
        public bool DebugMode;
        public Guid EasyTestExecutionInfoKey { get; set; }
    }
    public class TestExecutor{
        public const string EasyTestUsersDir = "EasyTestUsers";
        
        private CancellationToken _cancellationToken;
        private readonly object _locker=new object();

        private TestExecutor(CancellationToken cancellationToken){
            _cancellationToken = cancellationToken;
        }

        void Execute(string[] easyTestFiles, bool debugMode, int retries){
            retries = 0;
            ExecutionInfo executionInfo;
            using (var xpObjectSpaceProvider = new XPObjectSpaceProvider(new ConnectionStringDataStoreProvider(ApplicationHelper.Instance.Application.ConnectionString), true)){
                easyTestFiles = easyTestFiles.ToArray();
                using (var dataLayer = xpObjectSpaceProvider.CreateObjectSpace().Session().DataLayer){
                    int finishedTestsCount;
                    int windowsUsersCount;
                    using (var unitOfWork = new UnitOfWork(dataLayer)){
                        var easyTests = unitOfWork.Query<EasyTest>().Where(test => easyTestFiles.Contains(test.FileName)).ToArray();
                        executionInfo = ExecutionInfo.Create(unitOfWork, true, retries);
                        windowsUsersCount = executionInfo.WindowsUsers.Count;
                        TestEnviroment.Setup(easyTests, executionInfo);
                        finishedTestsCount = executionInfo.FinishedTests.Count;
                    }
                    
                    while (finishedTestsCount != easyTestFiles.Length&&_runningUsers.Count<windowsUsersCount){
                        finishedTestsCount = RunTests(debugMode, executionInfo.Oid, dataLayer, easyTestFiles);
                        Thread.Sleep(5000);
                        _cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
            Tracing.Tracer.LogText($"Execution {executionInfo.Sequence} Finished");
        }
        static readonly Dictionary<string, EasyTest> _runningUsers=new Dictionary<string, EasyTest>();

        private int RunTests(bool debugMode, Guid executionInfoKey, IDataLayer dataLayer, string[] easyTests){
            using (var unitOfWork = new UnitOfWork(dataLayer)){
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey,true);
                executionInfo.SetEasyTests(easyTests);
                var tests = executionInfo.TestsToExecute.Take(executionInfo.WindowsUsers.Count- _runningUsers.Count).ToArray();
                foreach (var easyTest in tests){
                    Guid easyTestExecutionInfoKey;
                    lock (_locker){
                        executionInfo.Reload();
                        executionInfo.EasyTestExecutionInfos.Reload();
                        var windowsUser = GetNextWindowsUser(easyTest, executionInfo);
                        var easyTestExecutionInfo = easyTest.CreateExecutionInfo(executionInfo,windowsUser);
                        
                        easyTest.EasyTestExecutionInfos.Reload();
                        executionInfo.EasyTestExecutionInfos.Reload();
                        easyTestExecutionInfoKey = easyTestExecutionInfo.Oid;
                        _runningUsers.Add(easyTestExecutionInfo.WindowsUser.Name,null);
                    }
                    
                    StartTask(debugMode, easyTestExecutionInfoKey, dataLayer).ContinueWith(task =>{
                        lock (_locker){
                            using (var uow = new UnitOfWork(dataLayer)){
                                var easyTestExecutionInfo = uow.GetObjectByKey<EasyTestExecutionInfo>(easyTestExecutionInfoKey);
                                Tracing.Tracer.LogValue(easyTestExecutionInfo.EasyTest,
                                "LogOffUser " + easyTestExecutionInfo.WindowsUser.Name);
                                EnviromentExEx.LogOffUser(easyTestExecutionInfo.WindowsUser.Name);
                                easyTestExecutionInfo.Unlink();
                                UpdateState(easyTestExecutionInfo);
                                _runningUsers.Remove(easyTestExecutionInfo.WindowsUser.Name);
                            }
                        }
                    }, _cancellationToken);
                    TraceExecutionInfo(easyTests, executionInfo);
                }
                var finishedTestsCount = executionInfo.FinishedTests.Count;
                executionInfo.SetEasyTests(new string[0]);
                return finishedTestsCount;
            }
        }

        private static WindowsUser GetNextWindowsUser(EasyTest easyTest, ExecutionInfo executionInfo){
            var lastWindowsUser = easyTest.GetLastEasyTestExecutionInfo(executionInfo)?.WindowsUser;
            var availableUsers = executionInfo.AvailableUsers.
                OrderByDescending(user => lastWindowsUser != null && user.Oid != lastWindowsUser.Oid).
                ThenByDescending(user =>executionInfo.EasyTestExecutionInfos.Count(info => info.WindowsUser.Oid == user.Oid && info.EasyTest.Oid == easyTest.Oid));

            var windowsUser = availableUsers.First(user => !_runningUsers.ContainsKey(user.Name));
            return windowsUser;
        }


        private Task StartTask(bool debugMode, Guid easyTestExecutionInfoKey, IDataLayer dataLayer){
            return Task.Factory.StartNew(() =>{
                var testData = new TestData{
                    EasyTestExecutionInfoKey = easyTestExecutionInfoKey,
                    DataLayer = dataLayer,
                    DebugMode = debugMode
                };
                RunTest(testData);
            }, _cancellationToken);
        }

        private void RunTest(TestData testData){
            _cancellationToken.ThrowIfCancellationRequested();
            Tracing.Tracer.LogText(nameof(RunTest));
            using (var unitOfWork = new UnitOfWork(testData.DataLayer)){
                var easyTestExecutionInfo = unitOfWork.GetObjectByKey<EasyTestExecutionInfo>(testData.EasyTestExecutionInfoKey);
                var easyTest = easyTestExecutionInfo.EasyTest;
                var executionInfo = easyTestExecutionInfo.ExecutionInfo;
                Tracing.Tracer.LogValue(easyTest, "RunTest");
                var lastEasyTestExecutionInfo = easyTest.GetLastEasyTestExecutionInfo(executionInfo);
                using (var process = new CustomProcess(lastEasyTestExecutionInfo, testData.DebugMode)){
                    try{
                        lastEasyTestExecutionInfo.Setup();
                        var timeout = lastEasyTestExecutionInfo.EasyTest.Options.DefaultTimeout*1000*60;
                        process.Start(timeout);
                        process.WaitForExit(timeout);
                    }
                    catch (Exception e){
                        Tracing.Tracer.LogErrors(e, lastEasyTestExecutionInfo);
                        throw;
                    }
                    finally{
                        process.CloseRDClient();
                    }
                }
            }
        }

        private void UpdateState(EasyTestExecutionInfo easyTestExecutionInfo){
            try{
                var easyTestName = easyTestExecutionInfo.EasyTest.Name + "/" + easyTestExecutionInfo.EasyTest.Application + "/" + easyTestExecutionInfo.WindowsUser;

                var easyTest = easyTestExecutionInfo.EasyTest;
                var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                CopyXafLogs(directoryName);
                var logTests = easyTest.GetLogTests();
                var state = EasyTestState.Passed;
                if (!logTests.Any() || logTests.Any(test => test.Result != "Passed"))
                    state = EasyTestState.Failed;
                Tracing.Tracer.LogValue(easyTest, "Update State=" + state);
                easyTestExecutionInfo.Update(state);
                easyTest.Session.ValidateAndCommitChanges();
                easyTest.IgnoreApplications(logTests, easyTestExecutionInfo.WindowsUser.Name);

                Tracing.Tracer.LogText(easyTestName + "AfterTestRun Out");
            }
            catch (Exception e){
                Tracing.Tracer.LogErrors(e, easyTestExecutionInfo);
                throw;
            }
        }

        private static void TraceExecutionInfo(string[] easyTests, ExecutionInfo executionInfo){
            Tracing.Tracer.LogValue("executionInfo.AvailableUsers", executionInfo.AvailableUsers.Count);
            Tracing.Tracer.LogValue("executionInfo.EasyTests", executionInfo.EasyTests.Count);
            Tracing.Tracer.LogValue("easyTests", easyTests.Length);
            Tracing.Tracer.LogValue("executionInfo.FinishedTests", executionInfo.FinishedTests.Count);
            Tracing.Tracer.LogValue("executionInfo.RunningTests", executionInfo.RunningTests.Count);
        }


        private void CopyXafLogs(string directoryName){
            var fileName = Path.Combine(directoryName, "config.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)){
                var options = Options.LoadOptions(optionsStream, null, null, directoryName);
                foreach (var alias in options.Aliases.Cast<TestAlias>().Where(alias => alias.ContainsAppPath())){
                    var suffix = alias.IsWinAppPath() ? "_win" : "_web";
                    var sourceFileName = Path.Combine(alias.Value, "eXpressAppFramework.log");
                    if (File.Exists(sourceFileName))
                        File.Copy(sourceFileName, Path.Combine(directoryName, "eXpressAppFramework" + suffix + ".log"),
                            true);
                }
            }
        }

        public static void Execute(string[] easyTestFiles, bool isDebug, CancellationToken token, int retries){
            var testExecutor = new TestExecutor(token);
            testExecutor.Execute(easyTestFiles, isDebug,  retries);
        }

        public static void Execute(string easyTestsFile, int retries){
            var easyTests = GetEasyTests(easyTestsFile);
            Execute(easyTests.Select(test => test.FileName).ToArray(), false,CancellationToken.None, retries);
        }

        static EasyTest[] GetEasyTests(string fileName){
            var fileNames = File.ReadAllLines(fileName).Where(s => !string.IsNullOrEmpty(s)).ToArray();
            ApplicationHelper.Instance.Application.ObjectSpaceProvider.UpdateSchema();
            var objectSpace = ApplicationHelper.Instance.Application.ObjectSpaceProvider.CreateObjectSpace();
            OptionsProvider.Init(fileNames);
            var easyTests = EasyTest.GetTests(objectSpace, fileNames);
            objectSpace.Session().ValidateAndCommitChanges();
            return easyTests;
        }

    }
}
