using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Acr.UserDialogs;
using BMM.Api.Framework;
using BMM.Api.Implementation.Models;
using BMM.Core.Constants;
using BMM.Core.Extensions;
using BMM.Core.GuardedActions.Base;
using BMM.Core.GuardedActions.TrackOptions.Interfaces;
using BMM.Core.GuardedActions.TrackOptions.Parameters;
using BMM.Core.GuardedActions.TrackOptions.Parameters.Interfaces;
using BMM.Core.Helpers;
using BMM.Core.Helpers.PresentationHints;
using BMM.Core.Implementations;
using BMM.Core.Implementations.Analytics;
using BMM.Core.Implementations.FirebaseRemoteConfig;
using BMM.Core.Implementations.Localization.Interfaces;
using BMM.Core.Implementations.Player.Interfaces;
using BMM.Core.Implementations.Tracks.Interfaces;
using BMM.Core.Implementations.UI;
using BMM.Core.Messages;
using BMM.Core.Models.POs;
using BMM.Core.NewMediaPlayer.Abstractions;
using BMM.Core.NewMediaPlayer.Constants;
using BMM.Core.Translation;
using BMM.Core.ViewModels;
using MvvmCross;
using MvvmCross.Commands;
using MvvmCross.Navigation;
using MvvmCross.Plugin.Messenger;
using Xamarin.Essentials;

namespace BMM.Core.GuardedActions.TrackOptions
{
    public class PrepareTrackOptionsAction :
        GuardedActionWithParameterAndResult<IPrepareTrackOptionsParameters, IList<StandardIconOptionPO>>,
        IPrepareTrackOptionsAction
    {
        private const string SleepTimerOptionKey = "Selected sleep timer option"; 
        private const string PlaybackSpeedOptionKey = "Selected playback speed option"; 
        private const string PlaybackSpeedStringFormat = "0.00";
        
        private readonly IConnection _connection;
        private readonly IBMMLanguageBinder _bmmLanguageBinder;
        private readonly IMvxMessenger _mvxMessenger;
        private readonly IMvxNavigationService _mvxNavigationService;
        private readonly IShareLink _shareLink;
        private readonly ITrackOptionsService _trackOptionsService;
        private readonly IFeaturePreviewPermission _featurePreviewPermission;
        private readonly ISleepTimerService _sleepTimerService;
        private readonly IFirebaseRemoteConfig _firebaseRemoteConfig;
        private readonly IAnalytics _analytics;
        private readonly IMediaPlayer _mediaPlayer;

        private readonly List<decimal> _availablePlaybackSpeed = new()
        {
            0.75m,
            1.25m,
            1.5m
        };

        public PrepareTrackOptionsAction(IConnection connection,
            IBMMLanguageBinder bmmLanguageBinder,
            IMvxMessenger mvxMessenger,
            IMvxNavigationService mvxNavigationService,
            IShareLink shareLink,
            ITrackOptionsService trackOptionsService,
            IFeaturePreviewPermission featurePreviewPermission,
            ISleepTimerService sleepTimerService,
            IFirebaseRemoteConfig firebaseRemoteConfig,
            IAnalytics analytics,
            IMediaPlayer mediaPlayer)
        {
            _connection = connection;
            _bmmLanguageBinder = bmmLanguageBinder;
            _mvxMessenger = mvxMessenger;
            _mvxNavigationService = mvxNavigationService;
            _shareLink = shareLink;
            _trackOptionsService = trackOptionsService;
            _featurePreviewPermission = featurePreviewPermission;
            _sleepTimerService = sleepTimerService;
            _firebaseRemoteConfig = firebaseRemoteConfig;
            _analytics = analytics;
            _mediaPlayer = mediaPlayer;
        }
        
        private bool IsSleepTimerOptionAvailable => _featurePreviewPermission.IsFeaturePreviewEnabled() || _firebaseRemoteConfig.IsSleepTimerEnabled;
        private bool IsPlaybackSpeedOptionAvailable => _featurePreviewPermission.IsFeaturePreviewEnabled() || _firebaseRemoteConfig.IsPlaybackSpeedEnabled;

        protected override async Task<IList<StandardIconOptionPO>> Execute(IPrepareTrackOptionsParameters parameter)
        {
            await Task.CompletedTask;
            var options = new List<StandardIconOptionPO>();

            var sourceVM = parameter.SourceVM;
            var track = (Track)parameter.Track;
            
            bool shouldShowSleepTimerOption = sourceVM is PlayerViewModel && IsSleepTimerOptionAvailable;
            bool shouldShowPlaybackSpeedOption = sourceVM is PlayerViewModel && IsPlaybackSpeedOptionAvailable;

            bool isInOnlineMode = _connection.GetStatus() == ConnectionStatus.Online;

            var actionSheet = new ActionSheetConfig();

            if (isInOnlineMode && !track.IsLivePlayback)
            {
                if (sourceVM is TrackCollectionViewModel trackCollectionVm)
                {
                    options.Add(
                        new StandardIconOptionPO(
                            _bmmLanguageBinder[Translations.UserDialogs_Track_DeleteFromPlaylist],
                            ImageResourceNames.IconRemove,
                            new MvxAsyncCommand(() => trackCollectionVm.DeleteTrackFromTrackCollection(track))));
                }

                options.Add(
                    new StandardIconOptionPO(
                        _bmmLanguageBinder[Translations.UserDialogs_Track_AddToPlaylist],
                        ImageResourceNames.IconPlaylist,
                        new MvxAsyncCommand(async () =>
                        {
                            await _mvxNavigationService.ChangePresentation(new CloseFragmentsOverPlayerHint());
                            _mvxMessenger.Publish(new TogglePlayerMessage(this, false));
                            await Task.Delay(ViewConstants.DefaultAnimationDurationInMilliseconds);

                            await _mvxNavigationService.Navigate<TrackCollectionsAddToViewModel, TrackCollectionsAddToViewModel.Parameter>(
                                new TrackCollectionsAddToViewModel.Parameter
                                {
                                    DocumentId = track.Id,
                                    DocumentType = track.DocumentType
                                });
                        })));
            }

            if (sourceVM is not QueueViewModel && sourceVM is not PlayerViewModel)
            {
                var mediaPlayer = Mvx.IoCProvider.Resolve<IMediaPlayer>();

                if (DeviceInfo.Platform == DevicePlatform.iOS)
                {
                    actionSheet.AddHandled(_bmmLanguageBinder[Translations.UserDialogs_Track_QueueToPlayNext],
                        async () =>
                        {
                            var success = await mediaPlayer.QueueToPlayNext(track, sourceVM.PlaybackOriginString);
                            if (success)
                            {
                                await Mvx.IoCProvider.Resolve<IToastDisplayer>()
                                    .Success(_bmmLanguageBinder.GetText(Translations.UserDialogs_Track_AddedToQueue, track.Title));

                                Mvx.IoCProvider.Resolve<IAnalytics>()
                                    .LogEvent(Event.TrackHasBeenAddedToBePlayedNext,
                                        new Dictionary<string, object>
                                        {
                                            { "track", track.Id }
                                        });
                            }
                        },
                        ImageResourceNames.IconPlay);
                }

                options.Add(
                    new StandardIconOptionPO(
                        _bmmLanguageBinder[Translations.UserDialogs_Track_AddToQueue],
                        ImageResourceNames.IconQueue,
                        new MvxAsyncCommand(async () =>
                        {
                            var success = await mediaPlayer.AddToEndOfQueue(track, sourceVM.PlaybackOriginString);
                            if (success)
                            {
                                await Mvx.IoCProvider.Resolve<IToastDisplayer>()
                                    .Success(_bmmLanguageBinder.GetText(Translations.UserDialogs_Track_AddedToQueue, track.Title));

                                Mvx.IoCProvider.Resolve<IAnalytics>()
                                    .LogEvent(Event.TrackHasBeenAddedToEndOfQueue,
                                        new Dictionary<string, object>
                                        {
                                            { "track", track.Id }
                                        });
                            }
                        })));
            }

            if (!track.IsLivePlayback)
            {
                options.Add(
                    new StandardIconOptionPO(
                        _bmmLanguageBinder[Translations.UserDialogs_Track_Share],
                        ImageResourceNames.IconShare,
                        new MvxAsyncCommand(async () => { await _shareLink.For(track); })));
            }

            // Only show this option if we are not inside an album (what apparently is the one you would be guided to here)
            if (sourceVM is not AlbumViewModel && isInOnlineMode && track.ParentId != 0)
            {
                options.Add(
                    new StandardIconOptionPO(
                        _bmmLanguageBinder[Translations.UserDialogs_Track_GoToAlbum],
                        ImageResourceNames.IconAlbum,
                        new MvxAsyncCommand(async () =>
                        {
                            await ClosePlayer();
                            await _mvxNavigationService.Navigate<AlbumViewModel, Album>(new Album
                                { Id = track.ParentId, Title = track.Album });
                        })));
            }

            if (isInOnlineMode)
            {
                IList<TrackRelation> contributors = track.Relations?.Where(r =>
                        r.Type == TrackRelationType.Composer ||
                        r.Type == TrackRelationType.Interpret ||
                        r.Type == TrackRelationType.Lyricist ||
                        r.Type == TrackRelationType.Arranger)
                    .ToList();

                if (contributors != null && contributors.Count > 0)
                {
                    var trackHasOnlyOneContributor = contributors.Count == 1;

                    options.Add(
                        new StandardIconOptionPO(_bmmLanguageBinder[Translations.UserDialogs_Track_GoToContributor],
                            ImageResourceNames.IconPerson,
                            new MvxAsyncCommand(async () =>
                            {
                                var goToContributorOptions = new List<StandardIconOptionPO>();

                                foreach (TrackRelation relation in track.Relations)
                                {
                                    if (relation.Type == TrackRelationType.Composer)
                                    {
                                        var rel = (TrackRelationComposer)relation;

                                        if (trackHasOnlyOneContributor)
                                            await GoToContributorVM(rel.Id);
                                        else
                                        {
                                            goToContributorOptions.Add(new StandardIconOptionPO(
                                                _bmmLanguageBinder.GetText(Translations.UserDialogs_Track_GoToContributor_Composer,
                                                    rel.Name),
                                                ImageResourceNames.IconPerson,
                                                new MvxAsyncCommand(() => GoToContributorVM(rel.Id))));
                                        }
                                    }
                                    else if (relation.Type == TrackRelationType.Interpret)
                                    {
                                        var rel = (TrackRelationInterpreter)relation;

                                        if (trackHasOnlyOneContributor)
                                            await GoToContributorVM(rel.Id);
                                        else
                                        {
                                            goToContributorOptions.Add(new StandardIconOptionPO(
                                                _bmmLanguageBinder.GetText(Translations.UserDialogs_Track_GoToContributor_Interpret,
                                                    rel.Name),
                                                ImageResourceNames.IconPerson,
                                                new MvxAsyncCommand(() => GoToContributorVM(rel.Id))));
                                        }
                                    }
                                    else if (relation.Type == TrackRelationType.Lyricist)
                                    {
                                        var rel = (TrackRelationLyricist)relation;

                                        if (trackHasOnlyOneContributor)
                                            await GoToContributorVM(rel.Id);
                                        else
                                        {
                                            goToContributorOptions.Add(new StandardIconOptionPO(
                                                _bmmLanguageBinder.GetText(Translations.UserDialogs_Track_GoToContributor_Lyricist,
                                                    rel.Name),
                                                ImageResourceNames.IconPerson,
                                                new MvxAsyncCommand(() => GoToContributorVM(rel.Id))));
                                        }
                                    }
                                    else if (relation.Type == TrackRelationType.Arranger)
                                    {
                                        var rel = (TrackRelationArranger)relation;

                                        if (trackHasOnlyOneContributor)
                                            await GoToContributorVM(rel.Id);
                                        else
                                        {
                                            goToContributorOptions.Add(new StandardIconOptionPO(
                                                _bmmLanguageBinder.GetText(Translations.UserDialogs_Track_GoToContributor_Arranger,
                                                    rel.Name),
                                                ImageResourceNames.IconPerson,
                                                new MvxAsyncCommand(() => GoToContributorVM(rel.Id))));
                                        }
                                    }
                                }
                                
                                if (goToContributorOptions.Any())
                                    await _trackOptionsService.OpenOptions(goToContributorOptions);
                            })));
                }
            }

            options.AddIf(() => shouldShowPlaybackSpeedOption,
                new StandardIconOptionPO(
                    _bmmLanguageBinder[Translations.PlayerViewModel_PlaybackSpeed],
                    ImageResourceNames.IconPlaybackSpeed,
                    new MvxAsyncCommand(async () => { await PlaybackSpeedClickedAction(); })));

            options.AddIf(() => shouldShowSleepTimerOption,
                new StandardIconOptionPO(
                    _bmmLanguageBinder[Translations.PlayerViewModel_SleepTimer],
                    ImageResourceNames.IconSleepTimer,
                    new MvxAsyncCommand(async () => await SleepTimerClickedAction())));

            options.Add(
                new StandardIconOptionPO(
                    _bmmLanguageBinder[Translations.UserDialogs_Track_MoreInformation],
                    ImageResourceNames.IconInfo,
                    new MvxAsyncCommand(async () =>
                    {
                        await ClosePlayer();
                        await _mvxNavigationService.Navigate<TrackInfoViewModel, Track>(track);
                    })));

            return options;
        }
        
        private async Task PlaybackSpeedClickedAction()
        {
            IMvxCommand CreatePlaybackSpeedTapCommand(decimal playbackSpeed)
            {
                return new MvxCommand(() =>
                {
                    _mediaPlayer.ChangePlaybackSpeed(playbackSpeed);
                    _analytics.LogEvent(Event.PlaybackSpeedOptionSelected, new Dictionary<string, object>
                    {
                        {PlaybackSpeedOptionKey, playbackSpeed}
                    });
                });
            }

            StandardIconOptionPO CreatePlaybackSpeedOption(decimal playbackSpeed)
            {
                return new StandardIconOptionPO(
                    $"{playbackSpeed.ToString(PlaybackSpeedStringFormat, CultureInfo.InvariantCulture)}x",
                    ImageResourceNames.IconPlaybackSpeed,
                    CreatePlaybackSpeedTapCommand(playbackSpeed));
            }

            var playbackSpeedOptions = _availablePlaybackSpeed
                .Select(CreatePlaybackSpeedOption)
                .ToList();

            if (_mediaPlayer.CurrentPlaybackSpeed != PlayerConstants.DefaultPlaybackSpeed)
            {
                playbackSpeedOptions.Add(new StandardIconOptionPO(
                    _bmmLanguageBinder[Translations.PlayerViewModel_Default],
                    ImageResourceNames.IconRemove,
                    CreatePlaybackSpeedTapCommand(PlayerConstants.DefaultPlaybackSpeed)));
            }

            _analytics.LogEvent(Event.PlaybackSpeedOptionsOpened);
            await _trackOptionsService.OpenOptions(playbackSpeedOptions);
        }

        private async Task SleepTimerClickedAction()
        {
            IMvxCommand CreateOptionTapCommand(SleepTimerOption sleepTimerOption)
            {
                return new MvxCommand(() =>
                {
                    _sleepTimerService.Set(sleepTimerOption);
                    _analytics.LogEvent(Event.SleepTimerOptionSelected, new Dictionary<string, object>
                    {
                        {SleepTimerOptionKey, sleepTimerOption.ToString()}
                    });
                });
            }

            StandardIconOptionPO PrepareMinutesOption(SleepTimerOption sleepTimerOption)
            {
                return new StandardIconOptionPO(
                    _bmmLanguageBinder.GetText(Translations.PlayerViewModel_Minutes, (int)sleepTimerOption),
                    ImageResourceNames.IconSleepTimer,
                    CreateOptionTapCommand(sleepTimerOption));
            }

            var sleepTimerOptions = new List<StandardIconOptionPO>
            {
                PrepareMinutesOption(SleepTimerOption.FiveMinutes),
                PrepareMinutesOption(SleepTimerOption.TenMinutes),
                PrepareMinutesOption(SleepTimerOption.FifteenMinutes),
                PrepareMinutesOption(SleepTimerOption.ThirtyMinutes),
                PrepareMinutesOption(SleepTimerOption.FortyFiveMinutes),
                new(
                    _bmmLanguageBinder.GetText(Translations.PlayerViewModel_Hour, 1),
                    ImageResourceNames.IconSleepTimer,
                    CreateOptionTapCommand(SleepTimerOption.OneHour))
            };

            if (_sleepTimerService.IsEnabled)
            {
                sleepTimerOptions.Add(new StandardIconOptionPO(
                    _bmmLanguageBinder.GetText(Translations.PlayerViewModel_Disable),
                    ImageResourceNames.IconRemove,
                    CreateOptionTapCommand(SleepTimerOption.NotSet)));
            }

            _analytics.LogEvent(Event.SleepTimerOptionsOpened);
            await _trackOptionsService.OpenOptions(sleepTimerOptions);
        }

        private async Task GoToContributorVM(int id)
        {
            await ClosePlayer();
            await _mvxNavigationService.Navigate<ContributorViewModel, int>(id);
        }

        private async Task ClosePlayer()
        {
            await _mvxNavigationService.ChangePresentation(new CloseFragmentsOverPlayerHint());
            _mvxMessenger.Publish(new TogglePlayerMessage(this, false));
            await Task.Delay(ViewConstants.DefaultAnimationDurationInMilliseconds);
        }
    }
}
