using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkReq
{
    /*
    [Verb("add", HelpText = "Add file contents to the index.")]
    class AddOptions
    {
        //normal options here
    }
    [Verb("commit", HelpText = "Record changes to the repository.")]
    class CommitOptions
    {
        //commit options here
    }
    [Verb("clone", HelpText = "Clone a repository into a new directory.")]
    class CloneOptions
    {
        //clone options here
    }
    */
    [Verb("WMI",isDefault:true, HelpText = "WMI test")]
    class WMIOptions
    {
        // Omitting long name, defaults to name of property, ie "--verbose"
        [Option('d', "dcom",
            Required = false,
            Default = true,
            HelpText = "Suggest to use DCOM methods to query WMI. Enabled by default")]
        public bool DCOM { get; set; }

        [Option('a', "async",
            Required = false,
            Default = false,
            HelpText = "Use async methods to query WMI")]
        public bool AsyncOnly { get; set; }

        [Option('h', "host",
            Required = false,
            Default = "localhost",
            HelpText = "Which host we try to query? Default is localhost")]
        public string Host { get; set; }

        [Option('t', "threads",
            Required = false,
            Default = 1,
            HelpText = "Number of active WMI threads")]
        public int Threads { get; set; }

        [Option('m', "minutes",
            Required = false,
            Default = 0,
            HelpText = "How many minutes souls take a test, default = 0. It means unlimited")]
        public int Minutes { get; set; }

        /*
        [Value(0, MetaName = "offset", HelpText = "File offset.")]
        public long? Offset { get; set; }*/
    }
}
