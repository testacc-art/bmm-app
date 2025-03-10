﻿using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using BMM.Api.Abstraction;
using BMM.Api.Implementation.Models;
using BMM.Core.Implementations.DocumentFilters;
using BMM.Core.Implementations.Podcasts;
using BMM.Core.Messages;
using MvvmCross.Plugin.Messenger;

namespace BMM.Core.ViewModels.MyContent
{
    public class DownloadedFollowedPodcastsViewModel : PodcastsViewModel
    {
        private readonly IPodcastOfflineManager _podcastDownloader;
        private readonly IMvxMessenger _messenger;

        protected MvxSubscriptionToken FollowedPodcastChangedToken;

        public DownloadedFollowedPodcastsViewModel(IPodcastOfflineManager podcastDownloader, IMvxMessenger messenger, IDownloadedTracksOnlyFilter downloadedTracksOnlyFilter)
            : base(downloadedTracksOnlyFilter)
        {
            _podcastDownloader = podcastDownloader;
            _messenger = messenger;

            FollowedPodcastChangedToken = _messenger.Subscribe<FollowedPodcastsChangedMessage>(async message =>
            {
                await TryRefresh();
            });
        }

        protected override void AttachEvents()
        {
            base.AttachEvents();
            PropertyChanged += OnPropertyChanged;
            Documents.CollectionChanged += DocumentsOnCollectionChanged;
        }

        protected override void DetachEvents()
        {
            base.DetachEvents();
            PropertyChanged -= OnPropertyChanged;
            Documents.CollectionChanged -= DocumentsOnCollectionChanged;
        }

        public override async Task Load()
        {
            await base.Load();
            await RaisePropertyChanged(() => ShowEmptyFollowedPodcasts);
        }

        private void DocumentsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(() => ShowEmptyFollowedPodcasts);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IsLoading):
                    RaisePropertyChanged(() => ShowEmptyFollowedPodcasts);
                    break;
            }
        }

        public bool ShowEmptyFollowedPodcasts => (Documents.Count == 0) && !IsLoading;

        public override async Task<IEnumerable<Document>> LoadItems(CachePolicy policy = CachePolicy.UseCacheAndRefreshOutdated)
        {
            var podcasts = await Client.Podcast.GetAll(policy);
            return podcasts?.Where(_podcastDownloader.IsFollowing);
        }
    }
}