using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using System;
using System.Collections.Generic;

//For more on Batch Application Package, see
//https://docs.microsoft.com/en-us/azure/batch/batch-application-packages

namespace AppPackage
{
    class Program
    {
        class Settings : Common.Settings
        {
            public string Application { get; private set; }

            public string Version { get; private set; }

            protected override void ShowHelp(Exception error)
            {
                string msg = @"
Usage:
AppPackage <-a PackageName> [-v PackageVersion]
";
                Console.WriteLine(msg);
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
                            Application = args[++i];
                            break;
                        case "-v":
                            Version = args[++i];
                            break;
                        default:
                            throw new ArgumentException($"Unknonw argument `{args[i]}'!");
                    }
                }
                if (string.IsNullOrWhiteSpace(Application))
                {
                    throw new ArgumentException("Application must be specified in argument `-a'.");
                }
            }
        }


        static int Main(string[] args)
        {
            var settings = new Settings();

            // Get a Batch client using account creds
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(settings.BatchAccountUrl, settings.BatchAccountName, settings.BatchAccountKey);
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                var pool = batchClient.PoolOperations.CreatePool(
                    poolId: $"AppPackagePool_{DateTime.UtcNow.Ticks}",
                    targetDedicatedComputeNodes: 1,
                    virtualMachineSize: "standard_d1_v2",
                    virtualMachineConfiguration: new VirtualMachineConfiguration(
                        imageReference: new ImageReference(
                            publisher: "MicrosoftWindowsServer",
                            offer: "WindowsServer",
                            sku: "2019-datacenter-core"
                        ),
                        nodeAgentSkuId: "batch.node.windows amd64"
                    )
                );
                pool.ApplicationPackageReferences = new List<ApplicationPackageReference>()
                {
                    new ApplicationPackageReference()
                    {
                        ApplicationId = settings.Application,
                        Version = settings.Version
                    }
                };
                var packagePath = $"AZ_BATCH_APP_PACKAGE_{settings.Application}";
                if (!string.IsNullOrWhiteSpace(settings.Version))
                {
                    packagePath += $"#{settings.Version}";
                }
                //NOTE: Make sure the app package containing Hello.ps1 is specified and has been uploaded to the current Batch account.
                //Or the command will fail.
                var cmd = $"cmd /c powershell -f %{packagePath}%\\Hello.ps1 AppPackage > StartTask.Output.txt";
                pool.StartTask = new StartTask()
                {
                    CommandLine = cmd
                };
                Console.WriteLine($"Creating pool `{pool.Id}' with application `{settings.Application}' version `{settings.Version}' and start command:\n{cmd}");
                pool.Commit();
                return 0;
            }
        }
    }
}
