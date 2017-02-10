using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using DevExpress.EasyTest.Framework;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using Xpand.Persistent.Base.General;
using Xpand.Persistent.Base.General.CustomAttributes;

namespace XpandTestExecutor.Module.BusinessObjects {
    [DefaultClassOptions]
    [DefaultProperty("Sequence")]
    public class EasyTestExecutionInfo : BaseObject, ISupportSequenceObject {
        private WindowsUser _windowsUser;
        private EasyTest _easyTest;
        private DateTime _end;
        private DateTime _start;
        private EasyTestState _state;
        private int _webPort;
        private int _winPort;

        public EasyTestExecutionInfo(Session session)
            : base(session) {
        }
        [Size(SizeAttribute.Unlimited), Delayed]
        public string TestsLog {
            get { return GetDelayedPropertyValue<string>("TestsLog"); }
            set { SetDelayedPropertyValue("TestsLog", value); }
        }

        [Size(SizeAttribute.Unlimited), Delayed]
        public string ExecutorLog {
            get { return GetDelayedPropertyValue<string>("ExecutorLog"); }
            set { SetDelayedPropertyValue("ExecutorLog", value); }
        }

        [Size(SizeAttribute.Unlimited), Delayed]
        public string WinLog {
            get { return GetDelayedPropertyValue<string>("WinLog"); }
            set { SetDelayedPropertyValue("WinLog", value); }
        }

        [Size(SizeAttribute.Unlimited), Delayed]
        public string WebLog {
            get { return GetDelayedPropertyValue<string>("WebLog"); }
            set { SetDelayedPropertyValue("WebLog", value); }
        }

        [ValueConverter(typeof(ImageValueConverter))]
        [Delayed]
        public Image WebView {
            get { return GetDelayedPropertyValue<Image>("WebView"); }
            set { SetDelayedPropertyValue("WebView", value); }
        }

        [ValueConverter(typeof(ImageValueConverter))]
        [Delayed]
        public Image WinView {
            get { return GetDelayedPropertyValue<Image>("WinView"); }
            set { SetDelayedPropertyValue("WinView", value); }
        }

        public int WinPort {
            get { return _winPort; }
            set { SetPropertyValue("WinPort", ref _winPort, value); }
        }

        public int WebPort {
            get { return _webPort; }
            set { SetPropertyValue("WebPort", ref _webPort, value); }
        }

        [Association("EasyTestExecutionInfo-EasyTestApplications")]
        public XPCollection<EasyTestApplication> EasyTestApplications => GetCollection<EasyTestApplication>("EasyTestApplications");

        public XPCollection<EasyTestExecutionInfo> ConcurrentInfos {
            get {
                var easyTestExecutionInfos = ExecutionInfo.EasyTestExecutionInfos.Where(info => info.EasyTest.Oid!=EasyTest.Oid&&info.Start!=DateTime.MinValue&&Start!=DateTime.MinValue);
                var timeRange = new DateTimeRange(Start,End);
                var testExecutionInfos = easyTestExecutionInfos.Where(info => timeRange.Intersects(new DateTimeRange(info.Start, info.End)));
                return new XPCollection<EasyTestExecutionInfo>(Session, testExecutionInfos);
            }
        }

        [Association("EasyTestExecutionInfo-EasyTests")]
        public EasyTest EasyTest {
            get { return _easyTest; }
            set { SetPropertyValue("EasyTest", ref _easyTest, value); }
        }

        [Association("EasyTestExecutionInfo-ExecutionInfos")]
        public ExecutionInfo ExecutionInfo { get; set; }

        public int Duration => (int)End.Subtract(Start).TotalMinutes;

        [DisplayFormat("{0:HH:mm}")]
        public DateTime End {
            get { return _end; }
            set { SetPropertyValue("End", ref _end, value); }
        }

        [InvisibleInAllViews]
        [DisplayFormat("{0:HH:mm}")]
        public DateTime Start {
            get { return _start; }
            set { SetPropertyValue("Start", ref _start, value); }
        }

        public EasyTestState State {
            get { return _state; }
            set { SetPropertyValue("State", ref _state, value); }
        }

        [RuleRequiredField(TargetCriteria = "State='Running'")]
        public WindowsUser WindowsUser {
            get { return _windowsUser; }
            set { SetPropertyValue("WindowsUser", ref _windowsUser, value); }
        }

        [InvisibleInAllViews]
        public long Sequence { get; set; }

        string ISupportSequenceObject.Prefix => ((ISupportSequenceObject)EasyTest).Sequence.ToString(CultureInfo.InvariantCulture);

        public void SetView(bool win, Image view) {
            if (win)
                WinView = view;
            else {
                WebView = view;
            }
        }

        protected override void OnSaving() {
            base.OnSaving();
            SequenceGenerator.GenerateSequence(this);
        }

        public void CreateApplications(string directory) {
            foreach (TestApplication application in EasyTest.Options.Applications.Cast<TestApplication>()) {
                EasyTestApplications.Add(
                    new XPQuery<EasyTestApplication>(Session, true).FirstOrDefault(
                        testApplication => testApplication.Name == application.Name) ??
                    new EasyTestApplication(Session) { Name = application.Name });
            }
        }

        public void SetLog(bool isWin, string text) {
            if (isWin)
                WinLog = text;
            else {
                WebLog = text;
            }
        }

        public void Update(EasyTestState easyTestState) {
            State = easyTestState;
            if (State == EasyTestState.Running) {
                Start = DateTime.Now;
            }
            else if (State == EasyTestState.Passed || State == EasyTestState.Failed)
                End = DateTime.Now;

            var path = Path.GetDirectoryName(EasyTest.FileName) + "";
            if (State == EasyTestState.Failed) {
                var logTests = EasyTest.GetLogTests().Where(test => test.Result!="Passed").ToArray();
                var testsLog = Path.Combine(path, "TestsLog.xml");
                if (File.Exists(testsLog)){
                    if (logTests.All(test => test.ApplicationName==null))
                        TestsLog = File.ReadAllText(testsLog);
                    else {
                        foreach (var platform in new[] { "Win", "Web" }) {
                            var logTest = logTests.FirstOrDefault(test => test.ApplicationName.Contains("." + platform));
                            if (logTest != null) {
                                var fileName = Directory.GetFiles(path, EasyTest.Name + "_*." + platform + "_View.jpeg").FirstOrDefault();
                                if (fileName != null)
                                    SetView(platform == "Win", Image.FromFile(fileName));
                                fileName = Directory.GetFiles(path, "eXpressAppFramework_" + platform + ".log").FirstOrDefault();
                                if (fileName != null)
                                    SetLog(platform == "Win", File.ReadAllText(fileName));
                            }
                        }
                        TestsLog = File.ReadAllText(testsLog);
                        var testExecutorLog = Path.Combine(path, "TestExecutor.log");
                        if (File.Exists(testExecutorLog))
                            ExecutorLog = File.ReadAllText(testExecutorLog);
                    }
                }
            }

        }

    }

    public enum EasyTestState {
        NotStarted,
        Running,
        Failed,
        Passed
    }
}