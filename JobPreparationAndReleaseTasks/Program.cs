using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using System;
using System.Collections.Generic;

namespace JobPreparationAndReleaseTasks
{
    class Program
    {
        class Settings : Common.Settings
        {
            public string PoolName { get; private set; }

            public int NumOfTasks { get; private set; } = 3;

            protected override void ShowHelp(Exception error)
            {
                string msg = @"
Usage:
{0}  <-p PoolName> [-n NumOfTasks] [-d]
";
                //NOTE: This returns a file name of .dll instead of .exe:
                //Console.WriteLine(String.Format(msg, Environment.GetCommandLineArgs()[0])); 
                Console.WriteLine(String.Format(msg, "JobPreparationAndReleaseTasks"));
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
                            PoolName = args[++i];
                            break;
                        case "-n":
                            NumOfTasks = int.Parse(args[++i]);
                            if (NumOfTasks < 0)
                            {
                                throw new ArgumentException();
                            }
                            break;
                        default:
                            throw new ArgumentException($"Unknonw argument `{args[i]}'!");
                    }
                }
                if (string.IsNullOrWhiteSpace(PoolName))
                {
                    throw new ArgumentException("Pool name must be specified in `-p' argument.");
                }
            }
        }

        static int Main(string[] args)
        {
            var settings = new Settings();
            var cred = new BatchSharedKeyCredentials(settings.BatchAccountUrl, settings.BatchAccountName, settings.BatchAccountKey);
            using (var batchClient = BatchClient.Open(cred))
            {
                var ts = DateTime.UtcNow.Ticks;
                var jobId = $"JobWithPreparationAndReleaseTask_{ts}";
                Console.WriteLine($"Creating job `{jobId}'...");
                var job = batchClient.JobOperations.CreateJob(
                    jobId: jobId,
                    poolInformation: new PoolInformation() { PoolId = settings.PoolName });
                //NOTE: If the Job Preparation Task fails on a node, then the task on the node will not be started.
                var jobPrepCmdLine = "cmd /c echo %AZ_BATCH_NODE_ID% > %AZ_BATCH_NODE_SHARED_DIR%\\shared_file.txt";
                var jobReleaseCmdLine = "cmd /c del %AZ_BATCH_NODE_SHARED_DIR%\\shared_file.txt";
                job.JobPreparationTask = new JobPreparationTask { CommandLine = jobPrepCmdLine };
                job.JobReleaseTask = new JobReleaseTask { CommandLine = jobReleaseCmdLine };
                job.Commit();

                if (settings.NumOfTasks > 0)
                {
                    Console.WriteLine($"Submitting {settings.NumOfTasks} task(s) to job...");
                    var cmd = "cmd /c type %AZ_BATCH_NODE_SHARED_DIR%\\shared_file.txt";
                    var tasks = new List<CloudTask>();
                    for (int i = 1; i <= settings.NumOfTasks; i++)
                    {
                        tasks.Add(new CloudTask($"task-{i}", cmd));
                    }
                    batchClient.JobOperations.AddTask(jobId, tasks);
                }
            }
            return 0;
        }
    }
}
