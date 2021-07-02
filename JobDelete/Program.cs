using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace JobDelete
{
    class Program
    {
        class Settings : Common.Settings
        {
            public string Pattern { get; protected set; }

            protected override void ShowHelp(Exception error)
            {
                string msg = @"
Usage:
{0} [-p JobIdPattern]
";
                //Console.WriteLine(String.Format(msg, Environment.GetCommandLineArgs()[0])); //TODO: This returns the .dll name instead of .exe?
                Console.WriteLine(String.Format(msg, "JobDelete"));
                base.ShowHelp(error);
            }

            protected override void GetCmdArgs(string[] args)
            {
                //NOTE: The index is from 1 instead of 0.
                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-p":
                            Pattern = args[++i];
                            break;
                        default:
                            throw new ArgumentException($"Unknonw argument `{args[i]}'!");
                    }
                }
                if (string.IsNullOrWhiteSpace(Pattern))
                {
                    throw new ArgumentException("Job id pattern must be specified in `-p' argument.");
                }
            }
        }

        static int Main(string[] args)
        {
            var settings = new Settings();
            var reg = new Regex(settings.Pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var cred = new BatchSharedKeyCredentials(settings.BatchAccountUrl, settings.BatchAccountName, settings.BatchAccountKey);
            using (var batchClient = BatchClient.Open(cred))
            {
                var matched = new List<string>();
                var jobs = batchClient.JobOperations.ListJobs(new ODATADetailLevel(selectClause: "id"));
                foreach (var job in jobs)
                {
                    if (reg.IsMatch(job.Id))
                    {
                        matched.Add(job.Id);
                    }
                }
                Console.WriteLine($"Found {matched.Count} matched jobs.");
                foreach (var id in matched)
                {
                    Console.WriteLine($"Deleting job {id}...");
                    batchClient.JobOperations.DeleteJob(id);
                }
            }
            return 0;
        }
    }
}
