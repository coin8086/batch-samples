using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using System;
using System.Collections.Generic;

namespace JobManagerTask
{
    class Program
    {
        class Settings : Common.Settings
        {
            public string JobId { get; protected set; }

            public bool Debug { get; protected set; } = false;

            public int NumOfTasks { get; protected set; } = 10;

            public string Cmd { get; protected set; } = "cmd /c echo Task %AZ_BATCH_TASK_ID% on Node %AZ_BATCH_NODE_ID%";

            protected override void ShowHelp(Exception error)
            {
                string msg = @"
Usage:
{0} [-n NumOfTasks] [-c TaskCmdLine] [-d]
";
                //NOTE: This returns a file name of .dll instead of .exe:
                //Console.WriteLine(String.Format(msg, Environment.GetCommandLineArgs()[0]));
                Console.WriteLine(String.Format(msg, "JobManagerTask"));
                base.ShowHelp(error);
            }

            protected override void GetCmdArgs(string[] args)
            {
                //NOTE: The index is from 1 instead of 0.
                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-d":
                            Debug = true;
                            break;
                        case "-n":
                            NumOfTasks = int.Parse(args[++i]);
                            if (NumOfTasks < 1)
                            {
                                throw new ArgumentException();
                            }
                            break;
                        case "-c":
                            Cmd = args[++i];
                            if (string.IsNullOrWhiteSpace(Cmd))
                            {
                                throw new ArgumentException("Command can not be blank!");
                            }
                            break;
                        default:
                            throw new ArgumentException($"Unknonw argument `{args[i]}'!");
                    }
                }
            }

            protected override void GetEnvArgs()
            {
                var settingNames = new (string, string)[] {
                    ("AZ_BATCH_ACCOUNT_NAME", "BatchAccountName"),
                    ("AZ_BATCH_ACCOUNT_URL", "BatchAccountUrl"),
                    ("AZ_BATCH_JOB_ID", "JobId"),
                    ("BatchAccountKey", "BatchAccountKey"),
                };
                var settings = new Dictionary<string, string>();
                foreach (var name in settingNames)
                {
                    var value = Environment.GetEnvironmentVariable(name.Item1);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException($"Environment variable {name.Item1} is not set.");
                    }
                    if (name.Item1 == "AZ_BATCH_ACCOUNT_URL")
                    {
                        //NOTE: 1) The trailing '/' will invalidate the BatchSharedKeyCredentials with the URL.
                        //      2) Batch will inproperly set the URL with a trailing '/'!
                        value = value.TrimEnd('/');
                    }
                    settings[name.Item2] = value;
                }
                BatchAccountName = settings["BatchAccountName"];
                BatchAccountUrl = settings["BatchAccountUrl"];
                BatchAccountKey = settings["BatchAccountKey"];
                JobId = settings["JobId"];
            }
        }

        static int Main(string[] args)
        {
            var settings = new Settings();
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(settings.BatchAccountUrl, settings.BatchAccountName, settings.BatchAccountKey);
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                var tasks = new List<CloudTask>();
                for (int i = 1; i <= settings.NumOfTasks; i++)
                {
                    tasks.Add(new CloudTask($"task-{i}", settings.Cmd));
                }
                batchClient.JobOperations.AddTask(settings.JobId, tasks);
            }

            return 0;
        }
    }
}
