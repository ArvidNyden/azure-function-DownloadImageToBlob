using System;
using System.IO;
using System.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System.Net.Http;
using Newtonsoft.Json;

namespace DownloadImageToBlob
{
    public static class DownloadImageToBlob
    {
        [FunctionName("DownloadImageToBlob")]
        public static async void Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info($"Function triggered");

            string body = new StreamReader(req.Body).ReadToEnd();

            log.Info($"Request Body: {body}");

            var bodyContent = JsonConvert.DeserializeObject<RequestBodyContent>(body);

            if (bodyContent == null)
                throw new Exception("Unable to parse body.");

            string imageUri = bodyContent.ImageUrl;
            string referenceLink = bodyContent.Link;

            try
            {
                var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(
                    new AzureServiceTokenProvider().KeyVaultTokenCallback));

                var keyVaultUri = Environment.GetEnvironmentVariable("KeyVault");

                log.Info($"Using key vault {keyVaultUri}");

                var connectionString = await kvClient.GetSecretAsync(keyVaultUri, "StorageConnectionString");

                if (!CloudStorageAccount.TryParse(connectionString.Value, out var storagAccount))
                    throw new Exception($"Unable to parse connection string {connectionString}");

                var blobClient = storagAccount.CreateCloudBlobClient();
                var blobContainer = blobClient.GetContainerReference("rssimages");
                await blobContainer.CreateIfNotExistsAsync();

                var rawImageName = new Uri(imageUri).Segments.Last();
                var imageExtension = rawImageName.Remove(0, rawImageName.IndexOf(".", StringComparison.Ordinal));
                var imageName = new Uri(referenceLink).Segments.Last();

                log.Info($"Using image name {imageName}");

                var blobReferences = blobContainer.GetBlockBlobReference(imageName);
                blobReferences.Metadata.Add("Reference", referenceLink);
                blobReferences.Metadata.Add("Source", imageUri);
                blobReferences.Metadata.Add("Extension", imageExtension);
                blobReferences.Properties.ContentType = $"image/{imageExtension.Replace(".", string.Empty)}";

                using (var httpClient = new HttpClient())
                using (var contentStream = await httpClient.GetStreamAsync(imageUri))
                {
                    await blobReferences.UploadFromStreamAsync(contentStream);
                }
                log.Info($"Function finished. {blobReferences.Uri}");
            }
            catch (Exception ex)
            {
                log.Error($"Function failed {ex.Message}. {ex.InnerException}", ex);
            }
        }
    }
}
