using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using System;
using System.Collections.Generic;

namespace JobStress
{
    class Program
    {
        class Settings : Common.Settings
        {
            public string PoolId { get; protected set; } = "TestPool";

            public int NumOfJobs { get; protected set; } = 3;

            public int NumOfTasks { get; protected set; } = 5;

            protected override void ShowHelp(Exception error)
            {
                string msg = @"
Usage:
{0} [-p PoolId] [-j NumOfJobs] [-t NumOfTasksPerJob]
";
                //Console.WriteLine(String.Format(msg, Environment.GetCommandLineArgs()[0])); //TODO: This returns the .dll name instead of .exe?
                Console.WriteLine(String.Format(msg, "JobStress"));
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
                            PoolId = args[++i];
                            break;
                        case "-j":
                            NumOfJobs = int.Parse(args[++i]);
                            if (NumOfJobs < 0)
                            {
                                throw new ArgumentException();
                            }
                            break;
                        case "-t":
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
            }
        }

        static int Main(string[] args)
        {
            var settings = new Settings();
            var ts = DateTime.UtcNow.Ticks;

            Console.WriteLine($"Creating {settings.NumOfJobs} jobs, each with {settings.NumOfTasks} tasks, in pool {settings.PoolId}, time stamp {ts}...");

            var cred = new BatchSharedKeyCredentials(settings.BatchAccountUrl, settings.BatchAccountName, settings.BatchAccountKey);
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                for (int j = 1; j <= settings.NumOfJobs; j++)
                {
                    var jobId = $"JobStress_{ts}_Job_{j}";
                    var job = batchClient.JobOperations.CreateJob(
                        jobId: jobId, 
                        poolInformation: new PoolInformation() { PoolId = settings.PoolId });

                    Console.WriteLine($"Creating job {jobId}...");

                    try
                    {
                        job.Commit();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed committing job ${j}:\n{ex}");
                        continue;
                    }

                    var tasks = new List<CloudTask>();
                    for (int t = 1; t <= settings.NumOfTasks; t++)
                    {
                        tasks.Add(new CloudTask($"task-{t}", "sleep 300"));
                    }
                    batchClient.JobOperations.AddTask(jobId, tasks);
                }
            }

            return 0;
        }
    }
}

