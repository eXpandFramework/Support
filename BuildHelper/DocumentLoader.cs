﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace BuildHelper {
    public interface IDocumentHelper {
        XDocument GetXDocument(string file);
        void Save(XDocument document, string file);
        IList<string> SavedFiles { get; }
    }

    public class DocumentHelper : IDocumentHelper {
        private readonly IList<string> _savedFiles=new List<string>();

        public XDocument GetXDocument(string file) {
            Environment.CurrentDirectory = Path.GetDirectoryName(file) + "";
            XDocument document;
            using (var fileStream = File.OpenRead(file)) {
                document = XDocument.Load(fileStream,LoadOptions.PreserveWhitespace);
            }
            return document;
        }

        // Fields...

        public IList<string> SavedFiles => _savedFiles;

        public void Save(XDocument document, string file) {
            document.Save(file, SaveOptions.DisableFormatting);
            _savedFiles.Add(file);
        }

    }
}