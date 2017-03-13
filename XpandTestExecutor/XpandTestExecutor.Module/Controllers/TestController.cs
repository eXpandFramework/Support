using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using XpandTestExecutor.Module.BusinessObjects;
using XpandTestExecutor.Module.Services;

namespace XpandTestExecutor.Module.Controllers {
    public interface IModelOptionsTestExecutor {
        int ExecutionRetries { get; set; }
    }

    [DomainLogic(typeof(IModelOptionsTestExecutor))]
    public static class ModelOptionsTestExecutorLogic{
        public static int Get_ExecutionRetries(IModelOptionsTestExecutor modelOptionsTestExecutor){
            var environmentVariable = Environment.GetEnvironmentVariable("TestExecutionRetries",
                EnvironmentVariableTarget.Machine);
            return environmentVariable != null ? Convert.ToInt32(environmentVariable) : 3;
        }
    }
    public class TestController : ObjectViewController<ListView, EasyTest>,IModelExtender {
        private const string CancelRun = "Cancel Run";
        private const string Run = "Run";
        private readonly SimpleAction _runTestAction;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly SimpleAction _unlinkTestAction;
        private TestControllerHelper _testControllerHelper;

        public TestController() {
            _runTestAction = new SimpleAction(this, "RunTest", PredefinedCategory.View) {
                Caption = Run,
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects
            };
            _runTestAction.Execute += RunTestActionOnExecute;

            _unlinkTestAction = new SimpleAction(this, "UnlinkTest", PredefinedCategory.View) {
                Caption = "Unlink",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects
            };
            _unlinkTestAction.Execute+=UnlinkTestActionOnExecute;   
        }

        protected override void OnActivated() {
            base.OnActivated();
            _testControllerHelper = Application.MainWindow.GetController<TestControllerHelper>();
        }

        public SingleChoiceAction SelectionModeAction => _testControllerHelper.SelectionModeAction;

        public SingleChoiceAction UserModeAction => _testControllerHelper.UserModeAction;

        public bool IsDebug => _testControllerHelper.ExecutionModeAction.SelectedItem.Caption == "Debug";

        
        private void UnlinkTestActionOnExecute(object sender, SimpleActionExecuteEventArgs e) {
            var easyTests = e.SelectedObjects.Cast<EasyTest>().ToArray();
            OptionsProvider.Init(easyTests.Select(test => test.FileName).ToArray());
            if (ReferenceEquals(SelectionModeAction.SelectedItem.Data, TestControllerHelper.FromFile)) {
                var fileNames = File.ReadAllLines("easytests.txt").Where(s => !string.IsNullOrEmpty(s)).ToArray();
                easyTests = EasyTest.GetTests(ObjectSpace, fileNames);
            }
            foreach (var info in easyTests.SelectMany(GetUISequenceInfos)) {
                info.WindowsUser = WindowsUser.CreateUsers((UnitOfWork)ObjectSpace.Session(), false).First();
                info.Unlink();
            }
            ObjectSpace.RollbackSilent();

        }

        private IEnumerable<EasyTestExecutionInfo> GetUISequenceInfos(EasyTest test){
            return new XPCollection<EasyTestExecutionInfo>(test.Session,
                test.EasyTestExecutionInfos.Where(
                    info => info.ExecutionInfo.Sequence == CurrentSequenceOperator.CurrentSequence));
        }

    private void RunTestActionOnExecute(object sender, SimpleActionExecuteEventArgs e) {
            if (_runTestAction.Caption==CancelRun){
                _runTestAction.Enabled[CancelRun] = false;
                _cancellationTokenSource?.Cancel();
            }
            else if (ReferenceEquals(SelectionModeAction.SelectedItem.Data, TestControllerHelper.Selected)){
                _runTestAction.Caption = CancelRun;
                _unlinkTestAction.DoExecute();
                _cancellationTokenSource=new CancellationTokenSource();
                var easyTestFiles = e.SelectedObjects.Cast<EasyTest>().Select(test => test.FileName).ToArray();
                Task.Factory.StartNew(() => TestExecutor.Execute(easyTestFiles, IsDebug, _cancellationTokenSource.Token,
                        ((IModelOptionsTestExecutor) Application.Model.Options).ExecutionRetries))
                    .ContinueWith(
                        task =>{
                            _runTestAction.Caption = Run;
                            _runTestAction.Enabled[CancelRun] = true;
                        });
            }
            else{
                throw new NotImplementedException();
            }
        }



        public void ExtendModelInterfaces(ModelInterfaceExtenders extenders) {
            extenders.Add<IModelOptions, IModelOptionsTestExecutor>();
        }
    }
}