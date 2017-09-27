using System;
using Android.App;
using Android.Content;
using Android.Media;
using Android.Support.V4.Media;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.EventArguments;


namespace Plugin.MediaManager
{
    public class VolumeManagerImplementation : VolumeProviderCompat.Callback        
        , IVolumeManager
    {
        //private AudioManager _audioManager = null;

        public VolumeManagerImplementation()
        {
            //_audioManager = (AudioManager) Application.Context.GetSystemService(Context.AudioService);
        }

        public float CurrentVolume { get; set; }

        public float MaxVolume { get; set; }

        public event VolumeChangedEventHandler VolumeChanged;

        public override void OnVolumeChanged(VolumeProviderCompat volumeProvider)
        {
            CurrentVolume = volumeProvider.CurrentVolume;
            MaxVolume = volumeProvider.MaxVolume;

            VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(volumeProvider, Mute));
        }

        private bool _Mute;
        public bool Mute
        {
            get { return _Mute; }
            set
            {
                if (_Mute == value)
                    return;                
                _Mute = value;
            }
        }

        //private bool _Mute;
        //public bool Mute
        //{
        //    get { return _Mute; }
        //    set
        //    {
        //        if (_Mute == value)
        //            return;
        //        Adjust flag = Adjust.Unmute;
        //        if (value)
        //            flag = Adjust.Mute;
        //        //_audioManager.AdjustStreamVolume(Stream.Music, flag, 0);
        //        _audioManager.AdjustVolume(flag, 0);                
        //        _Mute = value;
        //    }
        //}
    }
}
