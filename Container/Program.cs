using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContainerSample
{
    class Program
    {
        class Settings : Common.Settings
        {
            public bool KeepPoolAndJob { get; protected set; }

            protected override void ShowHelp(Exception error)
            {
                string msg = @"
Usage:
{0} [-k]

    -k: Keep pool and job after executing the program. They'll be deleted by default. 
";
                //Console.WriteLine(String.Format(msg, Environment.GetCommandLineArgs()[0])); //TODO: This returns the .dll name instead of .exe?
                Console.WriteLine(String.Format(msg, "ContainerSample"));
                base.ShowHelp(error);
            }

            protected override void GetCmdArgs(string[] args)
            {
                //NOTE: The index is from 1 instead of 0.
                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "-k":
                            KeepPoolAndJob = true;
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

                    // Specify container configuration. This is required even though there are no prefetched images.
                    var containerConfig = new ContainerConfiguration();

                    var virtualMachineConfiguration = new VirtualMachineConfiguration(
                        imageReference: imageReference,
                        nodeAgentSkuId: "batch.node.ubuntu 20.04")
                    {
                        ContainerConfiguration = containerConfig
                    };

                    // Create pool
                    poolId = $"ContainerSamplePool_{ts}";
                    var pool = client.PoolOperations.CreatePool(
                        poolId: poolId,
                        targetDedicatedComputeNodes: 1,
                        virtualMachineSize: "STANDARD_D1_V2",
                        virtualMachineConfiguration: virtualMachineConfiguration);
                    Console.WriteLine($"Creating pool `{poolId}'...");
                    pool.Commit();

                    // Submit job with a task running in container
                    jobId = $"ContainerSampleJob_{ts}";
                    var job = client.JobOperations.CreateJob(
                        jobId: jobId,
                        poolInformation: new PoolInformation() { PoolId = poolId });
                    Console.WriteLine($"Creating job `{jobId}'...");
                    job.Commit();

                    var taskContainerSettings = new TaskContainerSettings(
                        imageName: "hello-world",   // Docker hello-world image
                        containerRunOptions: "--rm");

                    var task = new CloudTask(
                        id: "task",
                        commandline: "") // Run the default ENTRYPOINT of the image
                    {
                        ContainerSettings = taskContainerSettings
                    };

                    Console.WriteLine("Submitting task to job...");
                    job.Refresh();
                    job.AddTask(task);
                    job.CommitChanges();

                    // Wait the task to finish
                    Console.WriteLine("Waiting task to finish...");
                    // NOTE:
                    // Refresh makes the job instance "bound" from "unbound". However, the Refresh method of
                    // a task instance doesn't make a task "bound". So here we get the tasks from ListTasks method.
                    var tasks = job.ListTasks();
                    var monitor = client.Utilities.CreateTaskStateMonitor();
                    monitor.WaitAll(tasks, TaskState.Completed, TimeSpan.FromMinutes(15));

                    task = tasks.First();
                    Console.WriteLine("STDOUT:");
                    Console.WriteLine(task.GetNodeFile(Constants.StandardOutFileName).ReadAsString());
                    Console.WriteLine("STDERR:");
                    Console.WriteLine(task.GetNodeFile(Constants.StandardErrorFileName).ReadAsString());
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

            return 0;
        }
    }
}
