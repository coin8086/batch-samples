using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using System;
using System.Collections.Generic;

namespace MyJobManagerTask
{
    class Program
    {
        static void ShowUsage()
        {
            string msg = @"
Usage:
{0} [-n NumOfTasks] [-c TaskCmdLine] [-d]
";
            //Console.WriteLine(String.Format(msg, Environment.GetCommandLineArgs()[0])); //TODO: This returns the .dll name instead of .exe?
            Console.WriteLine(String.Format(msg, "MyJobManagerTask"));
        }

        static int Main(string[] args)
        {
            bool debug = false;
            int nTasks = 10;
            string cmd = "cmd /c echo Task %AZ_BATCH_TASK_ID% on Node %AZ_BATCH_NODE_ID%";
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-d":
                            debug = true;
                            break;
                        case "-n":
                            nTasks = int.Parse(args[++i]);
                            if (nTasks < 1)
                            {
                                throw new ArgumentException();
                            }
                            break;
                        case "-c":
                            cmd = args[++i];
                            if (string.IsNullOrWhiteSpace(cmd))
                            {
                                throw new ArgumentException();
                            }
                            break;
                        default:
                            Console.WriteLine($"Unknonw argument `{args[i]}'!");
                            throw new ArgumentException();
                    }
                }
            }
            catch
            {
                ShowUsage();
                return 1;
            }

            var settingNames = new (string, string)[] 
            { 
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
                    Console.WriteLine($"Environment variable {name.Item1} is not set.");
                    return 1;
                }
                if (name.Item1 == "AZ_BATCH_ACCOUNT_URL")
                {
                    //NOTE: 1) The trailing '/' will invalidate the BatchSharedKeyCredentials with the URL.
                    //      2) Batch will inproperly set the URL with a trailing '/'!
                    value = value.TrimEnd('/');
                }
                settings[name.Item2] = value;
            }

            if (debug)
            {
                Console.WriteLine("Settings from environment:");
                foreach(var pair in settings)
                {
                    Console.WriteLine($"{pair.Key} = {pair.Value}");
                }
            }

            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(settings["BatchAccountUrl"], settings["BatchAccountName"], settings["BatchAccountKey"]);
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                var tasks = new List<CloudTask>();
                for (int i = 1; i <= nTasks; i++)
                {
                    tasks.Add(new CloudTask($"task-{i}", cmd));
                }
                batchClient.JobOperations.AddTask(settings["JobId"], tasks);
            }

            return 0;
        }
    }
}
