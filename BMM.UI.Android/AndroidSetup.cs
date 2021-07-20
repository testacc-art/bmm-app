﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Acr.UserDialogs;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using AndroidX.AppCompat.Widget;
using AndroidX.CardView.Widget;
using AndroidX.DrawerLayout.Widget;
using AndroidX.ViewPager.Widget;
using BMM.Api.Framework;
using BMM.Api.Framework.HTTP;
using BMM.Core;
using BMM.Core.Helpers;
using BMM.Core.Implementations;
using BMM.Core.Implementations.Analytics;
using BMM.Core.Implementations.DownloadManager;
using BMM.Core.Implementations.Exceptions;
using BMM.Core.Implementations.FileStorage;
using BMM.Core.Implementations.FirebaseRemoteConfig;
using BMM.Core.Implementations.Notifications;
using BMM.Core.Implementations.Player;
using BMM.Core.Implementations.UI;
using BMM.Core.NewMediaPlayer;
using BMM.Core.NewMediaPlayer.Abstractions;
using BMM.UI.Droid.Application.Bindings;
using BMM.UI.Droid.Application.DownloadManager;
using BMM.UI.Droid.Application.Helpers;
using BMM.UI.Droid.Application.Implementations;
using BMM.UI.Droid.Application.Implementations.FileStorage;
using BMM.UI.Droid.Application.Implementations.FirebaseRemoteConfig;
using BMM.UI.Droid.Application.Implementations.Notifications;
using BMM.UI.Droid.Application.Implementations.Oidc;
using BMM.UI.Droid.Application.Implementations.UI;
using BMM.UI.Droid.Application.Media;
using BMM.UI.Droid.Application.NewMediaPlayer;
using BMM.UI.Droid.Application.NewMediaPlayer.Controller;
using BMM.UI.Droid.Application.NewMediaPlayer.Notification;
using BMM.UI.Droid.Application.NewMediaPlayer.Playback;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Navigation;
using IdentityModel.OidcClient.Browser;
using MvvmCross;
using MvvmCross.Base;
using MvvmCross.Binding.Bindings.Target.Construction;
using MvvmCross.DroidX;
using MvvmCross.DroidX.RecyclerView;
using MvvmCross.IoC;
using MvvmCross.Platforms.Android;
using MvvmCross.Platforms.Android.Core;
using MvvmCross.Platforms.Android.Presenters;
using MvvmCross.ViewModels;
using TrackMediaHelper = BMM.UI.Droid.Application.Implementations.Media.TrackMediaHelper;

namespace BMM.UI.Droid
{
    public class AndroidSetup : MvxAndroidSetup<App>
    {
        protected override IMvxApplication CreateApp()
        {
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
            {
                LogError("AndroidEnvironment.UnhandledException", args.Exception.ToString());
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                LogError("AppDomain.CurrentDomain.UnhandledException", args.ExceptionObject.ToString());
            };

            return new App();
        }

        private void LogError(string location, string exception)
        {
#if DEBUG
            Log.Error(location, exception);
#endif
        }

        protected override IMvxIocOptions CreateIocOptions()
        {
            return new MvxIocOptions
            {
                PropertyInjectorOptions = MvxPropertyInjectorOptions.MvxInject
            };
        }

        protected override IMvxAndroidViewPresenter CreateViewPresenter()
        {
            var customPresenter = new ViewModelAwareViewPresenter(AndroidViewAssemblies);
            Mvx.IoCProvider.RegisterSingleton<IMvxAndroidViewPresenter>(customPresenter);
            Mvx.IoCProvider.RegisterSingleton<IViewModelAwareViewPresenter>(customPresenter);
            return customPresenter;
        }

        protected override IEnumerable<Assembly> AndroidViewAssemblies
        {
            get
            {
                var assemblies = new List<Assembly>(base.AndroidViewAssemblies)
                {
                    typeof(NavigationView).Assembly,
                    typeof(FloatingActionButton).Assembly,
                    typeof(Toolbar).Assembly,
                    typeof(CardView).Assembly,
                    typeof(DrawerLayout).Assembly,
                    typeof(ViewPager).Assembly,
                    typeof(MvxRecyclerView).Assembly,
                    typeof(MvxSwipeRefreshLayout).Assembly
                };

                return assemblies;
            }
        }

        protected override void InitializeFirstChance()
        {
            base.InitializeFirstChance();

            Mvx.IoCProvider.CallbackWhenRegistered<IMvxTargetBindingFactoryRegistry>(RegisterAdditionalBindings);

            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<IStopwatchManager, StopwatchManager>();
            var stopwatch = Mvx.IoCProvider.Resolve<IStopwatchManager>();
            stopwatch.StartAndGetStopwatch(StopwatchType.AppStart);

            Firebase.FirebaseApp.InitializeApp(ApplicationContext);

            Mvx.IoCProvider.RegisterType<INotificationSubscriptionTokenProvider, FirebaseTokenProvider>();
#if DEBUG
            Mvx.IoCProvider.RegisterType<ILogger>(
                // Use direct instance of UserDialog.Instance to avoid nested-loops
                () => new ErrorDialogDisplayingLogger(UserDialogs.Instance, new AndroidLogger(), Mvx.IoCProvider.Resolve<IMvxMainThreadAsyncDispatcher>())
            );
#else
            Mvx.IoCProvider.RegisterType<ILogger, AndroidLogger>();
#endif

            Mvx.IoCProvider.RegisterType<IClipboardService, ClipboardService>();

            Mvx.IoCProvider.RegisterSingleton<ISdkVersionHelper>(new SdkVersionHelper(Build.VERSION.SdkInt));
            Mvx.IoCProvider.RegisterType<ISimpleHttpClient, SimpleHttpClient>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<ITrackMediaHelper, TrackMediaHelper>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<IDownloadManager, AndroidDownloadManager>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<IStorageManager, StorageManager>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<INotificationDisplayer, NotificationDisplayer>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<IPlatformSpecificRemoteConfig, AndroidFirebaseRemoteConfig>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<IDeviceSupportVersionChecker, AndroidSupportVersionChecker>();

            Mvx.IoCProvider.RegisterType<IUriOpener, UriOpener>();
            Mvx.IoCProvider.RegisterType<DownloadCompletedHandler, DownloadCompletedHandler>();
            Mvx.IoCProvider.RegisterType<MediaMountedHandler, MediaMountedHandler>();

            Mvx.IoCProvider.RegisterType<IBrowser, BrowserSelector>();

            UserDialogs.Init(() => Mvx.IoCProvider.Resolve<IMvxAndroidCurrentTopActivity>().Activity);

            InitializeMediaPlayer();
        }

        private void RegisterAdditionalBindings(IMvxTargetBindingFactoryRegistry registry)
        {
            registry.RegisterFactory(new MvxCustomBindingFactory<View>("BackgroundTint", view => new MvxBackgroundTintBinding(view)));
            registry.RegisterFactory(new MvxCustomBindingFactory<CardView>("CardVisibility", card => new MvxCardVisibility(card)));
            registry.RegisterFactory(new MvxCustomBindingFactory<CardView>("CardCircle", card => new MvxCardCircle(card)));
            registry.RegisterFactory(new MvxCustomBindingFactory<CardView>("CardBackgroundColor", card => new MvxCardBackgroundColor(card)));
        }

        private void InitializeMediaPlayer()
        {
            Mvx.IoCProvider.RegisterType<IMediaPlayerInitializer, NullMediaPlayerInitializer>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<IMediaQueue, MediaQueue>();
            Mvx.IoCProvider.RegisterType<MediaControllerCallback, MediaControllerCallback>();
            Mvx.IoCProvider.RegisterType<PeriodicExecutor, PeriodicExecutor>();
            Mvx.IoCProvider.RegisterType<PlaybackStateCompatMapper, PlaybackStateCompatMapper>();
            Mvx.IoCProvider.RegisterType<MediaSessionConnector.IPlaybackPreparer, ExoPlaybackPreparer>();

            Mvx.IoCProvider.RegisterType<NotificationChannelBuilder>();
            Mvx.IoCProvider.RegisterType<IMetadataMapper, MetadataMapper>();
            Mvx.IoCProvider.LazyConstructAndRegisterSingleton<IPlatformSpecificMediaPlayer, AndroidMediaPlayer>();
        }

        public override void InitializeSecondary()
        {
            try
            {
                base.InitializeSecondary();
            }
            catch (Exception e)
            {
                // Since the exception is thrown on a background thread we need to move it to the main thread.
                // On the main thread it makes the app crash as expected
                // This is needed to make the app crash when something goes wrong at AppStart. E.g. MvxIoCResolveException
                var dispatcher = Mvx.IoCProvider.Resolve<IMvxMainThreadAsyncDispatcher>();
                dispatcher.ExecuteOnMainThreadAsync(() => { throw e; });

                throw;
            }
        }
    }
}