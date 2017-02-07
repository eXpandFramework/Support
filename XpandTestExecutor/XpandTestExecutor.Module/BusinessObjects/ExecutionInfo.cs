using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.CustomAttributes;
using XpandTestExecutor.Module.Services;

namespace XpandTestExecutor.Module.BusinessObjects {
    [DefaultProperty("Sequence")]
    [FriendlyKeyProperty("Sequence")]
    public class ExecutionInfo : BaseObject, ISupportSequenceObject {
        private int _retries;
        private DateTime _creationDate;

        public ExecutionInfo(Session session)
            : base(session) {
        }

        public int Duration => EasyTestExecutionInfos.Duration();

        [Association("ExecutionInfos-Users")]
        public XPCollection<WindowsUser> WindowsUsers => GetCollection<WindowsUser>("WindowsUsers");

        [Association("EasyTestExecutionInfo-ExecutionInfos")]
        public XPCollection<EasyTestExecutionInfo> EasyTestExecutionInfos => GetCollection<EasyTestExecutionInfo>("EasyTestExecutionInfos");


        public XPCollection<EasyTest> FinishedTests {
            get{
                var passedTests = PassedEasyTests.Distinct();
                return new XPCollection<EasyTest>(Session, FailedTests.Concat(passedTests));
            }
        }
        public XPCollection<EasyTest> EasyTests {
            get{
                return new XPCollection<EasyTest>(Session, EasyTestExecutionInfos.Select(info => info.EasyTest));
            }
        }
        public XPCollection<EasyTest> RunningTests {
            get{
                var runningInfos = EasyTestExecutionInfos.GroupBy(info => info.EasyTest).Where(infos => infos.Count(info => info.State==EasyTestState.Running)==1).Select(infos => infos.Key);
                return new XPCollection<EasyTest>(Session, runningInfos.Except(FailedTests).Except(PassedEasyTests));
            }
        }

        [VisibleInListView(false)]
        public DateTime CreationDate {
            get { return _creationDate; }
            set { SetPropertyValue("CreationDate", ref _creationDate, value); }
        }

        [InvisibleInAllViews]
        public XPCollection<EasyTest> PassedEasyTests {
            get {
                return new XPCollection<EasyTest>(Session,
                    EasyTestExecutionInfos.Where(info => info.State==EasyTestState.Passed).Select(info => info.EasyTest));
            }
        }


        [InvisibleInAllViews]
        public bool Failed => FailedTests.Any();

        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix => "";

        public IEnumerable<EasyTest> FailedTests{
            get{
                return EasyTestExecutionInfos.GroupBy(info => info.EasyTest)
                        .Where(Failure)
                        .Select(infos => infos.Key);
            }
        }

        private bool Failure(IGrouping<EasyTest, EasyTestExecutionInfo> infos){
            return infos.Count() >= Retries;
        }


        protected override void OnSaving() {
            base.OnSaving();
            SequenceGenerator.GenerateSequence(this);
        }

        [InvisibleInAllViews]
        public int Retries{
            get { return _retries; }
            set { SetPropertyValue("Retries", ref _retries, value); }
        }

        public static ExecutionInfo Create(UnitOfWork unitOfWork, bool rdc,int retries) {
            IEnumerable<WindowsUser> windowsUsers = WindowsUser.CreateUsers(unitOfWork, rdc);
            var executionInfo = new ExecutionInfo(unitOfWork){Retries = retries};
            executionInfo.WindowsUsers.AddRange(windowsUsers);
            return executionInfo;
        }

        public override void AfterConstruction() {
            base.AfterConstruction();
            CreationDate = DateTime.Now;
        }

        public EasyTest[] GetTestsToExecute(int retries){
            return GetTestsToExecuteCore(retries).ToArray();
        }

        public IEnumerable<EasyTest> LastExecutionFailures(){
            var sequence = Session.Query<ExecutionInfo>().Where(info => info.Sequence<Sequence).Max(info => info.Sequence);
            var executionInfo = Session.Query<ExecutionInfo>().First(info => info.Sequence==sequence);
            return executionInfo.PassedEasyTests;
        }


        private EasyTest[] GetTestsToExecuteCore(int retries){
            if (retries == 0){
                var lastExecutionFailures = LastExecutionFailures().ToArray();
                if (lastExecutionFailures.Any()){
                    if (FinishedTests.Count < lastExecutionFailures.Length){
                        var tests = lastExecutionFailures.Except(FinishedTests).Except(RunningTests);
                        var easyTests = tests.Except(PassedEasyTests);
                        return easyTests.ToArray();
                    }
                }
                var firstRunEasyTests =
                    GetFirstRunEasyTests().Select(test => new { Test = test, Duration = test.LastPassedDuration() });
                return firstRunEasyTests.Select(arg => arg.Test).ToArray();
            }
            Tracing.Tracer.LogValue("EasyTests",EasyTests.Count);
            Tracing.Tracer.LogValue("FinishedTests", FinishedTests.Count);
            Tracing.Tracer.LogValue("RunningTests", RunningTests.Count);
            return EasyTests.Except(FinishedTests).Except(RunningTests).ToArray();
//            return EasyTestExecutionInfos.GroupBy(executionInfo => executionInfo.EasyTest).ToArray()
//                .Where(infos => infos.All(info => info.State == EasyTestState.Failed) && infos.Count() == retries)
//                .Select(infos => new{Test = infos.Key, Count = infos.Count()})
//                .OrderBy(arg => arg.Count)
//                .Select(arg => arg.Test)
//                .ToArray();
        }

        public bool FailedAgain() {
            var lastExecutionFailures = LastExecutionFailures().ToArray();
            if (lastExecutionFailures.Any()){
                if (FinishedTests.Count == lastExecutionFailures.Length &&
                    FailedTests.Any(lastExecutionFailures.Contains)) return true;
            }
            return false;
        }

        private IEnumerable<EasyTest> GetFirstRunEasyTests() {
            var execInfos = EasyTestExecutionInfos.GroupBy(info => info.EasyTest)
                .Where(infos => ((infos.Count() == 1 && infos.First().State == EasyTestState.NotStarted)));
            return execInfos.SelectMany(infos => infos).Select(info => info.EasyTest).Distinct().OrderByDescending(test => test.LastPassedDuration()).ToArray();
        }

        public IEnumerable<WindowsUser> GetUsedUsers(EasyTest easytest) {
            return EasyTestExecutionInfos.Where(info => ReferenceEquals(info.EasyTest, easytest) && info.State == EasyTestState.Passed || info.State == EasyTestState.Failed).Select(info => info.WindowsUser).Distinct();
        }

        public IEnumerable<WindowsUser> GetIdleUsers() {
            var users = RunningTests.Select(test => test.LastEasyTestExecutionInfo).Select(info => info.WindowsUser).Distinct();
            return WindowsUsers.Except(users);
        }

        public WindowsUser GetNextUser(EasyTest easyTest) {
            var lastWindowsUser = easyTest.LastEasyTestExecutionInfo?.WindowsUser;
            var windowsUsers = GetIdleUsers().ToArray();
            return windowsUsers.Except(GetUsedUsers(easyTest).Concat(new[] { lastWindowsUser })).FirstOrDefault() ?? windowsUsers.Except(new[] { lastWindowsUser }).FirstOrDefault() ?? lastWindowsUser;
        }



    }
}