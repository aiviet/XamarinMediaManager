using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Java.Lang;
using Java.Util.Concurrent;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;
using Plugin.MediaManager.Abstractions.Implementations;


namespace Plugin.MediaManager
{
    public class VideoPlayerImplementation : Java.Lang.Object, 
        IVideoPlayer,        
        MediaPlayer.IOnCompletionListener,
        MediaPlayer.IOnErrorListener,
        MediaPlayer.IOnPreparedListener,
        MediaPlayer.IOnInfoListener
    {        
        private MediaPlayer _mediaPlayer;
        private AudioManager _audioManager = null;

        public VideoPlayerImplementation()
        {
            _audioManager = (AudioManager)Application.Context.GetSystemService(Context.AudioService);

            _status = MediaPlayerStatus.Stopped;
			StatusChanged += (sender, args) => OnPlayingHandler(args);            
        }        

        private bool isPlayerReady = false;        

        private IScheduledExecutorService _executorService = Executors.NewSingleThreadScheduledExecutor();
		private IScheduledFuture _scheduledFuture;

		private void OnPlayingHandler(StatusChangedEventArgs args)
		{
			if (args.Status == MediaPlayerStatus.Playing)
			{
				CancelPlayingHandler();
				StartPlayingHandler();
			}
			if (args.Status == MediaPlayerStatus.Stopped || args.Status == MediaPlayerStatus.Failed || args.Status == MediaPlayerStatus.Paused)
				CancelPlayingHandler();
		}
        
		private void CancelPlayingHandler()
		{
			_scheduledFuture?.Cancel(false);
		}

		private void StartPlayingHandler()
		{
			var handler = new Handler();
			var runnable = new Runnable(() => { handler.Post(OnPlaying); });
			if (!_executorService.IsShutdown)
			{                
                _scheduledFuture = _executorService.ScheduleAtFixedRate
                    (runnable
                    , 100
                    , MediaServiceBase.Event_Firing_MiliSec
                    , TimeUnit.Milliseconds);
			}
		}

		private void OnPlaying()
		{
		    if (!IsReadyRendering)
		        CancelPlayingHandler(); //RenderSurface is no longer valid => Cancel the periodic firing		    

            TimeSpan position = this.Position;
            var progress = (position.TotalSeconds / Duration.TotalSeconds);			
			var duration = Duration;

			PlayingChanged?.Invoke(this, new PlayingChangedEventArgs(
			    !double.IsInfinity(progress) ? progress : 0,
				position.TotalSeconds >= 0 ? position : TimeSpan.Zero,
				duration.TotalSeconds >= 0 ? duration : TimeSpan.Zero));
		}

        #region IVideoPlayer
        /// <summary>
        /// True when RenderSurface has been initialized and ready for rendering
        /// </summary>
        public bool IsReadyRendering => RenderSurface != null && !RenderSurface.IsDisposed;

        VideoView VideoViewCanvas => RenderSurface as VideoView;

        private IVideoSurface _renderSurface;
        public IVideoSurface RenderSurface { 
            get {
                return _renderSurface;
            } 
            set {
                if (!(value is VideoSurface))
                    throw new ArgumentException("Not a valid video surface");

                if (_renderSurface == value)
                    return;

                var canvas = (VideoSurface)value;                
                _renderSurface = canvas;
                
                //New canvas object => need initialization
                isPlayerReady = false;
            }
        }

		private VideoAspectMode _aspectMode;
		public VideoAspectMode AspectMode { 
			get
			{
				return _aspectMode;
			}
			set {
				//TODO: Wrap videoplayer to respect aspectmode
				_aspectMode = value;
			} 
		}

        private ReadOnlyCollection<IMediaTrackInfo> _TrackInfoList;
        public IReadOnlyCollection<IMediaTrackInfo> TrackInfoList => _TrackInfoList;

        public int TrackCount => _TrackInfoList?.Count ?? -1;

        public int GetSelectedTrack(Abstractions.Enums.MediaTrackType trackType)
        {
            if (trackType != Abstractions.Enums.MediaTrackType.Audio)
                throw new NotSupportedException($"{trackType}");

            if (_mediaPlayer == null)
                return -1;

            int trackIndex = _mediaPlayer.GetSelectedTrack(ToMediaTrackTypeAndroid(trackType));
            return trackIndex;
        }

        private volatile bool _IsMuted = false;
        public bool IsMuted
        {
            get { return _IsMuted; }
            set
            {
                if (_IsMuted == value)
                    return;

                float volumeValue = 0.0f;
                if (!value)
                {
                    //https://developer.xamarin.com/api/member/Android.Media.AudioManager.GetStreamVolume/p/Android.Media.Stream/
                    //https://stackoverflow.com/questions/17898382/audiomanager-getstreamvolumeaudiomanager-stream-music-returns-0
                    Stream streamType = Stream.Music;
                    int volumeMax = _audioManager.GetStreamMaxVolume(streamType);
                    int volume = _audioManager.GetStreamVolume(streamType);

                    //ltang: Unmute with the current volume
                    volumeValue = (float) volume / volumeMax;
                }

                if (SetVolume(volumeValue, volumeValue))
                {
                    _IsMuted = value;
                }
                else
                {
                    //ltang: Failed to mute
                }
            }
        }

        public bool SetVolume(float leftVolume, float rightVolume)
        {
            bool bOk = false;
            try
            {
                _mediaPlayer?.SetVolume(leftVolume, rightVolume);
                bOk = true;
            }
            catch (Java.Lang.IllegalStateException e)
            {
                //ltang: Wrong state to set volume
                Console.WriteLine(e);
                //throw;
            }
            catch (System.Exception e)
            {                
                throw;
            }
            return bOk;
        }

        private int? _lastSelectedTrackIndex = null;
        /// <summary>
        /// Do NOT call this in UI thread otherwise it will freeze the video rendering
        /// </summary>
        /// <param name="trackIndex"></param>
        /// <returns></returns>
        public Task<bool> SetTrack(int trackIndex)
        {
            if (_mediaPlayer == null || trackIndex < 0)
                return Task.FromResult(false);            
            if (_lastSelectedTrackIndex != null && _lastSelectedTrackIndex == trackIndex)
                return Task.FromResult(true);
            var task = Task.Run<bool>(() =>
            {                
                try
                {
                    int count = TrackCount;
                    if (count <= 0 || trackIndex >= count)
                        return false;

                    _mediaPlayer?.SelectTrack(trackIndex);

                    Console.WriteLine($"SelectTrack to {trackIndex}");

                    _lastSelectedTrackIndex = trackIndex;

                    return true;
                }
                catch (System.Exception ex)
                {
                    throw;
                }
            });
            return task;
        }
        #endregion      

        public IMediaFile CurrentFile { get; set; }
        private Android.Net.Uri currentUri { get; set; }

		public Dictionary<string, string> RequestHeaders { get; set; }

        public bool UseNativeControls = false;

        public event BufferingChangedEventHandler BufferingChanged;
        public event MediaFailedEventHandler MediaFailed;
        public event MediaFileFailedEventHandler MediaFileFailed;
        public event MediaFileChangedEventHandler MediaFileChanged;
        public event MediaFinishedEventHandler MediaFinished;
        public event PlayingChangedEventHandler PlayingChanged;
        public event StatusChangedEventHandler StatusChanged;

		protected virtual void OnStatusChanged(StatusChangedEventArgs e)
		{
			StatusChanged?.Invoke(this, e);
		}

		protected virtual void OnPlayingChanged(PlayingChangedEventArgs e)
		{
			PlayingChanged?.Invoke(this, e);
		}

		protected virtual void OnBufferingChanged(BufferingChangedEventArgs e)
		{
			BufferingChanged?.Invoke(this, e);
		}

		protected virtual void OnMediaFinished(MediaFinishedEventArgs e)
		{
            //Final update on completion
            OnPlaying();

            MediaFinished?.Invoke(this, e);
		}

		protected virtual void OnMediaFailed(MediaFailedEventArgs e)
		{
			MediaFailed?.Invoke(this, e);
		}

		protected virtual void OnMediaFileChanged(MediaFileChangedEventArgs e)
		{		    
            MediaFileChanged?.Invoke(this, e);
		}

		protected virtual void OnMediaFileFailed(MediaFileFailedEventArgs e)
		{
			MediaFileFailed?.Invoke(this, e);
		}

        //ltang: https://developer.android.com/reference/android/media/MediaPlayer.OnBufferingUpdateListener.html
        //According the doc above, this seems wrong
        public TimeSpan Buffered => IsReadyRendering == false ? TimeSpan.Zero : TimeSpan.FromMilliseconds(VideoViewCanvas.BufferPercentage);

        public TimeSpan Duration => IsReadyRendering == false ? TimeSpan.Zero : TimeSpan.FromMilliseconds(VideoViewCanvas.Duration);
        
        public TimeSpan Position => IsReadyRendering == false ? TimeSpan.Zero : TimeSpan.FromMilliseconds(VideoViewCanvas.CurrentPosition);

        private int lastPosition = 0;

		private MediaPlayerStatus _status = MediaPlayerStatus.Stopped;
        public MediaPlayerStatus Status
        {
            get { return _status; }
            private set
            {
                _status = value;
				OnStatusChanged(new StatusChangedEventArgs(_status));
            }
        }

        public async Task Pause()
        {			
            VideoViewCanvas.Pause();
            Status = MediaPlayerStatus.Paused;
            lastPosition = VideoViewCanvas.CurrentPosition;
            await Task.CompletedTask;            
        }

        public void Init()
        {
            _mediaPlayer = null;
            _lastSelectedTrackIndex = null;

            Status = MediaPlayerStatus.Loading;

            if (UseNativeControls)
            {
                var mediaController = new MediaController(((VideoView)RenderSurface).Context);
                mediaController.SetAnchorView(VideoViewCanvas);
                VideoViewCanvas.SetMediaController(mediaController);
            }
            
            VideoViewCanvas.SetOnCompletionListener(this);
            VideoViewCanvas.SetOnErrorListener(this);
            VideoViewCanvas.SetOnPreparedListener(this);
            VideoViewCanvas.SetOnInfoListener(this);            
        }
        
        public async Task Play(IMediaFile mediaFile = null)
        {            
            if (!IsReadyRendering)
                //Android ViewRenderer might not initialize Control yet
                return;

			if (isPlayerReady == false)
            {
                //await Task.Delay(100);
                Init();
				isPlayerReady = true;
            }

			if (mediaFile == null || (mediaFile != null && string.IsNullOrEmpty(mediaFile.Url)))
			{
				return;
			}

            if (mediaFile != null && CurrentFile != mediaFile)
            {
                CurrentFile = mediaFile;
                
                currentUri = mediaFile.CreateUri();

                VideoViewCanvas.StopPlayback();
				//VideoViewCanvas.Suspend();
				Status = MediaPlayerStatus.Stopped;
            }

            if (Status == MediaPlayerStatus.Paused)
            {
                //We are simply paused so just continue                
                Console.WriteLine($"Seekto: {lastPosition}");
                VideoViewCanvas.SeekTo(lastPosition);
                VideoViewCanvas.Start();

                Status = MediaPlayerStatus.Playing;
                return;
            }

            try
            {
                Status = MediaPlayerStatus.Buffering;
				VideoViewCanvas.SetVideoURI(currentUri, RequestHeaders);
            }
            catch(System.Exception ex)
            {
				OnMediaFailed(new MediaFailedEventArgs(ex.Message, ex));
                Status = MediaPlayerStatus.Stopped;
            }
        }

        public async Task Seek(TimeSpan position)
        {
            int msec = Convert.ToInt32(position.TotalMilliseconds);            
            VideoViewCanvas.SeekTo(msec);

            lastPosition = VideoViewCanvas.CurrentPosition;
            Console.WriteLine($"Seekto: {lastPosition}");

            await Task.CompletedTask;            
        }

        public async Task Stop()
        {            
            lastPosition = 0;
            VideoViewCanvas.StopPlayback();
            Status = MediaPlayerStatus.Stopped;
            await Task.CompletedTask;
        }

        public void OnCompletion(MediaPlayer mp)
        {
            Console.WriteLine($"OnCompletion");

            OnMediaFinished(new MediaFinishedEventArgs(CurrentFile));
        }

        public bool OnError(MediaPlayer mp, MediaError what, int extra)
        {
            Console.WriteLine($"OnError: {what}");

            Stop().Wait();
            Status = MediaPlayerStatus.Failed;
			OnMediaFailed(new MediaFailedEventArgs(what.ToString(), new System.Exception()));
            return true;
        }

        public void OnPrepared(MediaPlayer mp)
        {
            Console.WriteLine($"OnPrepared: {Status}");

            if (_mediaPlayer != mp)
            {
                _IsMuted = false;

                //ltang: Store _mediaPlayer and extract track info
                _mediaPlayer = mp;                
                List<IMediaTrackInfo> temp = ExtractTrackInfo(_mediaPlayer);
                _TrackInfoList = temp == null ? null : new ReadOnlyCollection<IMediaTrackInfo>(temp);

                //ltang: Try to re-select the last track selection
                if (_lastSelectedTrackIndex != null)
                {
                    int previousSelection = _lastSelectedTrackIndex.Value;
                    _lastSelectedTrackIndex = null;

                    SetTrack(previousSelection);
                }
            }

            if (Status == MediaPlayerStatus.Buffering)
            {
                if (lastPosition > 0)
                {                    
                    Console.WriteLine($"Seekto: {lastPosition}");
                    VideoViewCanvas.SeekTo(lastPosition);
                }

                VideoViewCanvas.Start();
            }

            Status = MediaPlayerStatus.Playing;
        }        

        public bool OnInfo(MediaPlayer mp, [GeneratedEnum] MediaInfo what, int extra)
        {
            Console.WriteLine($"OnInfo: {what}");            

            switch (what)
			{
				case MediaInfo.BadInterleaving:
					break;
				case MediaInfo.BufferingEnd:
					break;
				case MediaInfo.BufferingStart:
					break;
				case MediaInfo.MetadataUpdate:
					break;
				case MediaInfo.NotSeekable:
					break;
				case MediaInfo.SubtitleTimedOut:
					break;
				case MediaInfo.Unknown:
					break;
				case MediaInfo.UnsupportedSubtitle:
					break;
				case MediaInfo.VideoRenderingStart:				    
                    break;
				case MediaInfo.VideoTrackLagging:
					break;
			}

			return true;
        }

        #region Helpers        
        private static List<IMediaTrackInfo> ExtractTrackInfo(MediaPlayer mp)
        {            
            if (mp == null)
                return null;

            List<IMediaTrackInfo> result = null;

            //https://stackoverflow.com/questions/8789529/android-multiple-audio-tracks-in-a-videoview
            MediaPlayer.TrackInfo[] tracks = mp.GetTrackInfo();            
            if (tracks != null && tracks.Any())
            {
                result = new List<IMediaTrackInfo>();

                for (int i = 0; i < tracks.Length; i++)
                {
                    MediaPlayer.TrackInfo track = tracks[i];
                    
                    var audioTrack = new MediaTrackInfo()
                    {
                        Tag = track,
                        DisplayName = track.Language,
                        TrackIndex = i,
                        TrackId = track.ToString(),
                        LanguageCode = track.Language,
                        TrackType = ToMediaTrackTypeAsbtract(track.TrackType)
                    };
                    result.Add(audioTrack);
                }
            }
            return result;
        }

        private static Abstractions.Enums.MediaTrackType ToMediaTrackTypeAsbtract(Android.Media.MediaTrackType typeInt)
        {
            Abstractions.Enums.MediaTrackType typeOut = Abstractions.Enums.MediaTrackType.Unknown;
            switch (typeInt)
            {
                case (Android.Media.MediaTrackType.Unknown):
                    typeOut = Abstractions.Enums.MediaTrackType.Unknown;
                    break;
                case (Android.Media.MediaTrackType.Video):
                    typeOut = Abstractions.Enums.MediaTrackType.Video;
                    break;
                case (Android.Media.MediaTrackType.Audio):
                    typeOut = Abstractions.Enums.MediaTrackType.Audio;
                    break;
                case (Android.Media.MediaTrackType.Timedtext):
                    typeOut = Abstractions.Enums.MediaTrackType.Timedtext;
                    break;
                case (Android.Media.MediaTrackType.Subtitle):
                    typeOut = Abstractions.Enums.MediaTrackType.Subtitle;
                    break;
                case (Android.Media.MediaTrackType.Metadata):
                    typeOut = Abstractions.Enums.MediaTrackType.Metadata;
                    break;
                default:
                    throw new NotImplementedException($"ToMediaTrackTypeAsbtract conversion not found for {typeInt}");
            }
            
            return typeOut;
        }

        private static Android.Media.MediaTrackType ToMediaTrackTypeAndroid(Abstractions.Enums.MediaTrackType typeInt)
        {
            Android.Media.MediaTrackType typeOut = Android.Media.MediaTrackType.Unknown;
            switch (typeInt)
            {
                case (Abstractions.Enums.MediaTrackType.Unknown):
                    typeOut = Android.Media.MediaTrackType.Unknown;
                    break;
                case (Abstractions.Enums.MediaTrackType.Video):
                    typeOut = Android.Media.MediaTrackType.Video;
                    break;
                case (Abstractions.Enums.MediaTrackType.Audio):
                    typeOut = Android.Media.MediaTrackType.Audio;
                    break;
                case (Abstractions.Enums.MediaTrackType.Timedtext):
                    typeOut = Android.Media.MediaTrackType.Timedtext;
                    break;
                case (Abstractions.Enums.MediaTrackType.Subtitle):
                    typeOut = Android.Media.MediaTrackType.Subtitle;
                    break;
                case (Abstractions.Enums.MediaTrackType.Metadata):
                    typeOut = Android.Media.MediaTrackType.Metadata;
                    break;
                default:
                    throw new NotImplementedException($"ToMediaTrackTypeAndroid conversion not found for {typeInt}");
            }

            return typeOut;
        }

        
        #endregion
    }
}
