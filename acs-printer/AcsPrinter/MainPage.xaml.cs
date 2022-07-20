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
using Windows.UI.Xaml.Navigation;

namespace AcsPrinter
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private CallClient callClient;
        private Call _call;
        private Dictionary<MediaPlayerElement, SoftwareBitmap> _snapshots = new();
        private Task<CallAgent> _callAgent;

        private (MediaPlayerElement media, ImageBrush image)[] _elements;
        private int _cyclingMediaPlayerIndex = 0;

        private Dictionary<int, MediaPlayerElement> _streamAssignments = new();
        private Dictionary<ICommunicationIdentifier, RemoteParticipant> _participants = new();
        private HashSet<RemoteVideoStream> _streams = new();

        private readonly Printer _printer;

        public MainPage()
        {
            InitializeComponent();
            InitCallAgentAndDeviceManager();
            _elements = new[] { (Video1, Image1), (Video2, Image2), (Video3, Image3), (Video4, Image4) };
            _printer = new(Dispatcher);

            //TestStaticVideo();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e) => _printer.RegisterForPrinting();

        protected override void OnNavigatedFrom(NavigationEventArgs e) => _printer.UnregisterForPrinting();

        private void TestStaticVideo()
        {
            var source = new Uri("ms-appx:///Assets/video.mp4");
            _ = PlayVideo(Video1, source);
            _ = PlayVideo(Video2, source);
            _ = PlayVideo(Video4, source);
            Video1.MediaPlayer.IsLoopingEnabled = true;
        }

        private async void InitCallAgentAndDeviceManager()
        {
            callClient = new CallClient();

            var credential = new CommunicationTokenCredential("eyJhbGciOiJSUzI1NiIsImtpZCI6IjEwNCIsIng1dCI6IlJDM0NPdTV6UENIWlVKaVBlclM0SUl4Szh3ZyIsInR5cCI6IkpXVCJ9.eyJza3lwZWlkIjoiYWNzOjQ5MzVlODBlLTM5MjgtNDZkMS05ODY4LTNhZDA5NzkwZWVhNl8wMDAwMDAxMC03MzI4LTExNWYtOWZmYi05YzNhMGQwMDAzNTUiLCJzY3AiOjE3OTIsImNzaSI6IjE2NDg1MTcwODIiLCJleHAiOjE2NDg2MDM0ODIsImFjc1Njb3BlIjoiY2hhdCx2b2lwIiwicmVzb3VyY2VJZCI6IjQ5MzVlODBlLTM5MjgtNDZkMS05ODY4LTNhZDA5NzkwZWVhNiIsImlhdCI6MTY0ODUxNzA4Mn0.YvwKlwach6smOV4Z9SbjSoSRsmF-lSx6PE_Of3G_xKeHZmI4syceB6_OIdEUYd1tjiQlUyEAVXTadWNPaLV9n5ZYNhpgttc2nFxVRAeLdrUufZpQmiqPupzXpjTNohPGtUD5EgjSSq4cfqt8gLFYk1ThpmJh9qSJf-8zYAqA5XKHJUGe9fimxdccuO55W-3GYzBYJ-WRUHTAs6ZlWKSPqDTev57cOLYPDUybjDcwvIm3lDY_9qu8Hzq2qAXSCB6NtpB1It04fNKngsqg62uVLvYPi-WaHVJ5_Bp37nXfBmTNFipAsuG5aAt5HX622NNNdvWIH0LsPG7q0BQhCEJ94Q");

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
            _call.OnRemoteParticipantsUpdated += Call_OnRemoteParticipantsUpdated;
            _call.OnStateChanged += Call_OnStateChanged;
        }

        private async void Agent_OnCallsUpdated(object sender, CallsUpdatedEventArgs args)
        {
            foreach (var call in args.AddedCalls)
            {
                await HandleAddedParticipants(call.RemoteParticipants);
                call.OnRemoteParticipantsUpdated += Call_OnRemoteParticipantsUpdated;
                call.OnStateChanged += Call_OnStateChanged;
            }
        }

        private async Task OnVideoStreamsUpdated(RemoteVideoStreamsEventArgs args)
        {
            foreach (var removed in args.RemovedRemoteVideoStreams)
                Debug.WriteLine("Lost stream " + removed.Id);

            foreach (var added in args.AddedRemoteVideoStreams)
                Debug.WriteLine("New stream " + added.Id);

            await RenderVideos(args.AddedRemoteVideoStreams);
        }

        private async Task RenderVideos(IReadOnlyList<RemoteVideoStream> remoteVideos)
        {
            foreach (var remoteVideoStream in remoteVideos)
            {
                var mediaElement = GetNextAvailableMediaPlayerElement(remoteVideoStream.Id);
                await RenderVideo(mediaElement, remoteVideoStream);
            }
        }

        private MediaPlayerElement GetNextAvailableMediaPlayerElement(int streamId)
        {
            if (_streamAssignments.TryGetValue(streamId, out var mediaPlayerElement))
                return mediaPlayerElement;

            // we're cycling through
            var media = _elements[_cyclingMediaPlayerIndex].media;
            _cyclingMediaPlayerIndex = (_cyclingMediaPlayerIndex + 1) % _elements.Length;
            _streamAssignments[streamId] = media;
            return media;
        }

        private async Task RenderVideo(MediaPlayerElement mediaPlayerElement, RemoteVideoStream stream)
        {
            // store in the hope this fixes it
            _streams.Add(stream);
            var remoteUri = await stream.Start();
            Debug.WriteLine("Render " + remoteUri);
            await PlayVideo(mediaPlayerElement, remoteUri);
        }

        private async Task PlayVideo(MediaPlayerElement mediaPlayerElement, Uri source)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                mediaPlayerElement.MediaPlayer.IsMuted = true;
                mediaPlayerElement.Source = MediaSource.CreateFromUri(source);
                //mediaPlayerElement.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/video.mp4"));
                //mediaPlayerElement.Source = MediaSource.CreateFromUri(new Uri("skype://urlbinding_sink_1/"));

                mediaPlayerElement.MediaPlayer.Play();
                
                // bad place
                CallButton.Content = "✅ Joined";
            });
        }

        private async void Call_OnRemoteParticipantsUpdated(object sender, ParticipantsUpdatedEventArgs args)
        {
            await HandleAddedParticipants(args.AddedParticipants);
        }

        private async Task HandleAddedParticipants(IReadOnlyList<RemoteParticipant> participants)
        {
            foreach (var remoteParticipant in participants)
            {
                // store so that event handlers will fire
                _participants[remoteParticipant.Identifier] = remoteParticipant;
                await RenderVideos(remoteParticipant.VideoStreams);
                remoteParticipant.OnVideoStreamsUpdated += async (s, a) => await OnVideoStreamsUpdated(a);
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

            //foreach (var kv in _snapshots)
            //{
            //    if (kv.Value is null) continue;
            //    var image = _elements.First(x => x.media == kv.Key).image;
            //    image.ImageSource = kv.Value;
            //}
        }

        private async Task<IReadOnlyList<SoftwareBitmap>> TakeSnapshots()
        {
            var snapshots = new List<SoftwareBitmap>();
            var tasks = new List<Task>();
            foreach (var (media, _) in _elements)
            {
                tasks.Add(StoreSnapshot(media));
            }
            await Task.WhenAll(tasks);
            return snapshots;

            async Task StoreSnapshot(MediaPlayerElement element)
            {
                var bitmap = await TakeSnapshot(element);
                if (bitmap is null) return;

                snapshots.Add(bitmap);
                _snapshots[element] = bitmap;
            }
        }

        private async Task<SoftwareBitmap> TakeSnapshot(MediaPlayerElement mediaPlayerElement)
        {
            if (mediaPlayerElement.Source is null) return null;

            SoftwareBitmap result = null;

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
            return result;
        }

        private async void InvokePrintingButton_Click(object sender, RoutedEventArgs e)
        {
            var snapshots = await TakeSnapshots();

            var panel = new StackPanel();
            var images = snapshots.Select(x =>
            {
                var image = new Image
                {
                    Width = x.PixelWidth,
                    Height = x.PixelHeight
                };
                var bitmap = new WriteableBitmap(x.PixelWidth, x.PixelHeight);
                x.CopyToBuffer(bitmap.PixelBuffer);
                image.Source = bitmap;
                panel.Children.Add(image);
                return image;
            }).ToList();

            await _printer.Print(panel);
        }
    }
}
