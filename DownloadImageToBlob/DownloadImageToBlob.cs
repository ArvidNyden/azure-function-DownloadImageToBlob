using System;
using System.IO;
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
        public static async void Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info($"Function triggered");

            string body = new StreamReader(req.Body).ReadToEnd();
            var bodyContent = JsonConvert.DeserializeObject<RequestBodyContent>(body);

            string imageUri = bodyContent.ImageUrl;
            string referenceLink = bodyContent.Link;

            log.Info($"Request Body: {imageUri}");

            try
            {
                var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(
                    new AzureServiceTokenProvider().KeyVaultTokenCallback));

                var keyVaultUri = Environment.GetEnvironmentVariable("KeyVault");

                log.Info($"Using key vault {keyVaultUri}");

                var connectionString = await kvClient.GetSecretAsync(keyVaultUri, "StorageConnectionString");

                log.Info($"Using connection string {connectionString.Value}");

                if (!CloudStorageAccount.TryParse(connectionString.Value, out var storagAccount))
                    throw new Exception($"Unable to parse connection string {connectionString}");

                var blobClient = storagAccount.CreateCloudBlobClient();
                var blobContainer = blobClient.GetContainerReference("rssimages");
                await blobContainer.CreateIfNotExistsAsync();

                var imageName = imageUri.Substring(imageUri.LastIndexOf("/") + 1, imageUri.IndexOf("?") - imageUri.LastIndexOf("/") - 1);

                log.Info($"Using image name {imageName}");

                var blobReferences = blobContainer.GetBlockBlobReference(imageName);
                blobReferences.Metadata.Add("Reference", referenceLink);
                blobReferences.Metadata.Add("Source", imageUri);

                using (var httpClient = new HttpClient())
                using (var contentStream = await httpClient.GetStreamAsync(imageUri))
                {
                    await blobReferences.UploadFromStreamAsync(contentStream);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Function failed {ex.Message}. {ex.InnerException}", ex);
            }
        }
    }
}
