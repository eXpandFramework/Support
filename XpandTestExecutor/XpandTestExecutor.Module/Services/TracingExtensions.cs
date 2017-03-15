using System;
using System.IO;
using System.Linq;
using DevExpress.EasyTest.Framework;
using DevExpress.Persistent.Base;
using Xpand.Utils.Helpers;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Services{
    internal static class TracingExtensions{
        public static void LogErrors(this Tracing tracing, Exception e,  EasyTestExecutionInfo easyTestExecutionInfo){
            Tracing.Tracer.LogSeparator("LogErrors");
            Tracing.Tracer.LogError(e);
            try{
                easyTestExecutionInfo.Update(EasyTestState.Failed);
                easyTestExecutionInfo.EasyTestExecutionInfoSteps.Add(new EasyTestExecutionInfoStep(easyTestExecutionInfo.Session) {StepName = e.ToString(),EasyTestExecutionInfo = easyTestExecutionInfo});
                easyTestExecutionInfo.Session.ValidateAndCommitChanges();
                var directoryName = Path.GetDirectoryName(easyTestExecutionInfo.EasyTest.FileName) + "";
                var logTests = new LogTests();
                foreach (var application in easyTestExecutionInfo.EasyTest.Options.Applications.Cast<TestApplication>()){
                    var logTest = new LogTest{ApplicationName = application.Name, Result = "Failed"};
                    var logError = new LogError{Message ={Text = e.ToString()}};
                    logTest.Errors.Add(logError);
                    logTests.Tests.Add(logTest);
                }
                logTests.Save(Path.Combine(directoryName, "TestsLog.xml"));
            }
            catch (Exception exception){
                Tracing.Tracer.LogError(exception);
                throw;
            }
        }
    }
}