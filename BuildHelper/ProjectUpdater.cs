﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace BuildHelper {
    class ProjectUpdater : Updater {
        private readonly string _version;
        public static readonly XNamespace XNamespace = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");

        readonly string[] _copyLocalReferences ={
            "Xpand.ExpressApp.FilterDataStore", "Xpand.ExpressApp.FilterDataStore.Win",
            "Xpand.ExpressApp.FilterDataStore.Web", "Xpand.ExpressApp.ModelAdaptor", "Xpand.Persistent.BaseImpl","DevExpress.Web.ASPxThemes."
        };

        readonly Dictionary<string, string> _requiredApplicationProjectReferences =
            new Dictionary<string, string>{
                {"Xpand.ExpressApp", "Xpand.Persistent.BaseImpl"},
                {"Xpand.ExpressApp.ExceptionHandling", "Xpand.Persistent.BaseImpl"},
                {"Xpand.ExpressApp.IO", "Xpand.Persistent.BaseImpl"},
                {"Xpand.Persistent.BaseImpl.JobScheduler", "Xpand.Persistent.BaseImpl"},
                {"Xpand.ExpressApp.WorldCreator", "Xpand.Persistent.BaseImpl"},
                {"Xpand.ExpressApp.PivotChart.Win", "Xpand.Persistent.BaseImpl"}
            };

        public ProjectUpdater(IDocumentHelper documentHelper, string rootDir,string version) : base(documentHelper, rootDir){
            _version = version;
        }

        public override void Update(string file) {
            var document = DocumentHelper.GetXDocument(file);
            var directoryName = Path.GetDirectoryName(file) + "";
            if (IsApplicationProject(document)) {
                AddRequiredReferences(document, file);
            }
            UpdateProjectReferences(document,file);
            UpdateReferences(document, directoryName, file);
            UpdateNugetTargets(document, file);
            UpdateConfig(file);
            UpdateLanguageVersion(document,file);
            if (SyncConfigurations(document))
                DocumentHelper.Save(document, file);

            var licElement = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "EmbeddedResource" && element.Attribute("Include")?.Value == @"Properties\licenses.licx");
            if (licElement != null) {
                licElement.Remove();
                DocumentHelper.Save(document,file);
            }
            var combine = Path.Combine(Path.GetDirectoryName(file)+"", @"Properties\licenses.licx");
            if  (File.Exists(combine))
                File.Delete(combine);
//            Console.WriteLine(document);
        }

        private void UpdateLanguageVersion(XDocument document, string file){
            var propertyGroups = document.Descendants().Where(element => element.Name.LocalName=="PropertyGroup").ToArray();
            foreach (var propertyGroup in propertyGroups){
                var langVersionElement = propertyGroup.Descendants().FirstOrDefault(element => element.Name.LocalName== "LangVersion")?? new XElement(XNamespace + "LangVersion");
                if (string.IsNullOrEmpty(langVersionElement.Value))
                    propertyGroup.Add(langVersionElement);
                langVersionElement.Value = "latest";
            }
            DocumentHelper.Save(document,file );
        }

        private void UpdateProjectReferences(XDocument document, string file){
            var xpandProjectReferences = document.Descendants().Where(element => element.Name.LocalName == "ProjectReference" && Path.GetFileName(element.Attribute("Include")?.Value).StartsWith("Xpand.")).ToArray();
            var references = document.Descendants().Where(xElement => xElement.Name.LocalName=="Reference").Select(element => element.Parent).First();
            foreach (var element in xpandProjectReferences){
                var referenceElement = new XElement(XNamespace + "Reference");
                var value = Path.GetFileNameWithoutExtension(element.Attribute("Include")?.Value);
                Debug.Assert(value != null, nameof(value) + " != null");
                referenceElement.Add(new XAttribute("Include", value));
                references.Add(referenceElement);
                element.Remove();
            }
            if (xpandProjectReferences.Any())
                DocumentHelper.Save(document, file);
        }

        private void UpdateNugetTargets(XDocument document, string file){
            var nugetTargetsPath = Extensions.PathToRoot(Path.GetDirectoryName(file),RootDir) + @"Support\Build\Nuget.Targets";
            if (document.Descendants(XNamespace + "Import").All(element => !NugetPathMatch(element, nugetTargetsPath))) {
                var elements = document.Descendants(XNamespace + "Import").Where(xelement => xelement.Attribute("Project").Value.ToLowerInvariant().EndsWith("nuget.targets")).ToArray();
                for (int index = elements.Length - 1; index >= 0; index--) {
                    var xElement = elements[index];
                    xElement.Remove();
                }
                Debug.Assert(document.Root != null, "document.Root != null");
                var element = new XElement(XNamespace + "Import");
                element.SetAttributeValue("Project", nugetTargetsPath);
                element.SetAttributeValue("Condition",$"Exists('{nugetTargetsPath}')");
                document.Root.Add(element);
                DocumentHelper.Save(document, file);
            }
        }

        private static bool NugetPathMatch(XElement element, string nugetTargetsPath){
            return string.Equals(element.Attribute("Project")?.Value, nugetTargetsPath,
                       StringComparison.InvariantCultureIgnoreCase)&& element.Attributes("Condition").Any();
        }

        private bool SyncConfigurations(XDocument document){
            var debugOutputPath = GetOutputPath(document,"Debug").Value;
            if (debugOutputPath.ToLower().TrimEnd('\\').EndsWith("xpand.dll")){
                var releaseOutputPath = GetOutputPath(document,"Release");
                if (releaseOutputPath.Value+"" != debugOutputPath){
                    releaseOutputPath.Value = debugOutputPath;
                    return true;
                }
            }
            return false;
        }

        private XElement GetOutputPath(XDocument document, string configuration){
            return document.Descendants().Where(element => element.Name.LocalName=="OutputPath").First(element =>{
                var attribute = element.Parent?.Attribute("Condition");
                if (attribute != null){
                    var value = attribute.Value+"";
                    return new[]{"Configuration",configuration}.All(value.Contains);
                }
                return false;
            });
        }

        void UpdateConfig(string file) {
            var config = Path.Combine(Path.GetDirectoryName(file) + "", "app.config");
            if (File.Exists(config)) {
                ReplaceToken(config);
            }
            else {
                config = Path.Combine(Path.GetDirectoryName(file) + "", "web.config");
                if (File.Exists(config)) {
                    ReplaceToken(config);
                }
            }
            var functionalTestsPath = Path.GetDirectoryName(file)+@"\FunctionalTests";
            if (Directory.Exists(functionalTestsPath)){
                foreach (var configFile in Directory.GetFiles(functionalTestsPath,"Config.xml",SearchOption.AllDirectories)){
                    ReplaceToken(configFile);
                    UpdateAdapterVersion(configFile);    
                }
            }
        }

        private void UpdateAdapterVersion(string config){
            string readToEnd;
            using (var streamReader = new StreamReader(config)){
                var toEnd = streamReader.ReadToEnd();
                readToEnd = Regex.Replace(toEnd, @"(<Alias Name=""(Win|Web)AdapterAssemblyName"" Value=""Xpand\.ExpressApp\.EasyTest[^=]*=)([.\d]*)", "${1}" + _version);
                readToEnd = Regex.Replace(readToEnd, @"((<Alias Name=""(Win|Web)AdapterAssemblyName"" Value=""Xpand\.ExpressApp\.EasyTest[^=]*=).*?, PublicKeyToken=)(b88d1754d700e49a)", "$1c52ffed5d5ff0958", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            }
            using (var streamWriter = new StreamWriter(config)) {
                streamWriter.Write(readToEnd);
            }
        }

        void ReplaceToken(string config) {     
            string readToEnd;
            using (var streamReader = new StreamReader(config)) {
                readToEnd = streamReader.ReadToEnd().Replace("c52ffed5d5ff0958", "b88d1754d700e49a");
            }
            using (var streamWriter = new StreamWriter(config)) {
                streamWriter.Write(readToEnd);
            }
        }

        void AddRequiredReferences(XDocument document, string file) {
            var referencesItemGroup = document.Descendants().First(element => element.Name.LocalName == "Reference").Parent;
            if (referencesItemGroup == null) throw new NullReferenceException("referencesItemGroup");

            foreach (string reference in RequiredReferencesThatDoNotExist(document)) {
                var referenceElement = new XElement(XNamespace + "Reference");
                referenceElement.Add(new XAttribute("Include", reference));
                referencesItemGroup.Add(referenceElement);
                DocumentHelper.Save(document, file);
            }
        }

        IEnumerable<string> RequiredReferencesThatDoNotExist(XDocument document) {
            return _requiredApplicationProjectReferences.Where(reference => !AlreadyReferenced(document, reference.Value) &&
                HasReferenceRequirement(document, reference.Key)).Select(reference => reference.Value);
        }

        bool AlreadyReferenced(XDocument document, string reference) {
            return document.Descendants().Any(element => element.Name.LocalName == "Reference" && element.Attribute("Include")?.Value == reference);
        }

        bool HasReferenceRequirement(XDocument document, string reference) {
            return HasReferenceRequirementInProject(document, reference) || HasReferenceRequirementInReferenceProjects(document, reference);
        }

        bool HasReferenceRequirementInReferenceProjects(XDocument document, string reference) {
            var documents = document.Descendants().Where(element => element.Name.LocalName == "ProjectReference").Select(element
                => DocumentHelper.GetXDocument(Path.GetFullPath(element.Attribute("Include")?.Value)));
            return documents.Any(xDocument => HasReferenceRequirementInProject(xDocument, reference));
        }

        bool HasReferenceRequirementInProject(XDocument document, string reference) {
            return document.Descendants().Any(element => element.Name.LocalName == "Reference" && element.Attribute("Include")?.Value == reference);
        }

        bool IsApplicationProject(XDocument document){
            var outputType = GetOutputType(document);
            return outputType != null && (IsExe(outputType) || (IsWeb(document, outputType)));
        }

        private bool IsWeb(XDocument document, XElement outputType){
            return outputType.Value == "Library" && CheckWebGuid(document);
        }

        private static bool IsExe(XElement outputType){
            return (outputType.Value == "WinExe" || outputType.Value == "Exe");
        }

        private static XElement GetOutputType(XDocument document){
            var outputType = document.Descendants().First(element => element.Name.LocalName == "OutputType");
            return outputType;
        }

        bool CheckWebGuid(XDocument document) {
            var projectTypeGuids = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "ProjectTypeGuids");
            return projectTypeGuids != null && projectTypeGuids.Value.Split(';').Any(s => s.Contains("349c5851-65df-11da-9384-00065b846f21"));
        }

        void UpdateReferences(XDocument document, string directoryName, string file) {
            var references = document.Descendants().Where(IsXpandOrDXElement);
            foreach (XElement reference in references) {
                var attribute = reference.Attribute("Include");

                var value = Regex.Match(attribute?.Value, "(Xpand.[^,]*)|(DevExpress.[^,]*)", RegexOptions.Singleline | RegexOptions.IgnoreCase).Value;
                if (string.CompareOrdinal(attribute?.Value, value) != 0){
                    if (attribute != null) attribute.Value = value;
                    DocumentHelper.Save(document, file);
                }

                UpdateElementValue(reference, "SpecificVersion", "False", file, document);

                if (_copyLocalReferences.Any(s => attribute != null && attribute.Value.StartsWith(s)))
                    UpdateElementValue(reference, "Private", "True", file, document);
                var assemblyName = reference.Attribute("Include").Value;
                assemblyName = Path.GetFileNameWithoutExtension($"{assemblyName}.dll");
                if (!string.IsNullOrEmpty(Program.Options.DXHintPath) &&assemblyName.StartsWith("DevExpress.")&&!assemblyName.Contains(".DXCore.")) {
                    var hintpath = $@"{Program.Options.DXHintPath}\{assemblyName}.dll";
                    if (!File.Exists(hintpath)) {
                        Console.WriteLine($"DXHintPathFiles:{string.Join(Environment.NewLine,Directory.GetFiles(Program.Options.DXHintPath))}");
                        throw new FileNotFoundException($"Invalid path {hintpath}",hintpath);
                    }
                    
                    if (!Program.Options.AfterBuild) {
                        Console.WriteLine($"Set DXHintPath for {assemblyName} to {hintpath}");
                        UpdateElementValue(reference, "HintPath", hintpath, file, document);
                    }
                    else {
                        Console.WriteLine($"Remove DXHintPath for {assemblyName} to {hintpath}");
                        var elements = reference.Nodes().OfType<XElement>().Where(_ => _.Name.LocalName=="HintPath").ToArray();
                        foreach (var element in elements) {
                            element.Remove();
                        }
                    }
                }
                if (reference.Attribute("Include").Value.StartsWith("Xpand.")) {
                    var path = Extensions.PathToRoot(directoryName,RootDir) + @"Xpand.DLL\" + attribute?.Value + ".dll";
                    UpdateElementValue(reference, "HintPath", path, file, document);
                }
            }
        }

        void UpdateElementValue(XElement reference, string name, string value, string file, XDocument document) {
            var element = reference.Nodes().OfType<XElement>().FirstOrDefault(xelement => xelement.Name.LocalName == name);
            if (element == null) {
                element = new XElement(XNamespace + name);
                reference.Add(element);
                element.Value = value;
                DocumentHelper.Save(document, file);
            } else if (string.CompareOrdinal(value, element.Value) != 0) {
                element.Value = value;
                DocumentHelper.Save(document, file);
            }
        }

        bool IsXpandOrDXElement(XElement element) {
            return element.Name.LocalName == "Reference" && Regex.IsMatch(element.Attribute("Include")?.Value, "(Xpand)|(DevExpress)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        }
    }
}