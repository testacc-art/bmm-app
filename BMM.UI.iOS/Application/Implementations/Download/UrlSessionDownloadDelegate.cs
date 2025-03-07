﻿using System;
using System.Threading.Tasks;
using BMM.Api.Framework;
using BMM.Core.Implementations.Security;
using BMM.UI.iOS.DownloadManager;
using Foundation;
using MvvmCross;

namespace BMM.UI.iOS.Implementations.Download
{
    public class SpecificUrlSessionDownloadDelegate : UrlSessionDownloadDelegate
    {
        private readonly ILogger _logger;

        public SpecificUrlSessionDownloadDelegate(ILogger logger)
        {
            _logger = logger;
        }

        /**
         * Define what should happen to a download-task when receiving a challenge
         */
        public override void DidReceiveChallenge(NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
        {
            var tcs = new TaskCompletionSource<IToken>();
            Task.Run(async () =>
                {
                    try
                    {
                        var storage = Mvx.IoCProvider.Resolve<ICredentialsStorage>();
                        tcs.SetResult(await storage.GetToken());
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }).ConfigureAwait(false);

            if (tcs.Task.Exception != null)
            {
                throw new AggregateException(tcs.Task.Exception);
            }

            var token = tcs.Task.Result;

            completionHandler(NSUrlSessionAuthChallengeDisposition.UseCredential, new NSUrlCredential(token.Username, token.AuthenticationToken, NSUrlCredentialPersistence.None));
        }

        public override void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError error)
        {
            if (error == null)
            {
                _logger.Info("IosDownloadManager", $"TaskIdentifier: {task.TaskIdentifier} - Download completed successfully.");
            }
            else
            {
                _logger.Warn("IosDownloadManager",$"TaskIdentifier: {task.TaskIdentifier} - Download completed with error: {error.LocalizedDescription}");
            }
        }
    }
}