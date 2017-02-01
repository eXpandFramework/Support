using DevExpress.ExpressApp.Security.Strategy;
using DevExpress.Persistent.BaseImpl;
using DevExpress.ExpressApp;
using System.Reflection;
using Xpand.ExpressApp.JobScheduler.Jobs.ThresholdCalculation;
using Xpand.ExpressApp.ModelDifference.Security;
using Xpand.ExpressApp.WorldCreator.System;

namespace $projectsuffix$.Module {
    public sealed partial class $projectsuffix$Module : ModuleBase {
        public $projectsuffix$Module() {
            InitializeComponent();
        }
		public override void Setup(ApplicationModulesManager moduleManager) {
            base.Setup(moduleManager);
            AdditionalExportedTypes.AddRange(ModuleHelper.CollectExportedTypesFromAssembly(Assembly.GetAssembly(typeof(Analysis)), IsExportedType));
			WorldCreatorTypeInfoSource.Instance.ForceRegisterEntity(typeof(AuditDataItemPersistent));
            WorldCreatorTypeInfoSource.Instance.ForceRegisterEntity(typeof(AuditedObjectWeakReference));
        }

		
    }
}
