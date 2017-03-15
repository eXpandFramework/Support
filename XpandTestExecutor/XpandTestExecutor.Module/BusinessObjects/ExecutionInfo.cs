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
        private XPCollection<EasyTest> _easyTests;

        public void SetEasyTests(string[] easyTestFiles){
            if (easyTestFiles.Length>0){
                var easyTests = Session.Query<EasyTest>().Where(test => easyTestFiles.Contains(test.FileName)).ToArray();
                _easyTests = new XPCollection<EasyTest>(Session, easyTests);
            }
            else{
                _easyTests.Dispose();
            }
        }

        public ExecutionInfo(Session session)
            : base(session) {
        }

//        public WindowsUser[] LoggedInUsers{
//            get{
//
//                ITerminalServicesManager manager = new TerminalServicesManager();
//                using (ITerminalServer server = manager.GetRemoteServer(Environment.MachineName)){
//                    server.Open();
//                    return server.GetSessions().Select(session => WindowsUsers.FirstOrDefault(user => user.Name== session.UserName)).Where(user => user!=null).ToArray();
//                }
//
//                return RunningTests.Select(test => test.GetLastEasyTestExecutionInfo(this).WindowsUser).ToArray();
////                return EnviromentExEx.ListUsers().Select(s => WindowsUsers.FirstOrDefault(user => user.Name == s)).Where(user => user != null).ToArray();
//            }
//        }

        public int Duration => EasyTestExecutionInfos.Duration();

        [Association("ExecutionInfos-Users")]
        public XPCollection<WindowsUser> WindowsUsers => GetCollection<WindowsUser>("WindowsUsers");
        public XPCollection<WindowsUser> AvailableUsers{
            get{
                var users = WindowsUsers.Except(RunningTests.Select(test => test.GetLastEasyTestExecutionInfo(this).WindowsUser));
                return new XPCollection<WindowsUser>(Session, users);
            }
        }

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
                return _easyTests?? new XPCollection<EasyTest>(Session, EasyTestExecutionInfos.Select(info => info.EasyTest));
            }
        }

        public XPCollection<EasyTest> TestsToExecute{
            get{
                var runningTests = RunningTests.ToArray();
                var failedTests = FailedTests.ToArray();
                var passedEasyTests = PassedEasyTests.ToArray();
                var testsToExecute = EasyTests.Except(runningTests.Concat(failedTests).Concat(passedEasyTests))
                    .OrderBy(test => test.EasyTestExecutionInfos.Count(info => info.ExecutionInfo.Oid == Oid));
                return new XPCollection<EasyTest>(Session, testsToExecute);
            }
        }

        public XPCollection<EasyTest> RunningTests
            => new XPCollection<EasyTest>(Session, EasyTests.Except(FinishedTests).Where(test => test.GetLastEasyTestExecutionInfo(this)?.State==EasyTestState.Running));

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
                return EasyTestExecutionInfos.GroupBy(info => info.EasyTest).Where(Failure).Select(infos => infos.Key);
            }
        }

        private bool Failure(IGrouping<EasyTest, EasyTestExecutionInfo> infos){
            var maxSequence = infos.Max(info => info.Sequence);
            var lastEasyTestExecutionInfo = infos.First(info => info.Sequence==maxSequence);
            var isRunning = lastEasyTestExecutionInfo.State==EasyTestState.Running;
            var passed = infos.Any(info => info.State==EasyTestState.Passed);
            return !isRunning&&(!passed && infos.Count() > Retries);
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

        public static ExecutionInfo Create(UnitOfWork unitOfWork, bool rdc, int retries) {
            IEnumerable<WindowsUser> windowsUsers = WindowsUser.CreateUsers(unitOfWork, rdc);
            var executionInfo = new ExecutionInfo(unitOfWork){
                Retries = retries,
            };
            executionInfo.WindowsUsers.AddRange(windowsUsers);
            unitOfWork.ValidateAndCommitChanges();
            CurrentSequenceOperator.CurrentSequence = executionInfo.Sequence;
            return executionInfo;
        }

        public override void AfterConstruction() {
            base.AfterConstruction();
            CreationDate = DateTime.Now;
        }

        public IEnumerable<EasyTest> LastExecutionFailures(){
            var sequence = Session.Query<ExecutionInfo>().Where(info => info.Sequence<Sequence).Max(info => info.Sequence);
            var executionInfo = Session.Query<ExecutionInfo>().First(info => info.Sequence==sequence);
            return executionInfo.PassedEasyTests;
        }

        public bool FailedAgain() {
            var lastExecutionFailures = LastExecutionFailures().ToArray();
            if (lastExecutionFailures.Any()){
                if (FinishedTests.Count == lastExecutionFailures.Length &&
                    FailedTests.Any(lastExecutionFailures.Contains)) return true;
            }
            return false;
        }

    }
}