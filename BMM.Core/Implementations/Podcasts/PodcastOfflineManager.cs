﻿using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Akavache;
using BMM.Api.Implementation.Models;
using BMM.Core.Helpers;
using BMM.Core.Implementations.Downloading;
using BMM.Core.Messages;
using MvvmCross.Plugin.Messenger;

namespace BMM.Core.Implementations.Podcasts
{
    public class PodcastOfflineManager: IPodcastOfflineManager
    {
        private readonly IBlobCache _blobCache;
        private readonly IMvxMessenger _messenger;
        private readonly IGlobalMediaDownloader _mediaDownloader;

        private IDictionary<int, int> _automaticDownloadTracksByPodcast;
        private ICollection<int> _followedPodcasts;

        public PodcastOfflineManager(IBlobCache blobCache, IMvxMessenger messenger, IGlobalMediaDownloader mediaDownloader)
        {
            _blobCache = blobCache;
            _messenger = messenger;
            _mediaDownloader = mediaDownloader;
        }

        public async Task InitAsync()
        {
            _automaticDownloadTracksByPodcast = await _blobCache.GetOrCreateObject(
                StorageKeys.AutomaticallyDownloadedTracks, () => new Dictionary<int, int>());

            _followedPodcasts =
                await _blobCache.GetOrCreateObject(StorageKeys.LocalPodcasts, () => new HashSet<int>());
        }

        public bool IsFollowing(Podcast podcast)
        {
            ThrowIfNotInitialized();
            return _followedPodcasts.Contains(podcast.Id);
        }

        public int GetNumberOfTracksToAutomaticallyDownload(Podcast podcast)
        {
            if (_automaticDownloadTracksByPodcast.ContainsKey(podcast.Id))
            {
                _automaticDownloadTracksByPodcast.TryGetValue(podcast.Id, out var numTracks);
                return numTracks;
            }

            return GlobalConstants.DefaultNumberOfPodcastTracksToDownload;
        }

        public void SetNumbeOfTracksToAutomaticallyDownload(Podcast podcast, int numberOfTracks)
        {
            if (_automaticDownloadTracksByPodcast.ContainsKey(podcast.Id))
            {
                _automaticDownloadTracksByPodcast.Remove(podcast.Id);
            }

            _automaticDownloadTracksByPodcast.Add(podcast.Id, numberOfTracks);

            FollowedPodcastsChanged();
        }

        public ICollection<int> GetPodcastsFollowing()
        {
            return _followedPodcasts;
        }

        public async Task FollowPodcast(Podcast podcast)
        {
            ThrowIfNotInitialized();
            if (!_followedPodcasts.Contains(podcast.Id))
            {
                _followedPodcasts.Add(podcast.Id);
                await FollowedPodcastsChanged();
            }
        }

        public async Task UnfollowPodcast(Podcast podcast)
        {
            ThrowIfNotInitialized();

            _followedPodcasts.Remove(podcast.Id);

            await FollowedPodcastsChanged();
        }

        private async Task FollowedPodcastsChanged()
        {
            await Save();
            PublishFollowedPodcastsChangedMessage();
            await _mediaDownloader.SynchronizeOfflineTracks();
        }

        private void PublishFollowedPodcastsChangedMessage()
        {
            _messenger.Publish(new FollowedPodcastsChangedMessage(this, GetPodcastsFollowing()));
        }

        private void ThrowIfNotInitialized()
        {
            if (_automaticDownloadTracksByPodcast == null)
            {
                throw new NullReferenceException("You need to call the InitAsync method first");
            }
        }

        private async Task Save()
        {
            await _blobCache.InsertObject(StorageKeys.AutomaticallyDownloadedTracks, _automaticDownloadTracksByPodcast);
            await _blobCache.InsertObject(StorageKeys.LocalPodcasts, _followedPodcasts);
        }

        public async Task Clear()
        {
            ThrowIfNotInitialized();
            _followedPodcasts.Clear();
            _automaticDownloadTracksByPodcast.Clear();
            await Save();
        }
    }
}
