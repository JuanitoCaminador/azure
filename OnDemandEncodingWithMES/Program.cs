using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace OnDemandEncodingWithMES
{
    class Program
    {
        // Read values from the App.config file.
        private static readonly string _mediaServicesAccountName =
            ConfigurationManager.AppSettings["MediaServicesAccountName"];
        private static readonly string _mediaServicesAccountKey =
            ConfigurationManager.AppSettings["MediaServicesAccountKey"];

        private static readonly string _mediaFiles =
                Path.GetFullPath(@"../..\Media");

        private static readonly string _presetFiles =
            Path.GetFullPath(@"../..\Presets");

        // Field for service context.
        private static CloudMediaContext _context = null;
        private static MediaServicesCredentials _cachedCredentials = null;

        // Static variables
        private static string root_folder = "C:\\Users\\Karao\\0";
        private static string text_path = "C:\\Users\\Karao\\videos.txt";


        static void Main(string[] args)
        {
            try
            {
                // Create and cache the Media Services credentials in a static class variable.
                _cachedCredentials = new MediaServicesCredentials(
                                _mediaServicesAccountName,
                                _mediaServicesAccountKey);
                // Used the chached credentials to create CloudMediaContext.
                _context = new CloudMediaContext(_cachedCredentials);

                // If you want to secure your high quality input media files with strong encryption at rest on disk,
                // use AssetCreationOptions.StorageEncrypted instead of AssetCreationOptions.None.
                string[] directories = Directory.GetDirectories(root_folder);
                foreach (string directory in directories)
                {
                    System.IO.StreamWriter txtfile = File.AppendText(text_path);
                    txtfile.WriteLine(Path.GetDirectoryName(directory));
                    txtfile.Close();
                    string[] files = Directory.GetFiles(directory);
                    foreach (string file in files)
                    {
                        string filename = Path.GetFileName(file);
                        Console.WriteLine("Upload " + filename + "\n");

                        IAsset inputAsset = UploadFile(file, AssetCreationOptions.None);

                        Console.WriteLine("Encode to adaptive bitraite MP4s and get on demand URLs.\n");

                        // If you want to secure your high quality encoded media files with strong encryption at rest on disk,
                        // use AssetCreationOptions.StorageEncrypted instead of AssetCreationOptions.None.
                        // 
                        // If your asset is AssetCreationOptions.StorageEncrypted, 
                        // make sure to call ConfigureClearAssetDeliveryPolicy defined below.

                        //IAsset encodedAsset = EncodeToAdaptiveBitrateMP4s(inputAsset, AssetCreationOptions.None);

                        // If your want to delivery a storage encrypted asset, 
                        // you must configure the asset’s delivery policy.
                        // Before your asset can be streamed, 
                        // the streaming server removes the storage encryption and 
                        //streams your content using the specified delivery policy.
                        PublishAssetGetURLs(inputAsset, false);
                        //PublishAssetGetURLs(encodedAsset);
                    }   
                }
            }
            catch (Exception exception)
            {
                // Parse the XML error message in the Media Services response and create a new
                // exception with its content.
                //exception = MediaServicesExceptionParser.Parse(exception);

                Console.Error.WriteLine(exception.Message);
            }
            finally
            {
                Console.ReadLine();
            }
        }

        static public IAsset UploadFile(string fileName, AssetCreationOptions options)
        {
            IAsset inputAsset = _context.Assets.CreateFromFile(
                fileName,
                options,
                (af, p) =>
                {
                    Console.WriteLine("Uploading '{0}' - Progress: {1:0.##}%", af.Name, p.Progress);
                });

            Console.WriteLine("Asset {0} created.", inputAsset.Id);

            return inputAsset;
        }

        static public IAsset EncodeToAdaptiveBitrateMP4s(IAsset asset, AssetCreationOptions options)
        {

            // Prepare a job with a single task to transcode the specified asset
            // into a multi-bitrate asset.

            IJob job = _context.Jobs.CreateWithSingleTask(
                "Media Encoder Standard",
                "H264 Multiple Bitrate 1080p",
                asset,
                asset.Name,
                options);

            Console.WriteLine("Submitting transcoding job...");


            // Submit the job and wait until it is completed.
            job.Submit();

            job = job.StartExecutionProgressTask(
                j =>
                {
                    Console.WriteLine("Job state: {0}", j.State);
                    Console.WriteLine("Job progress: {0:0.##}%", j.GetOverallProgress());
                },
                CancellationToken.None).Result;

            Console.WriteLine("Transcoding job finished.");

            IAsset outputAsset = job.OutputMediaAssets[0];

            return outputAsset;
        }

        static public void PublishAssetGetURLs(IAsset asset, bool onDemaindURL = true, string fileExt = "")
        {
            // Publish the output asset by creating an Origin locator for adaptive streaming,
            // and a SAS locator for progressive download.

            System.IO.StreamWriter file = File.AppendText(text_path);

            if (onDemaindURL)
            {
                _context.Locators.Create(
                    LocatorType.OnDemandOrigin,
                    asset,
                    AccessPermissions.Read,
                    TimeSpan.FromDays(30));

                // Get the Smooth Streaming, HLS and MPEG-DASH URLs for adaptive streaming,
                // and the Progressive Download URL.
                Uri smoothStreamingUri = asset.GetSmoothStreamingUri();
                Uri hlsUri = asset.GetHlsUri();
                Uri mpegDashUri = asset.GetMpegDashUri();

                // Display  the streaming URLs.
                Console.WriteLine("Use the following URLs for adaptive streaming: ");
                Console.WriteLine(smoothStreamingUri);
                file.WriteLine(smoothStreamingUri);
                //Console.WriteLine(hlsUri);
                //Console.WriteLine(mpegDashUri);
                Console.WriteLine();
            }
            else
            {
                _context.Locators.Create(
                    LocatorType.Sas,
                    asset,
                    AccessPermissions.Read,
                    TimeSpan.FromDays(30));

                IEnumerable<IAssetFile> assetFiles = asset
                    .AssetFiles
                    .ToList()
                    .Where(af => af.Name.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase));


                // Get the URls for progressive download for each specified file that was generated as a result
                // of encoding.

                List<Uri> sasUris = assetFiles.Select(af => af.GetSasUri()).ToList();

                // Display the URLs for progressive download.
                Console.WriteLine("Use the following URLs for progressive download.");
                sasUris.ForEach(uri => file.WriteLine(uri));
                Console.WriteLine();
            }
            file.Close();
        }
    }
}
