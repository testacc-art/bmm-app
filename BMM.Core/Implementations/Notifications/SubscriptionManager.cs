﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BMM.Api;
using BMM.Api.Implementation.Models;
using BMM.Core.Implementations.Exceptions;
using BMM.Core.Implementations.Podcasts;
using BMM.Core.Implementations.Security;
using BMM.Core.Messages;
using MvvmCross.Logging;
using MvvmCross.Plugin.Messenger;

namespace BMM.Core.Implementations.Notifications
{
    public class SubscriptionManager : ISubscriptionManager
    {
        private readonly IPodcastOfflineManager _podcastDownloader;
        private readonly INotificationSubscriptionTokenProvider _tokenProvider;
        private readonly IMvxLog _logger;
        private readonly IExceptionHandler _exceptionHandler;
        private readonly IBMMClient _bmmClient;
        private readonly IUserAuthChecker _authChecker;

        public SubscriptionManager(
            IMvxMessenger messenger,
            IPodcastOfflineManager podcastDownloader,
            INotificationSubscriptionTokenProvider tokenProvider,
            IMvxLogProvider logProvider,
            IExceptionHandler exceptionHandler,
            IBMMClient bmmClient,
            IUserAuthChecker authChecker)
        {
            _podcastDownloader = podcastDownloader;
            _tokenProvider = tokenProvider;
            _exceptionHandler = exceptionHandler;
            _bmmClient = bmmClient;
            _authChecker = authChecker;
            _logger = logProvider.GetLogFor<SubscriptionManager>();

            LoggedInOnlineToken = messenger.Subscribe<LoggedInOnlineMessage>(message => { _exceptionHandler.FireAndForgetWithoutUserMessages(UpdateSubscriptionAndRetry); });

            ContentLanguagesChangedToken = messenger.Subscribe<ContentLanguagesChangedMessage>(message =>
            {
                _exceptionHandler.FireAndForgetWithoutUserMessages(UpdateSubscriptionAndRetry);
            });

            FollowedPodcastChangedToken = messenger.Subscribe<FollowedPodcastsChangedMessage>(message =>
            {
                _exceptionHandler.FireAndForgetWithoutUserMessages(UpdateSubscriptionAndRetry);
            });
        }

        protected MvxSubscriptionToken LoggedInOnlineToken;

        protected MvxSubscriptionToken ContentLanguagesChangedToken;

        protected MvxSubscriptionToken FollowedPodcastChangedToken;

        // todo pass token to conform to the API
        public virtual async Task UpdateSubscriptionAndRetry()
        {
            if (!await _authChecker.IsUserAuthenticated())
                return;

            const int waitMillis = 5000;
            const int maxTries = 5;
            var tries = 0;

            var tryAgain = true;

            while (tryAgain)
            {
                try
                {
                    await UpdateSubscription();
                    tryAgain = false;
                }
                catch (Exception)
                {
                    if (tries == maxTries)
                    {
                        tryAgain = false;
                    }
                    else
                    {
                        tries++;
                        await Task.Delay(waitMillis);
                    }
                }
            }
        }

        protected virtual async Task UpdateSubscription()
        {
            var token = await _tokenProvider.GetToken();

            if (token != null)
            {
                _logger.InfoFormat("FCM registration token: {0}", token);
                await UpdateSubscription(token);
            }
            else
            {
                throw new WebException("Can't update subscription: Token not available yet");
            }
        }

        protected virtual async Task UpdateSubscription(string token)
        {
            var podcastReferences = _podcastDownloader.GetPodcastsFollowing()
                .Select(podcastId => new PodcastReference {Id = podcastId}).ToList();

            var subscription = new Subscription
            {
                Token = token,
                DeviceId = token.GetHashCode().ToString("x8"),
                PodcastReferences = podcastReferences
            };

            await _bmmClient.Subscription.Save(subscription);
        }
    }
}