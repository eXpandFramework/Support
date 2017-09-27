function Show-ExitReason($exitCode){
    switch ($exitcode) {
        "1001" {"ExtensionManager.AlreadyInstalledException";break}
        "1002" {"ExtensionManager.NotInstalledException";break}
        "1003" {"ExtensionManager.NotPendingDeletionException";break}
        "1005" {"ExtensionManager.IdentifierConflictException";break}
        "1006" {"ExtensionManager.MissingTargetFrameworkException";break}
        "1007" {"ExtensionManager.MissingReferencesException";break}
        "1008" {"ExtensionManager.BreaksExistingExtensionsException";break}
        "1009" {"ExtensionManager.InstallByMsiException";break}
        "1010" {"ExtensionManager.SystemComponentException";break}
        "1011" {"ExtensionManager.MissingPackagePartException";break}
        "1012" {"ExtensionManager.InvalidExtensionManifestException";break}
        "1013" {"ExtensionManager.InvalidExtensionPackageException";break}
        "1014" {"ExtensionManager.NestedExtensionInstallException";break}
        "1015" {"ExtensionManager.RequiresAdminRightsException";break}
        "1016" {"ExtensionManager.ProxyCredentialsRequiredException";break}
        "1017" {"ExtensionManager.InvalidPerMachineOperationException";break}
        "1018" {"ExtensionManager.ReferenceConstraintException";break}
        "1019" {"ExtensionManager.DependencyException";break}
        "1020" {"ExtensionManager.InconsistentNestedReferenceIdException";break}
        "1021" {"ExtensionManager.UnsupportedProductException";break}
        "1022" {"ExtensionManager.DirectoryExistsException";break}
        "1023" {"ExtensionManager.FilesInUseException";break}
        "1024" {"ExtensionManager.CannotUninstallOrphanedComponentsException";break}
        "2001" {"VSIXInstaller.InvalidCommandLineException";break}
        "2002" {"VSIXInstaller.InvalidLicenseException";break}
        "2003" {"VSIXInstaller.NoApplicableSKUsException";break}
        "2004" {"VSIXInstaller.BlockingProcessesException";break}
       default {"Other exception"; break}
    }
}