using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using System;
using System.Collections.Generic;

namespace JobPreparationAndReleaseTasks
{
    class Program
    {
        static IDictionary<string, string> GetEnvSettings()
        {
            var settingNames = new string[] {
                "BatchAccountName",
                "BatchAccountUrl",
                "BatchAccountKey",
            };
            var settings = new Dictionary<string, string>();
            foreach (var name in settingNames)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Environment variable {name} is not set.");
                }
                if (name == "AZ_BATCH_ACCOUNT_URL")
                {
                    //NOTE: The trailing '/' will invalidate the BatchSharedKeyCredentials with the URL.
                    value = value.TrimEnd('/');
                }
                settings[name] = value;
            }
            return settings;
        }

        static void ShowUsage()
        {
            string msg = @"
Usage:
{0}  <-p PoolName> [-n NumOfTasks] [-d]
";
            //Console.WriteLine(String.Format(msg, Environment.GetCommandLineArgs()[0])); //TODO: This returns the .dll name instead of .exe?
            Console.WriteLine(String.Format(msg, "JobPreparationAndReleaseTasks"));
        }

        static int Main(string[] args)
        {
            int nTasks = 3;
            string poolName = null;
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-p":
                            poolName = args[++i];
                            break;
                        case "-n":
                            nTasks = int.Parse(args[++i]);
                            if (nTasks < 0)
                            {
                                throw new ArgumentException();
                            }
                            break;
                        default:
                            Console.WriteLine($"Unknonw argument `{args[i]}'!");
                            throw new ArgumentException();
                    }
                }
                if (string.IsNullOrWhiteSpace(poolName))
                {
                    throw new ArgumentException();
                }
            }
            catch
            {
                ShowUsage();
                return 1;
            }

            var settings = GetEnvSettings();
            var cred = new BatchSharedKeyCredentials(settings["BatchAccountUrl"], settings["BatchAccountName"], settings["BatchAccountKey"]);
            using (var batchClient = BatchClient.Open(cred))
            {
                var ts = DateTime.UtcNow.Ticks;
                var jobId = $"JobWithPreparationAndReleaseTask_{ts}";
                Console.WriteLine($"Creating job `{jobId}'...");
                var job = batchClient.JobOperations.CreateJob(
                    jobId: jobId,
                    poolInformation: new PoolInformation() { PoolId = poolName });
                //NOTE: If the Job Preparation Task fails on a node, then the task on the node will not be started.
                var jobPrepCmdLine = "cmd /c echo %AZ_BATCH_NODE_ID% > %AZ_BATCH_NODE_SHARED_DIR%\\shared_file.txt";
                var jobReleaseCmdLine = "cmd /c del %AZ_BATCH_NODE_SHARED_DIR%\\shared_file.txt";
                job.JobPreparationTask = new JobPreparationTask { CommandLine = jobPrepCmdLine };
                job.JobReleaseTask = new JobReleaseTask { CommandLine = jobReleaseCmdLine };
                job.Commit();

                if (nTasks > 0)
                {
                    Console.WriteLine($"Submitting {nTasks} task(s) to job...");
                    var cmd = "cmd /c type %AZ_BATCH_NODE_SHARED_DIR%\\shared_file.txt";
                    var tasks = new List<CloudTask>();
                    for (int i = 1; i <= nTasks; i++)
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
