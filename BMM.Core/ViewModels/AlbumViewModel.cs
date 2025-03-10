﻿using System;
using BMM.Api.Implementation.Models;
using BMM.Core.ViewModels.Base;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using BMM.Api.Abstraction;
using BMM.Core.Extensions;
using BMM.Core.GuardedActions.ContinueListening.Interfaces;
using BMM.Core.Helpers;
using BMM.Core.Implementations.TrackInformation.Strategies;
using BMM.Core.Translation;
using BMM.Core.ViewModels.Interfaces;
using MvvmCross.Commands;
using MvvmCross.ViewModels;

namespace BMM.Core.ViewModels
{
    public class AlbumViewModel : DocumentsViewModel, IMvxViewModel<int>, IMvxViewModel<Album>, IAlbumViewModel
    {
        private readonly IResumeOrShufflePlayAction _resumeOrShufflePlayAction;
        private int _id;

        /// <summary>
        /// While iOS reuses <see cref="BaseViewModel.OptionsAction"/> Android creates a new menu using these commands.
        /// </summary>
        public IMvxCommand AddToPlaylistCommand { get; }

        public IMvxCommand ShareCommand { get; }

        public override IMvxCommand ShufflePlayCommand => _resumeOrShufflePlayAction.Command;

        private Album _album;

        public Album Album
        {
            get => _album;
            set
            {
                SetProperty(ref _album, value);
                RaisePropertyChanged(() => Title);
                RaisePropertyChanged(() => Description);
                RaisePropertyChanged(() => Image);
                RaisePropertyChanged(() => ShowImage);
                RaisePropertyChanged(() => ShuffleOrResumeText);
            }
        }

        public override IEnumerable<string> PlaybackOrigin()
        {
            return new[] { Album.Id.ToString() };
        }

        public AlbumViewModel(IShareLink shareLink, IResumeOrShufflePlayAction resumeOrShufflePlayAction)
        {
            _resumeOrShufflePlayAction = resumeOrShufflePlayAction;
            _resumeOrShufflePlayAction.AttachDataContext(this);
            
            AddToPlaylistCommand = new ExceptionHandlingCommand(async () => await AddAlbumToTrackCollection(Album.Id));
            ShareCommand = new ExceptionHandlingCommand(async () => await shareLink.For(_album));

            var audiobookStyler = new AudiobookPodcastInfoProvider(TrackInfoProvider);
            TrackInfoProvider = new CustomTrackInfoProvider(TrackInfoProvider,
                (track, culture, defaultTrack) =>
                {
                    if (audiobookStyler.HasSpecificStyling(track))
                        return audiobookStyler.GetTrackInformation(track, culture, defaultTrack);

                    if (track.Subtype == TrackSubType.Speech || track.Subtype == TrackSubType.Exegesis)
                    {
                        return new TrackInformation
                        {
                            Label = track.Artist,
                            Subtitle = defaultTrack.Subtitle,
                            Meta = string.IsNullOrWhiteSpace(track.Title) || track.Title == track.Artist ? ReadableDuration(track.Duration) : track.Title
                        };
                    }

                    return new TrackInformation
                    {
                        Label = track.Title,
                        Subtitle = defaultTrack.Subtitle,
                        Meta = string.IsNullOrWhiteSpace(track.Artist) || track.Artist == track.Title ? ReadableDuration(track.Duration) : track.Artist
                    };
                });
        }

        protected override void AttachEvents()
        {
            base.AttachEvents();
            Documents.CollectionChanged += UpdateView;
        }

        protected override void DetachEvents()
        {
            base.DetachEvents();
            Documents.CollectionChanged -= UpdateView;
        }

        public override async Task Load()
        {
            await base.Load();
            await RaisePropertyChanged(() => ShowShuffleOrResumeButton);
        }

        public string ReadableDuration(long durationInMilliseconds)
        {
            var span = TimeSpan.FromMilliseconds(durationInMilliseconds);
            var seconds = span.Seconds;
            var minutes = Math.Floor(span.TotalMinutes);
            return $"{minutes} minutes {seconds} seconds";
        }

        /// <summary>
        /// In case we already have a title we can show the title immediately without waiting for <see cref="LoadItems"/>.
        /// </summary>
        public void Prepare(Album album)
        {
            _id = album.Id;
            Album = album;
        }

        public void Prepare(int id)
        {
            _id = id;
        }

        public override async Task<IEnumerable<Document>> LoadItems(CachePolicy policy = CachePolicy.UseCacheAndRefreshOutdated)
        {
            Album = await Client.Albums.GetById(_id);
            return Album?.Children;
        }

        private void UpdateView(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(() => ShowShuffleOrResumeButton);
        }

        public bool ShowSharingInfo => false;

        public bool ShowDownloadButtons => false;

        public bool IsDownloaded => false;

        public string Title => Album?.Title;

        public string Description => Album?.Description;

        public bool ShowPlaylistIcon => false;
        public bool ShowImage => Album?.Cover != null;
        public string Image => Album?.Cover;
        public bool UseCircularImage => false;

        public bool ShowFollowButtons => false;

        public bool ShowShuffleOrResumeButton => Documents.OfType<Track>().Any();
        public string ShuffleOrResumeText => GetShowShuffleOrResumeText();

        private string GetShowShuffleOrResumeText()
        {
            if (Album != null && Album.LatestTrackId.HasValue)
                return TextSource[Translations.TrackCollectionViewModel_Resume];
            
            return TextSource[Translations.TrackCollectionViewModel_ShufflePlay];
        }

        public bool ShowPlayButton => false;

        public bool ShowTrackCount => true;

        public bool ShowFollowSharedPlaylistButton => false;

        public override string TrackCountString => Documents.OfType<Album>().Any() ? TextSource.GetText(Translations.AlbumViewModel_PluralAlbums, Documents.OfType<Album>().Count()) : base.TrackCountString;
    }
}