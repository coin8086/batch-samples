﻿using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using System;

namespace AutoPool
{
    public class Program
    {
        static int Main(string[] args)
        {
            bool keepPool = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-k":
                        keepPool = true;
                        break;
                    default:
                        Console.WriteLine($"Unknonw argument `{args[i]}'!");
                        return 1;
                }
            }
            
            // Batch account credentials
            var BatchAccountName = Environment.GetEnvironmentVariable("BatchAccountName");
            var BatchAccountKey = Environment.GetEnvironmentVariable("BatchAccountKey");
            var BatchAccountUrl = Environment.GetEnvironmentVariable("BatchAccountUrl");

            if (String.IsNullOrEmpty(BatchAccountName) ||
                String.IsNullOrEmpty(BatchAccountKey) ||
                String.IsNullOrEmpty(BatchAccountUrl))
            {
                throw new InvalidOperationException("One or more Batch credentials are not specified.");
            }

            // Get a Batch client using account creds
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(BatchAccountUrl, BatchAccountName, BatchAccountKey);
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                CloudJob job = null;
                try
                {
                    //Create a job with an auto pool
                    Console.WriteLine("Creating job...");
                    job = batchClient.JobOperations.CreateJob();
                    job.Id = $"AutoPoolJob_{DateTime.UtcNow.Ticks}";
                    job.PoolInformation = new PoolInformation()
                    {
                        AutoPoolSpecification = new AutoPoolSpecification()
                        {
                            AutoPoolIdPrefix = "AutoPool",
                            PoolSpecification = new PoolSpecification()
                            {
                                TargetDedicatedComputeNodes = 1,
                                VirtualMachineSize = "standard_d1_v2",
                                VirtualMachineConfiguration = new VirtualMachineConfiguration(
                                    //See https://docs.microsoft.com/en-us/cli/azure/batch/pool/supported-images?view=azure-cli-latest
                                    //for more available images & their nodeAgentSkuId
                                    imageReference: new ImageReference(
                                        publisher: "MicrosoftWindowsServer",
                                        offer: "WindowsServer",
                                        sku: "2019-datacenter-smalldisk"
                                    ),
                                    nodeAgentSkuId: "batch.node.windows amd64"
                                )
                            },
                            PoolLifetimeOption = PoolLifetimeOption.Job,
                            KeepAlive = keepPool,
                        }
                    };
                    try
                    {
                        job.Commit();
                        //job = batchClient.JobOperations.GetJob(job.Id); //TODO: How to get the auto pool id?
                        Console.WriteLine($"Job `{job.Id}' is created with auto pool `{job.PoolInformation.PoolId}'.");
                    }
                    catch (BatchException be)
                    {
                        if (be.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.JobExists)
                        {
                            Console.WriteLine($"Job `{job.Id}' already exists!");
                            job = null; //Do not delete existing job in the final clause.
                        }
                        throw;
                    }

                    //Add task to job
                    Console.WriteLine("Adding tasks to job...");
                    batchClient.JobOperations.AddTask(job.Id, new CloudTask("task1", "cmd /c echo Hello, Auto Pool!"));

                    //Monitor the job
                    Console.WriteLine("Waiting job to finish...");
                    var tasks = batchClient.JobOperations.ListTasks(job.Id);
                    var monitor = batchClient.Utilities.CreateTaskStateMonitor();
                    monitor.WaitAll(tasks, TaskState.Completed, TimeSpan.FromMinutes(15));

                    //Check job output
                    Console.WriteLine($"Job `{job.Id}' finished with:");
                    foreach (var task in tasks)
                    {
                        Console.WriteLine($"Task: {task.Id}");
                        Console.WriteLine($"Node: {task.ComputeNodeInformation.ComputeNodeId}");
                        Console.WriteLine($"Pool: {task.ComputeNodeInformation.PoolId}");
                        Console.WriteLine($"StdOut:");
                        Console.WriteLine(task.GetNodeFile(Constants.StandardOutFileName).ReadAsString());
                        Console.WriteLine($"StdErr:");
                        Console.WriteLine(task.GetNodeFile(Constants.StandardErrorFileName).ReadAsString());
                    }
                }
                finally
                {
                    if (job?.Id != null)
                    {
                        Console.WriteLine($"Deleting job `{job.Id}'...");
                        batchClient.JobOperations.DeleteJob(job.Id);
                    }
                }
            }
            return 0;
        }
    }
}
