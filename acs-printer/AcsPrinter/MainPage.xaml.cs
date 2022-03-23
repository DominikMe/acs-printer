using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using Azure.WinRT.Communication;
using Azure.Communication.Calling;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Graphics.Imaging;
using System.Linq;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media;
using Microsoft.Graphics.Canvas;
using Windows.Media.Core;
using System.Threading;

namespace AcsPrinter
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private CallClient callClient;
        private Call _call;
        private Dictionary<MediaPlayerElement, ImageSource> _snapshots = new();
        private Task<CallAgent> _callAgent;

        private (MediaPlayerElement media, Image image)[] _elements;
        private int _cyclingMediaPlayerIndex = 0;

        private Dictionary<int, MediaPlayerElement> _streamAssignments = new();

        public MainPage()
        {
            InitializeComponent();
            InitCallAgentAndDeviceManager();
            _elements = new[] { (Video1, Image1), (Video2, Image2), (Video3, Image3), (Video4, Image4) };
        }

        private async void InitCallAgentAndDeviceManager()
        {
            callClient = new CallClient();

            var credential = new CommunicationTokenCredential("eyJhbGciOiJSUzI1NiIsImtpZCI6IjEwNCIsIng1dCI6IlJDM0NPdTV6UENIWlVKaVBlclM0SUl4Szh3ZyIsInR5cCI6IkpXVCJ9.eyJza3lwZWlkIjoiYWNzOjQ5MzVlODBlLTM5MjgtNDZkMS05ODY4LTNhZDA5NzkwZWVhNl8wMDAwMDAxMC00ZTI3LTZjYWMtMGNmOS05YzNhMGQwMDZmOTEiLCJzY3AiOjE3OTIsImNzaSI6IjE2NDc4OTYyODMiLCJleHAiOjE2NDc5ODI2ODMsImFjc1Njb3BlIjoidm9pcCIsInJlc291cmNlSWQiOiI0OTM1ZTgwZS0zOTI4LTQ2ZDEtOTg2OC0zYWQwOTc5MGVlYTYiLCJpYXQiOjE2NDc4OTYyODN9.wHcQBxkgNtuCH2CddBzCQHB1m0oUadGczb2h2WpGM2ZiUToQdMU8sMEmEeDAnWpOI-mqbOFC4LaSYb-eLLyOGyB8CfYKzY5yZrNfbrv5v5n0rK5njG3-0beEtx3jfCcw9TMeeCfgFy7IkbIw7PpPZNCNmWRt0D95-6c5Qibe4JzWHu-UST2hgRnTf5hg6NJbHdWt5P_IZZSCSr5vmvjNh6gQypVrY3ZXSCmITF37DW6Fd0HFxTHPxkOZd6fm_l2jL8xWvkXAwD_JG-dKYr6Jym5ig6BHBTX_L_7kB67sbAWDfHULYhYbvZw-8Az7yY8zgu4tNmhypbCqy4uO5I7RMQ");

            _callAgent = callClient.CreateCallAgent(credential, new CallAgentOptions()
            {
                DisplayName = "Printer"
            }).AsTask();
            var callAgent = await _callAgent;
            callAgent.OnCallsUpdated += Agent_OnCallsUpdated;
        }

        private async void CallButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            var callAgent = await _callAgent;
            _call = await callAgent.JoinAsync(
                new TeamsMeetingLinkLocator(JoinUrlTextBox.Text),
                new JoinCallOptions { });
        }

        private async void Agent_OnCallsUpdated(object sender, CallsUpdatedEventArgs args)
        {
            foreach (var call in args.AddedCalls)
            {
                foreach (var remoteParticipant in call.RemoteParticipants)
                {
                    await RenderVideos(remoteParticipant.VideoStreams);
                    remoteParticipant.OnVideoStreamsUpdated += async (s, a) => await RenderVideos(a.AddedRemoteVideoStreams);
                }
                call.OnRemoteParticipantsUpdated += Call_OnRemoteParticipantsUpdated;
                call.OnStateChanged += Call_OnStateChanged;
            }
        }

        private async Task RenderVideos(IReadOnlyList<RemoteVideoStream> streams)
        {
            foreach (var remoteVideoStream in streams)
            {
                var mediaElement = GetNextAvailableMediaPlayerElement(remoteVideoStream.Id);
                await RenderVideo(remoteVideoStream, mediaElement);
            }
        }

        private MediaPlayerElement GetNextAvailableMediaPlayerElement(int streamId)
        {
            if (_streamAssignments.TryGetValue(streamId, out var mediaPlayerElement))
                return mediaPlayerElement;

            // we're cycling through
            var media = _elements[_cyclingMediaPlayerIndex].media;
            _cyclingMediaPlayerIndex = _cyclingMediaPlayerIndex < _elements.Length - 1 ? _cyclingMediaPlayerIndex + 1 : 0;
            _streamAssignments[streamId] = media;
            return media;
        }

        private async Task RenderVideo(RemoteVideoStream stream, MediaPlayerElement mediaPlayerElement)
        {
            var remoteUri = await stream.Start();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                mediaPlayerElement.MediaPlayer.IsMuted = true;
                mediaPlayerElement.Source = MediaSource.CreateFromUri(remoteUri);
                //mediaPlayerElement.MediaPlayer.IsVideoFrameServerEnabled = true;
                //mediaPlayerElement.MediaPlayer.VideoFrameAvailable += MediaPlayer_VideoFrameAvailable;
                mediaPlayerElement.MediaPlayer.Play();
            });
        }

        private void MediaPlayer_VideoFrameAvailable(Windows.Media.Playback.MediaPlayer sender, object args)
        {

        }

        private async void Call_OnRemoteParticipantsUpdated(object sender, ParticipantsUpdatedEventArgs args)
        {
            foreach (var remoteParticipant in args.AddedParticipants)
            {
                await RenderVideos(remoteParticipant.VideoStreams);
                remoteParticipant.OnVideoStreamsUpdated += async (s, a) => await RenderVideos(a.AddedRemoteVideoStreams);
            }
        }

        private async void Call_OnStateChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (((Call)sender).State)
            {
                case CallState.Disconnected:
                    //await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => RemoteVideo.Source = null);
                    break;
                default:
                    Debug.WriteLine(((Call)sender).State);
                    break;
            }
        }

        private async void HangupButton_Click(object sender, RoutedEventArgs e) => await _call.HangUpAsync(new HangUpOptions());

        private async void SnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            await TakeSnapshots();

            foreach (var kv in _snapshots)
            {
                if (kv.Value is null) continue;
                var image = _elements.First(x => x.media == kv.Key).image;
                image.Source = kv.Value;
            }
        }

        private async Task TakeSnapshots()
        {
            foreach (var (media, _) in _elements)
            {
                // we can't parallelize this because it uses a shared CanvasDevice
                await StoreSnapshot(media);
            }

            async Task StoreSnapshot(MediaPlayerElement element)
            {
                var bitmap = await TakeSnapshot(element);
                if (bitmap is null) return;

                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(bitmap);
                _snapshots[element] = source;
            }
        }

        private readonly SemaphoreSlim _canvasDeviceSemaphore = new(1, 1);

        private async Task<SoftwareBitmap> TakeSnapshot(MediaPlayerElement mediaPlayerElement)
        {
            if (mediaPlayerElement.Source is null) return null;

            SoftwareBitmap result = null;

            await _canvasDeviceSemaphore.WaitAsync();
            var canvasDevice = CanvasDevice.GetSharedDevice();

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var frameServerDest = new SoftwareBitmap(BitmapPixelFormat.Bgra8, (int)mediaPlayerElement.Width, (int)mediaPlayerElement.Height, BitmapAlphaMode.Premultiplied);

                using (CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(canvasDevice, frameServerDest))
                {
                    try
                    {
                        mediaPlayerElement.MediaPlayer.CopyFrameToVideoSurface(canvasBitmap);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Failed to take snapshot from video: {e.Message}");
                        return;
                    }

                    var softwareBitmapImg = await SoftwareBitmap.CreateCopyFromSurfaceAsync(canvasBitmap);
                    result = SoftwareBitmap.Convert(softwareBitmapImg, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }
            });
            _canvasDeviceSemaphore.Release();
            return result;
        }
    }
}
