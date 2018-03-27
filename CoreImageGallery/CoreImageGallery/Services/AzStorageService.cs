﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CoreImageGallery.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace CoreImageGallery.Services
{
    public class AzStorageService : IStorageService
    {
        private const string _imagePrefix = "img_";
        private const string _uploadContainerName = "images";
        private const string downloadContainerName = "images-watermarked";

        private readonly CloudStorageAccount _account;
        private readonly CloudBlobClient _client;
        private readonly string _connectionString;
        private readonly CloudBlobContainer _uploadContainer;
        private readonly CloudBlobContainer _downloadContainer;

        public AzStorageService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("AzureStorageConnectionString");
            _account = CloudStorageAccount.Parse(_connectionString);
            _client = _account.CreateCloudBlobClient();
            _uploadContainer = _client.GetContainerReference(_uploadContainerName);
            _downloadContainer = _client.GetContainerReference(downloadContainerName);
        }

        public async Task InitializeBlobStorageAsync()
        {
            await _downloadContainer.CreateIfNotExistsAsync();

            var permissions = await _downloadContainer.GetPermissionsAsync();
            if (permissions.PublicAccess == BlobContainerPublicAccessType.Off || permissions.PublicAccess == BlobContainerPublicAccessType.Unknown)
            {
                // If blob isn't public, we can't directly link to the pictures
                await _downloadContainer.SetPermissionsAsync(new BlobContainerPermissions() { PublicAccess = BlobContainerPublicAccessType.Blob });
            }
        }

        public async Task<Image> AddImageAsync(Stream stream, string fileName)
        {
            await _uploadContainer.CreateIfNotExistsAsync();

            fileName = _imagePrefix + fileName;
            var imageBlob = _uploadContainer.GetBlockBlobReference(fileName);
            await imageBlob.UploadFromStreamAsync(stream);

            return new Image()
            {
                FileName = fileName,
                ImagePath = imageBlob.Uri.ToString()
            };
        }

        public async Task<IEnumerable<Image>> GetImagesAsync()
        {
            await InitializeBlobStorageAsync();

            var imageList = new List<Image>();
            var token = new BlobContinuationToken();
            var blobList = await _downloadContainer.ListBlobsSegmentedAsync(_imagePrefix, true, BlobListingDetails.All, 100, token, null, null);
            
            foreach (var blob in blobList.Results)
            {
                var image = new Image
                {
                    ImagePath = blob.Uri.ToString()
                };

                imageList.Add(image);
            }

            return imageList;
        }
    }
}
