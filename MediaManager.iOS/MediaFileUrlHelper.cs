using System;
using System.IO;
using Foundation;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;

namespace Plugin.MediaManager
{
    public static class MediaFileUrlHelper
    {
        public static NSUrl CreateUri(this IMediaFile mediaFile)
        {
            var isLocallyAvailable = mediaFile.Availability == ResourceAvailability.Local;

            string stUrl = mediaFile.Url;

            if (isLocallyAvailable)
            {
                if (!File.Exists(stUrl))
                {
                    //ltang: 2nd attemp with local path
                    string temp = GetLocalFilePath(stUrl);
                    if (!File.Exists(temp))
                        //ltang: Final attemp with bundle path
                        temp = GetBundleFilePath(stUrl);

                    if (!string.IsNullOrWhiteSpace(temp))
                    {
                        stUrl = temp;

                        //Store the existing url
                        if (File.Exists(stUrl))
                            mediaFile.Url = stUrl;
                    }
                }                
            }

            var url = isLocallyAvailable ? new NSUrl(stUrl, false) : new NSUrl(stUrl);
            return url;
        }

        private static string GetLocalFilePath(string filename)
        {
            string docFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            filename = Path.GetFileName(filename);
            string path = Path.Combine(docFolder, filename);
            return path;
        }

        private static string GetBundleFilePath(string filename)
        {
            string filenameNoExtension = Path.GetFileNameWithoutExtension(filename);
            string extension = Path.GetExtension(filename);
            if (!string.IsNullOrWhiteSpace(extension))
                extension = extension.Remove(0, 1);
            string pathInBundle = NSBundle.MainBundle.PathForResource(filenameNoExtension, extension);
            return pathInBundle;
        }

        //private static string CopyFromMainBundle(string assetName, string destination = null)
        //{
        //    if (string.IsNullOrWhiteSpace(assetName))
        //        throw new ArgumentNullException(nameof(assetName));

        //    if (string.IsNullOrWhiteSpace(destination))            
        //        destination = GetLocalFilePath(assetName);
            
        //    if (File.Exists(destination))
        //        return destination;
                        
        //    string pathInBundle = GetBundleFilePath(assetName);
        //    if (!File.Exists(pathInBundle))
        //        throw new FileNotFoundException($"{pathInBundle}");

        //    string directory = Path.GetDirectoryName(destination);
        //    if (!string.IsNullOrWhiteSpace(directory))
        //        Directory.CreateDirectory(directory);

        //    try
        //    {
        //        File.Copy(pathInBundle, destination);
        //    }
        //    catch (System.Exception e)
        //    {
        //        throw;
        //    }
        //    return destination;
        //}        
    }
}
