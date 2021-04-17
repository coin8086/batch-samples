using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using System;

namespace ListNodeAgentSkus
{
    public class Program
    {
        static void Main()
        {
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
                var skus = batchClient.PoolOperations.ListNodeAgentSkus();
                skus.ForEachAsync(sku =>
                {
                    var format = @"
Image Publisher = {0}
Image Offer = {1}
Image Sku = {2}
Image Version = {3}
";
                    Console.WriteLine($"## Node Agent SKU: {sku.Id}");
                    foreach (var img in sku.VerifiedImageReferences)
                    {
                        Console.WriteLine(String.Format(format, img.Publisher, img.Offer, img.Sku, img.Version));
                    }
                }).Wait();

            }
        }
    }
}
