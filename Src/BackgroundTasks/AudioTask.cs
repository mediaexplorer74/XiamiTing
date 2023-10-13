using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using JacobC.Xiami.Models;
using static JacobC.Xiami.Services.LogService;

namespace JacobC.Xiami.Services
{
    /// <summary>
    /// Provide background tasks for audio playback services
    /// </summary>
    public sealed class AudioTask : IBackgroundTask
    {
 
        #region Private fields, properties
        private const string TrackIdKey = "trackid";
        private const string TitleKey = "title";
        private const string AlbumArtKey = "albumart";
        private SystemMediaTransportControls smtc;
        private BackgroundTaskDeferral deferral; // Keep the task active
        private AppState foregroundAppState = AppState.Unknown;
        private ManualResetEvent backgroundTaskStarted = new ManualResetEvent(false);
        private bool playbackStartedPreviously = false;
        Template10.Services.SettingsService.ISettingsService settinghelper 
            = SettingsService.Playback;
        #endregion

        #region Helper methods

        Uri GetTrackId(MediaPlaybackItem item)
        {
            if (item == null)
                return null; //No audio track in play

            return item.Source.CustomProperties[TrackIdKey] as Uri;
        }
        #endregion

        #region IBackgroundTask and IBackgroundTaskInstance Interface Members and handlers

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            DebugWrite("Background Audio Task " + taskInstance.Task.Name 
                + " starting...", "BackgroundPlayer");

            // Initialize embedding SystemMediaTrasportControls(SMTC)，UVC
            // The UI and UVC need to be updated when the program is terminated,
            // so SMTC needs to be configured and get updates from background tasks
            smtc = BackgroundMediaPlayer.Current.SystemMediaTransportControls;
            smtc.ButtonPressed += smtc_ButtonPressed;
            smtc.PropertyChanged += smtc_PropertyChanged;
            smtc.IsEnabled = true;
            smtc.IsPauseEnabled = true;
            smtc.IsPlayEnabled = true;
            smtc.IsNextEnabled = true;
            smtc.IsPreviousEnabled = true;

            BackgroundMediaPlayer.Current.CurrentStateChanged 
                += Current_CurrentStateChanged; 
            //Add a background control handle to the player

            BackgroundMediaPlayer.MessageReceivedFromForeground 
                += BackgroundMediaPlayer_MessageReceivedFromForeground; 
            //Initialize the message channel

            foregroundAppState = settinghelper.ReadAndReset(
                nameof(AppState), AppState.Unknown); //Read APP status

            //if (foregroundAppState != AppState.Suspended)
            //    settinghelper.Write("BackgroundAudioStarted", true);
            settinghelper.Write(nameof(BackgroundTaskState), BackgroundTaskState.Running.ToString());

            deferral = taskInstance.GetDeferral(); // This must be done
                                                   // before registering the event that uses it

            // Mark the background task as started and release the SMTC
            // playback operation (see WaitOne related to this signal)
            backgroundTaskStarted.Set();

            // Handler for associated task cancellation and completion
            taskInstance.Task.Completed += TaskCompleted;
            taskInstance.Canceled += OnCanceled;
        }
        /// <summary>
        /// Indicates the end of the background task
        /// </summary>       
        private void TaskCompleted(BackgroundTaskRegistration sender, 
            BackgroundTaskCompletedEventArgs args)
        {
            DebugWrite("BackgroundAudioTask " + sender.TaskId + 
                " Completed...", "BackgroundPlayer");
            deferral.Complete();
        }
        /// <summary>
        /// Handle the cancellation of background tasks
        /// </summary>
        /// <remarks>
        /// The reasons for cancellation are:
        /// 1.Other exclusive Media apps run to the foreground and start playing sound
        /// 2.Insufficient system resources
        /// </remarks>
        private void OnCanceled(IBackgroundTaskInstance sender, 
            BackgroundTaskCancellationReason reason)
        {
            // Here you can save the state when the process and resources are recovered
            DebugWrite("MyBackgroundAudioTask " + sender.Task.TaskId + " Cancel Requested...", 
                "BackgroundPlayer");
            try
            {
                // Stop running immediately
                backgroundTaskStarted.Reset();

                // Save the existing state
                //settinghelper.Write(TrackIdKey, GetCurrentTrackId()?.ToString());
                settinghelper.Write(nameof(BackgroundMediaPlayer.Current.Position), 
                    BackgroundMediaPlayer.Current.Position.ToString());

                settinghelper.Write(nameof(BackgroundTaskState), 
                    BackgroundTaskState.Canceled.ToString());

                settinghelper.Write(nameof(AppState), Enum.GetName(typeof(AppState), 
                    foregroundAppState));

                // Cancel the handler of other events
                BackgroundMediaPlayer.MessageReceivedFromForeground 
                    -= BackgroundMediaPlayer_MessageReceivedFromForeground;
                smtc.ButtonPressed -= smtc_ButtonPressed;
                smtc.PropertyChanged -= smtc_PropertyChanged;
            }
            catch (Exception ex)
            {
                ErrorWrite(ex, "BackgroundPlayer");
            }
            //settinghelper.Remove("BackgroundAudioStarted");
            deferral.Complete(); //Give a task completion signal
            DebugWrite("BackgroundAudioTask Cancel complete...", "BackgroundPlayer");
        }
        #endregion

        #region SysteMediaTransportControls related functions and handlers
        /// <summary>
        /// pass <see cref="SystemMediaTransportControls"/> API Universal Volume Control
        /// (UVC) pointer
        /// </summary>
        private void UpdateUVCOnNewTrack(SongModel item)
        {
            if (item == null)
            {
                smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
                smtc.DisplayUpdater.MusicProperties.Title = string.Empty;
                smtc.DisplayUpdater.Update();
                return;
            }
            smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
            smtc.DisplayUpdater.Type = MediaPlaybackType.Music;
            smtc.DisplayUpdater.MusicProperties.Title = item.Name;
            smtc.DisplayUpdater.Thumbnail = 
                RandomAccessStreamReference.CreateFromUri(item.Album.ArtLarge);
            DebugWrite(item.Album.ArtLarge.ToString());
            smtc.DisplayUpdater.Update();
        }

        private void smtc_PropertyChanged(SystemMediaTransportControls sender, 
            SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            // TODO: If the volume is turned to mute, the app can choose to pause the music
        }

        /// <summary>
        /// Handle key events generated by UVC
        /// </summary>
        /// <remarks>If this code is not running in a background process, 
        /// it will not respond to UVC events when it hangs</remarks>
        private void smtc_ButtonPressed(SystemMediaTransportControls sender, 
            SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    DebugWrite("UVC play button pressed", "BackgroundPlayer");

                    // SMTC will start asynchronously after the background task is suspended,
                    // and sometimes the startup process in Run() needs to be completed.

                    // Wait for the background task to start.Once started, keep the signal
                    // until it is turned off, so that there is no need to wait again unless otherwise needed
                    bool result = backgroundTaskStarted.WaitOne(5000);
                    if (!result)
                        throw new Exception("Background Task didn't initialize in time");
                    StartPlayback();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    DebugWrite("UVC pause button pressed", "BackgroundPlayer");
                    try
                    {
                        BackgroundMediaPlayer.Current.Pause();
                    }
                    catch (Exception ex)
                    {
                        ErrorWrite(ex, "BackgroundPlayer");
                    }
                    break;
                case SystemMediaTransportControlsButton.Next:
                    DebugWrite("UVC next button pressed", "BackgroundPlayer");
                    MessageService.SendMediaMessageToForeground(MediaMessageTypes.SkipNext);
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    DebugWrite("UVC previous button pressed", "BackgroundPlayer");
                    MessageService.SendMediaMessageToForeground(MediaMessageTypes.SkipPrevious);
                    break;
            }
        }

        #endregion

        #region Playlist management functions and handlers
        /// <summary>
        /// Start the playlist and change the UVC status
        /// </summary>
        private void StartPlayback()
        {
            try
            {
                DebugWrite("into Start method", "BackgroundPlayer");
                // If the playback has already started once, you only need to continue the playback
                if (!playbackStartedPreviously)
                {
                    playbackStartedPreviously = true;

                    string currentTrackId 
                        = settinghelper.ReadAndReset<string>(TrackIdKey);
                    
                    string currentTrackPosition 
                        = settinghelper.ReadAndReset<string>(
                            nameof(BackgroundMediaPlayer.Current.Position));

                    if (currentTrackPosition != null)
                    {
                        // If the task is cancelled, playback starts from
                        // the saved track and location
                        BackgroundMediaPlayer.Current.Play();
                    }
                    else
                    {
                        DebugWrite("No current track", "BackgroundPlayer");
                        BackgroundMediaPlayer.Current.Play();
                    }
                }
                else
                {
                    DebugWrite("started previously", "BackgroundPlayer");
                    BackgroundMediaPlayer.Current.Play();
                }
            }
            catch (Exception ex)
            {
                ErrorWrite(ex, "BackgroundPlayer");
            }
        }

        //When the currently playing track changes, it occurs when the track is switched
        private void PlaybackList_CurrentItemChanged(MediaPlaybackList sender, 
            CurrentMediaPlaybackItemChangedEventArgs args)
        {
            // Get updated items
            var item = args.NewItem;
            DebugWrite("PlaybackList_CurrentItemChanged: " 
                + (item == null ? "null" : GetTrackId(item).ToString()), "BackgroundPlayer");

            //System.Diagnostics.Debugger.Break();
            //if (item == null)
            //    UpdateUVCOnNewTrack(null);

            //// Get the current playback track
            //Uri currentTrackId = null;
            //if (item != null)
            //    currentTrackId = item.Source.CustomProperties[TrackIdKey] as Uri;

            //// Notify the foreground to switch or keep
            //if (foregroundAppState == AppState.Active)
            //    MessageService.SendMediaMessageToForeground<Uri>
            //    (MediaMessageTypes.TrackChanged, currentTrackId);
            //else
            //    settinghelper.Write(TrackIdKey, currentTrackId?.ToString());
        }

        #endregion

        #region Background Media Player Handlers
        void Current_CurrentStateChanged(MediaPlayer sender, object args)
        {
            DebugWrite($"PlayerStateChanged to {sender.CurrentState.ToString()}",
                "BackgroundPlayer");
            switch (sender.CurrentState)
            {
                case MediaPlayerState.Playing:
                    smtc.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case MediaPlayerState.Paused://Pause in the middle or pause after playback
                    smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
                    var player = BackgroundMediaPlayer.Current;

                    //Play the next song, there is a fault tolerance rate of 0.1s
                    if (player.NaturalDuration.TotalSeconds
                        - player.Position.TotalSeconds < 0.1)
                    {
                        MessageService.SendMediaMessageToForeground(MediaMessageTypes.SkipNext);
                    }
                    break;
                case MediaPlayerState.Closed:
                    smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
                    break;
                case MediaPlayerState.Opening://Open file
                    break;
            }
        }

        private void BackgroundMediaPlayer_MessageReceivedFromForeground(object sender,
            MediaPlayerDataReceivedEventArgs e)
        {
            //System.Diagnostics.Debugger.Break();
            DebugWrite($"Message {e.Data["MessageType"]} get", "BackgroundPlayer");
            switch(MessageService.GetTypeOfMediaMessage(e.Data))
            {
                case MediaMessageTypes.AppSuspended:
                    DebugWrite("App suspending", "BackgroundPlayer"); 
                    // The application is suspended, save the application status here

                    foregroundAppState = AppState.Suspended;
                    //var currentTrackId = GetCurrentTrackId();
                    //settinghelper.Write(TrackIdKey, currentTrackId?.ToString());
                    return;
                case MediaMessageTypes.AppResumed:
                    DebugWrite("App resuming", "BackgroundPlayer"); // Application continues
                    foregroundAppState = AppState.Active;
                    return;
                case MediaMessageTypes.StartPlayback:
                    //The foreground of the application sends a playback signal
                    DebugWrite("Starting Playback", "BackgroundPlayer");
                    StartPlayback();
                    return;
                case MediaMessageTypes.SetSong:
                    var song = MessageService.GetMediaMessage<SongModel>(e.Data);
                    smtc.PlaybackStatus = MediaPlaybackStatus.Changing;
                    var current = BackgroundMediaPlayer.Current;
                    current.SetUriSource(song.MediaUri);

                    //if (current.CurrentState != MediaPlayerState.Playing
                    //  && current.CurrentState != MediaPlayerState.Closed)
                    //    StartPlayback();
                    UpdateUVCOnNewTrack(song);
                    DebugWrite($"PlaySong {song.XiamiID} Address:{song.MediaUri}",
                        "BackgroundPlayer");
                    return;
                case MediaMessageTypes.SourceTypeChanged:
                    smtc.IsPreviousEnabled = !MessageService.GetMediaMessage<bool>(e.Data);
                    return;
            }

        }

        private void PlaybackList_ItemFailed(MediaPlaybackList sender, 
            MediaPlaybackItemFailedEventArgs args)
        {
            DebugWrite(args.Error.ErrorCode.ToString(), "BackgroundPlayer ItemFailed");
        }
        #endregion
    }
}
