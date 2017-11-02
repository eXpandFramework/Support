using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using DevExpress.EasyTest.Framework;
using DevExpress.EasyTest.Framework.Utils;
using DevExpress.ExpressApp;

namespace XpandTestExecutor.Module.BusinessObjects {
    public class OptionsProvider {
        Dictionary<string,Options> _options;

        public static OptionsProvider Instance { get; } = new OptionsProvider();

        public Options this[string fileName] {
            get {
                if (_options==null)
                    _options = new Dictionary<string, Options>();
                if (!_options.ContainsKey(fileName.ToLower()))
                    Init(fileName);
                return _options[fileName.ToLower()];
            }
        }

        public static void Init(string[] easyTestFileNames) {
            Instance._options = new Dictionary<string, Options>();
            foreach (var path in easyTestFileNames) {
                Init(path);
            }
        }

        private static void Init(string path){
            var directoryName = Path.GetDirectoryName(path) + "";
            string fileName = Path.Combine(directoryName, "config.xml");
            var destFileName = Path.Combine(Path.GetDirectoryName(fileName) + "", "_" + Path.GetFileName(fileName));
            if (!File.Exists(destFileName))
                File.Copy(fileName, destFileName, true);
            Options options;
            using (var fileStream = new FileStream(destFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)){
                options = LoadOptions(fileStream, null, null, directoryName);
            }
            Instance._options.Add(path.ToLower(), options);
        }

        public static Options LoadOptions(Stream optionsStream, string profileName, string overrides, string configPath) {
            var testAliasList = new OptionsLoader().Load(optionsStream, profileName, ViewShortcut.Empty, configPath).Aliases;
            var options = (Options)new XmlSerializer(typeof(Options)).Deserialize(new AliasesComposer(testAliasList).Compose(optionsStream));
            return options;
        }

    }

}
