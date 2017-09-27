using System;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;

namespace Plugin.MediaManager.Abstractions.Implementations
{
    public class MediaFile : IMediaFile
    {
        public MediaFile() : this(String.Empty, default(MediaFileType))
        {
        }

        public MediaFile(string url) : this(url, default(MediaFileType))
        {
        }

        public MediaFile(string url, MediaFileType type) : this(url, type, default(ResourceAvailability))
        {
            GuessAvailability();
        }

        public MediaFile(string url, MediaFileType type, ResourceAvailability availability)
        {
            Url = url;
            Type = type;
            Availability = availability;
        }        

        public Guid Id { get; set; } = Guid.NewGuid();

        public MediaFileType Type { get; set; }

        public ResourceAvailability Availability { get; set; }
        
        public bool GuessAvailability()
        {
            if (!HasUrl)
                return false;

            var availability = ResourceAvailability.Remote;

            Uri uri;
            bool bValid = Uri.TryCreate(Url, UriKind.Absolute, out uri);
            if (!bValid || uri.IsFile)
                availability = ResourceAvailability.Local;

            Availability = availability;
            return true;
        }

        public IMediaFileMetadata Metadata { get; set; } = new MediaFileMetadata();

        public string Url { get; set; }

        public bool HasUrl => !string.IsNullOrWhiteSpace(Url);

        private bool _metadataExtracted;
        public bool MetadataExtracted
        {
            get
            {
                return _metadataExtracted;
            }
            set
            {
                _metadataExtracted = value;
                MetadataUpdated?.Invoke(this, new MetadataChangedEventArgs(Metadata));
            } 
        }

        public event MetadataUpdatedEventHandler MetadataUpdated;

        public override string ToString()
        {
            return $"{Type}, {Availability}, {Url}";
        }
    }
}