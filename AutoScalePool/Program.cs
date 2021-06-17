using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace AutoScalePool
{
    class Program
    {
        class Settings : Common.Settings
        {
            public string AutoScaleFormula { get; protected set; } = @"
// Get Active, Running and Pending tasks of the last sample
active = val($ActiveTasks.GetSample(1), 0);
pending = val($PendingTasks.GetSample(1), 0);
running = val($RunningTasks.GetSample(1), 0);

// In this example, the pool size is adjusted based on the number of tasks in the queue.
// Note that both comments and line breaks are acceptable in formula strings.

// Get pending tasks for the past 5 minutes.
$samples = $ActiveTasks.GetSamplePercent(TimeInterval_Minute * 5);

// If we have fewer than 70 percent data points, we use the last sample point, otherwise we use the maximum of last sample point and the history average.
$tasks = $samples < 70 ? max(0, $ActiveTasks.GetSample(1)) :
    max( $ActiveTasks.GetSample(1), avg($ActiveTasks.GetSample(TimeInterval_Minute * 5)));

// If number of pending tasks is not 0, set targetVM to pending tasks, otherwise half of current dedicated.
$targetVMs = $tasks > 0 ? $tasks : max(0, $TargetDedicatedNodes / 2);

// The pool size is capped at 5.
cappedPoolSize = 5;
$TargetDedicatedNodes = max(0, min($targetVMs, cappedPoolSize));

// Set node deallocation mode - keep nodes active only until tasks finish
$NodeDeallocationOption = taskcompletion;
";

            //The minimum allowed is 5 min, and default is 15 min.
            public int AutoScaleEvaluationInterval { get; } = 300;

            public bool KeepPoolAndJob { get; protected set; } = false;

            public int NumOfTasks { get; protected set; } = 10;

            public int SecondsToSleep { get; protected set; } = 180;

            public int CheckInterval { get; } = 300;

            public int CheckTimes { get; } = 4;

            protected override void ShowHelp(Exception error)
            {
                string msg = @"
Usage:
{0} [-a autoscaleFormula] [-f autoscaleFormulaFile] [-n numberOfTasksToSubmit] [-s secondsToSleepForTask] [-k] 

    -k: Keep pool and job after the program. They'll be deleted by default.
";
                //Console.WriteLine(String.Format(msg, Environment.GetCommandLineArgs()[0])); //TODO: This returns the .dll name instead of .exe?
                Console.WriteLine(String.Format(msg, "AutoScalePool"));
                base.ShowHelp(error);
            }

            protected override void GetCmdArgs(string[] args)
            {
                //NOTE: The index is from 1 instead of 0.
                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-a":
                            AutoScaleFormula = args[++i];
                            if (string.IsNullOrWhiteSpace(AutoScaleFormula))
                            {
                                throw new ArgumentException("Autoscale Formua can not be blank!");
                            }
                            break;
                        case "-f":
                            AutoScaleFormula = File.ReadAllText(args[++i]);
                            if (string.IsNullOrWhiteSpace(AutoScaleFormula))
                            {
                                throw new ArgumentException("Autoscale Formua can not be blank!");
                            }
                            break;
                        case "-n":
                            NumOfTasks = int.Parse(args[++i]);
                            if (NumOfTasks < 1)
                            {
                                throw new ArgumentException();
                            }
                            break;
                        case "-s":
                            SecondsToSleep = int.Parse(args[++i]);
                            if (SecondsToSleep < 0)
                            {
                                throw new ArgumentException();
                            }
                            break;
                        case "-k":
                            KeepPoolAndJob = true;
                            break;
                        default:
                            throw new ArgumentException($"Unknonw argument `{args[i]}'!");
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            var settings = new Settings();
            var cred = new BatchSharedKeyCredentials(settings.BatchAccountUrl, settings.BatchAccountName, settings.BatchAccountKey);
            using (var client = BatchClient.Open(cred))
            {
                string poolId = null;
                string jobId = null;
                try
                {
                    var ts = DateTime.UtcNow.Ticks;

                    // Select an image with container support
                    var imageReference = new ImageReference(
                        publisher: "microsoft-azure-batch",
                        offer: "ubuntu-server-container",
                        sku: "20-04-lts",
                        version: "latest");

                    var virtualMachineConfiguration = new VirtualMachineConfiguration(
                        imageReference: imageReference,
                        nodeAgentSkuId: "batch.node.ubuntu 20.04");

                    // Create pool
                    poolId = $"AutoScaleSamplePool_{ts}";
                    var pool = client.PoolOperations.CreatePool(
                        poolId: poolId,
                        virtualMachineSize: "STANDARD_D1_V2",
                        virtualMachineConfiguration: virtualMachineConfiguration);
                    pool.AutoScaleEnabled = true;
                    pool.AutoScaleFormula = settings.AutoScaleFormula;
                    pool.AutoScaleEvaluationInterval = TimeSpan.FromSeconds(settings.AutoScaleEvaluationInterval);
                    Console.WriteLine($"Creating pool `{poolId}' with autoscale formula:\n{settings.AutoScaleFormula}");
                    pool.Commit();

                    // Submit job to pool
                    jobId = $"AutoScalueSampleJob_{ts}";
                    var job = client.JobOperations.CreateJob(
                        jobId: jobId,
                        poolInformation: new PoolInformation() { PoolId = poolId });
                    Console.WriteLine($"Creating job `{jobId}'...");
                    job.Commit();

                    //Add tasks to job
                    var tasks = new List<CloudTask>();
                    var cmd = $"sleep {settings.SecondsToSleep}";
                    for (int i = 1; i <= settings.NumOfTasks; i++)
                    {
                        var task = new CloudTask(id: $"task-{i}", commandline: cmd);
                        tasks.Add(task);
                    }
                    Console.WriteLine($"Submitting {settings.NumOfTasks} tasks, each of which will sleep {settings.SecondsToSleep} seconds...");
                    job.Refresh();
                    job.AddTask(tasks);
                    job.CommitChanges();

                    for (int i = 0; i < settings.CheckTimes; i++)
                    {
                        Console.WriteLine("\nEvaluating...");
                        EvalueateAutoScaleForumla(client, poolId, settings.AutoScaleFormula);

                        Console.WriteLine("\nGetting nodes...");
                        GetNodesInPool(client, poolId);

                        if (i < 3)
                        {
                            Console.WriteLine($"\nSleeping {settings.CheckInterval} seconds and evaluate again...");
                            Thread.Sleep(TimeSpan.FromSeconds(settings.CheckInterval));
                        }
                    }
                }
                finally
                {
                    if (!settings.KeepPoolAndJob)
                    {
                        if (jobId != null)
                        {
                            Console.WriteLine($"Deleting job `{jobId}'...");
                            client.JobOperations.DeleteJob(jobId);
                        }
                        if (poolId != null)
                        {
                            Console.WriteLine($"Deleting pool `{poolId}'...");
                            client.PoolOperations.DeletePool(poolId);
                        }
                    }
                }

            }
        }

        static void EvalueateAutoScaleForumla(BatchClient client, string poolId, string formula)
        {
            //This only evaluates the formula without applying it to pool.
            var result = client.PoolOperations.EvaluateAutoScale(poolId, formula);
            Console.WriteLine($"Evaluated at {result.Timestamp}");
            if (result.Error is null)
            {
                var exprs = result.Results.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in exprs)
                {
                    Console.WriteLine(item);
                }
            }
            else
            {
                Console.WriteLine($"Error when evaluating the formula:\n{result.Error}");
            }
        }

        static void GetNodesInPool(BatchClient client, string poolId)
        {
            var nodes = client.PoolOperations.ListComputeNodes(poolId);
            var list = nodes.ToListAsync().GetAwaiter().GetResult();
            Console.WriteLine($"Nodes in pool: {list.Count}");
        }
    }
}
