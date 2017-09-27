using System;
using System.IO;
using System.Linq;
using Android.App;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using Environment = System.Environment;

namespace Plugin.MediaManager
{
    public static class MediaFileUrlHelper
    {
        /// <summary>
        /// Help to create Uri
        /// </summary>
        /// <param name="mediaFile"></param>
        /// <returns></returns>
        public static Android.Net.Uri CreateUri(this IMediaFile mediaFile)
        {            
            string url = mediaFile.Url;
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (mediaFile.Availability == ResourceAvailability.Local)
            {
                if (!File.Exists(url))
                {
                    //ltang: 2nd attemp with local path
                    string temp = GetLocalFilePath(url);
                    if (!File.Exists(temp))
                        //ltang: Final attemp with asset checking
                        temp = CopyFromAsset(url, temp);

                    if (!string.IsNullOrWhiteSpace(temp))
                    {
                        url = temp;

                        //Store the existing url
                        if (File.Exists(url))
                            mediaFile.Url = url;
                    }

                }
            }            
            Android.Net.Uri uri = Android.Net.Uri.Parse(url);            
            return uri;
        }

        private static string GetLocalFilePath(string filename)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            path = Path.Combine(path, filename);
            return path;
        }

        private static string CopyFromAsset(string assetName, string destination = null)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                throw new ArgumentNullException(nameof(assetName));
            
            if (string.IsNullOrWhiteSpace(destination))            
                destination = GetLocalFilePath(assetName);
            
            if (File.Exists(destination))
                return destination;

            string directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            try
            {
                using (var br = new BinaryReader(Application.Context.Assets.Open(assetName)))
                {
                    using (var bw = new BinaryWriter(new FileStream(destination, FileMode.Create)))
                    {
                        byte[] buffer = new byte[2048];
                        int length = 0;
                        while ((length = br.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            bw.Write(buffer, 0, length);
                        }
                    }
                }
            }
            catch (IOException)
            {
                //Asset not found
                destination = null;
            }
            catch (System.Exception e)
            {
                throw;
            }

            return destination;
        }

    }
}