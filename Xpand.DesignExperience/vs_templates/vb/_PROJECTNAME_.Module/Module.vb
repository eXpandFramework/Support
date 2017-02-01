
Imports DevExpress.ExpressApp
Imports System.Reflection
Imports DevExpress.Persistent.BaseImpl
Imports Xpand.ExpressApp.JobScheduler.Jobs.ThresholdCalculation
Imports Xpand.ExpressApp.WorldCreator.System

Partial Public NotInheritable Class [$projectsuffix$Module]
    Inherits ModuleBase
    Public Sub New()
        InitializeComponent()
    End Sub

	Public Overrides Sub Setup(ByVal moduleManager As ApplicationModulesManager)
        MyBase.Setup(moduleManager)
        AdditionalExportedTypes.AddRange(ModuleHelper.CollectExportedTypesFromAssembly(Assembly.GetAssembly(GetType(Analysis)), AddressOf IsExportedType))
		WorldCreatorTypeInfoSource.Instance.ForceRegisterEntity(GetType(AuditDataItemPersistent))
        WorldCreatorTypeInfoSource.Instance.ForceRegisterEntity(GetType(AuditedObjectWeakReference))
    End Sub

End Class
