using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using System;
using System.Diagnostics;

namespace WorkerRole1
{
    public class FileMan
    {
        public FileMan() { }
        public static CloudStorageAccount CloudStorageAccountstorageAccount;
        public static CloudFileClient CloudFileClientfileClient { get; private set; }
        public static CloudFileShare CloudFileSharefileShare { get; private set; }
        public static FileSharePermissions FileSharePermissionsfileSharePermissions { get; private set; }
        public static object SharedAccessFilePolicysharedAccessFilePolicy { get; private set; }
        // private static object CloudFileClientfileClient;
        public void  makeFiles(string SPC,string structureContent,string behaviourContent)
        {
            try
            {
                CloudStorageAccountstorageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=automatedcrawlersto;AccountKey=Tz8dWpWzAycx6P/i9zmHogpZesrNWqfXLNMKsvsjRen6BwqCT5+K+r6qyFNS1zx9SvbV7aZ5T+fsTSz7ncRiAQ==;EndpointSuffix=core.windows.net");
                CloudFileClientfileClient = CloudStorageAccountstorageAccount.CreateCloudFileClient();
                CloudFileSharefileShare = CloudFileClientfileClient.GetShareReference("resourcesfiles");
                if (CloudFileSharefileShare.Exists())
                {
                    string policyName = "DemoPolicy0";
                    FileSharePermissionsfileSharePermissions = CloudFileSharefileShare.GetPermissions(); 
                    CloudFileSharefileShare.SetPermissions(FileSharePermissionsfileSharePermissions);
                    CloudFileDirectory rootDirectory = CloudFileSharefileShare.GetRootDirectoryReference();
                    if (rootDirectory.Exists())
                    {
                        CloudFileDirectory customDirectory = rootDirectory.GetDirectoryReference(SPC);
                        if (!customDirectory.Exists())
                        {
                            customDirectory.Create();
                        }
                        CloudFile structure = customDirectory.GetFileReference(SPC + ".structure.json");
                        string sasToken = structure.GetSharedAccessSignature(null, policyName);
                        Uri fileSASUrl = new Uri(structure.StorageUri.PrimaryUri.ToString() + sasToken);
                        CloudFile structureFile = new CloudFile(fileSASUrl);
                        structureFile.UploadText(structureContent);

                        CloudFile behaviour = customDirectory.GetFileReference(SPC + ".behaviour.json");
                        string sasTokenB = behaviour.GetSharedAccessSignature(null, policyName);
                        Uri fileSASUrlB = new Uri(behaviour.StorageUri.PrimaryUri.ToString() + sasTokenB);
                        CloudFile behaviourFile = new CloudFile(fileSASUrlB);
                        behaviourFile.UploadText(behaviourContent);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceInformation("Error: " + ex.Message);

            }
            finally
            {

            }
        }

        public void makeCacheFiles(string SPC)
        {
            try
            {
                CloudStorageAccountstorageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=automatedcrawlersto;AccountKey=Tz8dWpWzAycx6P/i9zmHogpZesrNWqfXLNMKsvsjRen6BwqCT5+K+r6qyFNS1zx9SvbV7aZ5T+fsTSz7ncRiAQ==;EndpointSuffix=core.windows.net");
                CloudFileClientfileClient = CloudStorageAccountstorageAccount.CreateCloudFileClient();
                CloudFileSharefileShare = CloudFileClientfileClient.GetShareReference("automatedcache");
                if (CloudFileSharefileShare.Exists())
                {
                    FileSharePermissionsfileSharePermissions = CloudFileSharefileShare.GetPermissions();
                    CloudFileSharefileShare.SetPermissions(FileSharePermissionsfileSharePermissions);
                    CloudFileDirectory rootDirectory = CloudFileSharefileShare.GetRootDirectoryReference();
                    if (rootDirectory.Exists())
                    {
                        CloudFileDirectory customDirectory = rootDirectory.GetDirectoryReference(SPC);
                        if (!customDirectory.Exists())
                        {
                            customDirectory.Create();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceInformation("Error: " + ex.Message);
            }
            finally
            {

            }
        }
    }
}
