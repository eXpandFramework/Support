using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.EasyTest.Framework;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Services{
    struct TestData{
        public IDataLayer DataLayer;
        public bool DebugMode;
        public Guid EasyTestExecutionInfoKey { get; set; }
        public string UserName { get;  set; }
    }

    public class TestExecutor{
        public const string EasyTestUsersDir = "EasyTestUsers";

        private CancellationToken _cancellationToken;
        private List<Task> _tasks;

        private TestExecutor(CancellationToken cancellationToken){
            _cancellationToken = cancellationToken;
        }

        void Execute(string[] easyTestFiles, bool debugMode, int retries){
            easyTestFiles = easyTestFiles.Take(easyTestFiles.Length).ToArray();
            using (var xpObjectSpaceProvider =new XPObjectSpaceProvider(new ConnectionStringDataStoreProvider(ApplicationHelper.Instance.Application.ConnectionString),true)){
                using (var dataLayer = xpObjectSpaceProvider.CreateObjectSpace().Session().DataLayer){
                    using (var unitOfWork = new UnitOfWork(dataLayer)){
                        var easyTests = unitOfWork.Query<EasyTest>().Where(test => easyTestFiles.Contains(test.FileName)).ToArray();
                        var executionInfo = ExecutionInfo.Create(unitOfWork, true, retries);
                        TestEnviroment.Setup(easyTests, executionInfo);
                        RunTests(debugMode, executionInfo.Oid, dataLayer, easyTestFiles);
                        Tracing.Tracer.LogText($"Execution {executionInfo.Sequence} Finished");
                    }
                }
            }
        }

        private void RunTests(bool debugMode, Guid executionInfoKey, IDataLayer dataLayer, string[] easyTestFiles){
            var runningUsers = new List<string>();
            _tasks = CreateTasks(debugMode, executionInfoKey, dataLayer, easyTestFiles, runningUsers);
            while (_tasks.Count > 0){
                _cancellationToken.ThrowIfCancellationRequested();
                var i = Task.WaitAny(_tasks.ToArray());
                var task = (Task<TestData>)_tasks[i];
                if (task.Exception != null)
                    throw task.Exception;
                runningUsers.Remove(task.Result.UserName);
                _tasks.Remove(task);
                _tasks.AddRange(CreateTasks(debugMode, executionInfoKey, dataLayer, easyTestFiles, runningUsers));
            }
        }

        private List<Task> CreateTasks(bool debugMode, Guid executionInfoKey, IDataLayer dataLayer, string[] easyTestFiles,
            List<string> runningUsers){
            var tasks = new List<Task>();
            using (var unitOfWork = new UnitOfWork(dataLayer)){
                var executionInfo = unitOfWork.GetObjectByKey<ExecutionInfo>(executionInfoKey, true);
                executionInfo.SetEasyTests(easyTestFiles);
                var tests = executionInfo.TestsToExecute.Take(executionInfo.WindowsUsers.Count - runningUsers.Count).ToArray();

                foreach (var easyTest in tests){
                    var windowsUser = GetNextWindowsUser(easyTest, executionInfo, runningUsers);
                    var easyTestExecutionInfo = easyTest.CreateExecutionInfo(executionInfo, windowsUser);
                    var task = CreateTask(debugMode, easyTestExecutionInfo.Oid, dataLayer);
                    tasks.Add(task);
                }
                executionInfo.SetEasyTests(new string[0]);
            }
            return tasks;
        }

        private void AfterTestRun(EasyTestExecutionInfo easyTestExecutionInfo){
            UpdateState(easyTestExecutionInfo);
            Tracing.Tracer.LogValue(easyTestExecutionInfo,"LogOffUser " + easyTestExecutionInfo.WindowsUser.Name);
            EnviromentExEx.LogOffUser(easyTestExecutionInfo.WindowsUser.Name);
            Thread.Sleep(20000);
            try{
                easyTestExecutionInfo.Unlink();
            }
            catch {
            }
        }

        private WindowsUser GetNextWindowsUser(EasyTest easyTest, ExecutionInfo executionInfo, List<string> runningUsers){
            var usedUsers = executionInfo.EasyTestExecutionInfos.Where(info => info.EasyTest.Oid==easyTest.Oid).Select(info => info.WindowsUser);
            var availableUser = executionInfo.WindowsUsers.Where(user => !runningUsers.Contains(user.Name)).OrderBy(user => usedUsers.Count(windowsUser => user.Oid==windowsUser.Oid)).First();
            runningUsers.Add(availableUser.Name);
            return availableUser;
        }

        private Task<TestData> CreateTask(bool debugMode, Guid easyTestExecutionInfoKey, IDataLayer dataLayer){
            var testData = new TestData{
                EasyTestExecutionInfoKey = easyTestExecutionInfoKey,
                DataLayer = dataLayer,
                DebugMode = debugMode
            };
            var task = new Task<TestData>(o => RunTest((TestData) o), testData, _cancellationToken,TaskCreationOptions.AttachedToParent|TaskCreationOptions.LongRunning);
            task.Start();
            return task;
        }

        private TestData RunTest(TestData testData){
            _cancellationToken.ThrowIfCancellationRequested();
            Tracing.Tracer.LogText(nameof(RunTest));
            using (var unitOfWork = new UnitOfWork(testData.DataLayer)){
                var easyTestExecutionInfo =unitOfWork.GetObjectByKey<EasyTestExecutionInfo>(testData.EasyTestExecutionInfoKey);
                testData.UserName = easyTestExecutionInfo.WindowsUser.Name;
                Tracing.Tracer.LogValue(easyTestExecutionInfo, "RunTest");
                using (var process = new CustomProcess(easyTestExecutionInfo, testData.DebugMode)){
                    try{
                        easyTestExecutionInfo.Setup();
                        var timeout = easyTestExecutionInfo.EasyTest.Options.DefaultTimeout * 1000 * 60;
                        process.Start(timeout);
                    }
                    catch (Exception e){
                        Tracing.Tracer.LogErrors(e, easyTestExecutionInfo);
                    }
                    process.CloseRDClient();
                }


                AfterTestRun(easyTestExecutionInfo);

                TraceExecutionInfo(easyTestExecutionInfo.ExecutionInfo);

                Tracing.Tracer.LogValue(easyTestExecutionInfo,  "runtest out");
            }
            
            return testData;
        }

        private void UpdateState(EasyTestExecutionInfo easyTestExecutionInfo){
            try{
                var easyTest = easyTestExecutionInfo.EasyTest;
                var directoryName = Path.GetDirectoryName(easyTest.FileName) + "";
                CopyXafLogs(directoryName);
                var logTests = easyTest.GetLogTests();
                var state = EasyTestState.Passed;
                if (!logTests.Any()){
                    state = EasyTestState.Failed;
                    easyTestExecutionInfo.LogNotExists = true;
                }
                else{
                    if (logTests.Where(test => test.Result!="Ignored").Any(test => test.Result!=EasyTestState.Passed.ToString()))
                        state=EasyTestState.Failed;
                }
                Tracing.Tracer.LogValue(easyTestExecutionInfo, "Update State=" + state);
                easyTestExecutionInfo.Update(state);
                easyTest.Session.ValidateAndCommitChanges();
                easyTest.IgnoreApplications(logTests, easyTestExecutionInfo.WindowsUser.Name);

                Tracing.Tracer.LogValue(easyTestExecutionInfo,  "AfterTestRun Out");
            }
            catch (Exception e){
                Tracing.Tracer.LogErrors(e, easyTestExecutionInfo);
            }
        }

        private static void TraceExecutionInfo( ExecutionInfo executionInfo){
            Tracing.Tracer.LogValue("executionInfo.AvailableUsers", executionInfo.AvailableUsers.Count);
            Tracing.Tracer.LogValue("executionInfo.EasyTests", executionInfo.EasyTests.Count);
            Tracing.Tracer.LogValue("executionInfo.FinishedTests", executionInfo.FinishedTests.Count);
            Tracing.Tracer.LogValue("executionInfo.RunningTests", executionInfo.RunningTests.Count);
        }


        private void CopyXafLogs(string directoryName){
            var fileName = Path.Combine(directoryName, "config.xml");
            using (var optionsStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)){
                var options = new OptionsLoader().Load(optionsStream, null, null, directoryName);
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
