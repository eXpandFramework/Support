﻿using CommandLine;
using CommandLine.Text;

namespace RDClient{
    public class Options {
        private Options(){
        }

        [Option('u', "username", Required = true,HelpText = "A remote desktop session will be created with this user")]
        public string UserName { get; set; }

        [Option('p', "password", Required = true, HelpText = "The user password")]
        public string Password { get; set; }
        [Option('n', "pipe", Required = true, HelpText = "The pipe name")]
        public string PipeName { get; set; }
        
        [Option('d', "domain",  HelpText = "The network domain")]
        public string Domain { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        public static Options Instance { get; } = new Options();

        [Option('m', "timeout" )]
        public int TimeOut { get; set; }

        [HelpOption]
        public string GetUsage() {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
