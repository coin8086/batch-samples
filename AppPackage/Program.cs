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
        static void ShowUsage()
        {
            string msg = @"
Usage:
AppPackage <-a PackageName> [-v PackageVersion]
";
            Console.WriteLine(msg);
        }

        static int Main(string[] args)
        {
            string app = null;
            string ver = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-a":
                        app = args[++i];
                        break;
                    case "-v":
                        ver = args[++i];
                        break;
                    default:
                        Console.WriteLine($"Unknonw argument `{args[i]}'!");
                        ShowUsage();
                        return 1;
                }
            }
            if (app == null)
            {
                ShowUsage();
                return 1;
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
                        ApplicationId = app,
                        Version = ver
                    }
                };
                var packagePath = $"AZ_BATCH_APP_PACKAGE_{app}";
                if (ver != null)
                {
                    packagePath += $"#{ver}";
                }
                //NOTE: Make sure the app package containing Hello.ps1 is specified and has been uploaded to the current Batch account.
                //Or the command will fail.
                var cmd = $"cmd /c powershell -f %{packagePath}%\\Hello.ps1 AppPackage > StartTask.Output.txt";
                pool.StartTask = new StartTask()
                {
                    CommandLine = cmd
                };
                Console.WriteLine($"Creating pool `{pool.Id}' with application `{app}' version `{ver}' and start command:\n{cmd}");
                pool.Commit();
                return 0;
            }
        }
    }
}
