﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Acr.UserDialogs;
using BMM.Core.Extensions;
using BMM.Core.Helpers;
using BMM.Core.Implementations;
using BMM.Core.Implementations.Analytics;
using BMM.Core.Implementations.Caching;
using BMM.Core.Implementations.Connection;
using BMM.Core.Implementations.Device;
using BMM.Core.Implementations.Downloading;
using BMM.Core.Implementations.Exceptions;
using BMM.Core.Implementations.Feedback;
using BMM.Core.Implementations.FileStorage;
using BMM.Core.Implementations.FirebaseRemoteConfig;
using BMM.Core.Implementations.Notifications;
using BMM.Core.Implementations.Security;
using BMM.Core.Implementations.UI;
using BMM.Core.Messages;
using BMM.Core.Models;
using BMM.Core.Translation;
using BMM.Core.ViewModels.Base;
using MvvmCross;
using MvvmCross.Commands;
using MvvmCross.Plugin.Messenger;

namespace BMM.Core.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly IDeviceInfo _deviceInfo;
        private readonly INetworkSettings _networkSettings;
        private readonly ISettingsStorage _settingsStorage;
        private readonly INotificationSubscriptionTokenProvider _tokenProvider;
        private readonly IClipboardService _clipboard;
        private readonly IDeveloperPermission _developerPermission;
        private readonly IContacter _contacter;
        private readonly IAnalytics _analytics;
        private readonly IStorageManager _storageManager;
        private readonly ICache _cache;
        private readonly IGlobalMediaDownloader _mediaDownloader;
        private readonly IAppNavigator _appNavigator;
        private readonly ILogoutService _logoutService;
        private readonly IUriOpener _uriOpener;
        private readonly IExceptionHandler _exceptionHandler;
        private readonly IProfileLoader _profileLoader;
        private readonly IUserStorage _userStorage;
        private readonly IFirebaseRemoteConfig _remoteConfig;
        private readonly IFeatureSupportInfoService _featureSupportInfoService;
        private SelectableListItem _externalStorage;

        private List<IListItem> _listItems = new List<IListItem>();
        private string _profilePictureUrl;

        public List<IListItem> ListItems { get => _listItems; set => SetProperty(ref _listItems, value); }

        public IMvxCommand ItemSelectedCommand =>
            new MvxCommand<IListItem>(item => (item as IListContentItem)?.OnSelected?.Execute());

        public SettingsViewModel(
            IDeviceInfo deviceInfo,
            INetworkSettings networkSettings,
            INotificationSubscriptionTokenProvider tokenProvider,
            IClipboardService clipboard,
            IDeveloperPermission developerPermission,
            IContacter contacter,
            IAnalytics analytics,
            ISettingsStorage settingsStorage,
            IStorageManager storageManager,
            ICache cache,
            IGlobalMediaDownloader mediaDownloader,
            IAppNavigator appNavigator,
            ILogoutService logoutService,
            IUriOpener uriOpener,
            IExceptionHandler exceptionHandler,
            IProfileLoader profileLoader,
            IUserStorage userStorage,
            IFirebaseRemoteConfig remoteConfig,
            IFeatureSupportInfoService featureSupportInfoService)
        {
            _deviceInfo = deviceInfo;
            _networkSettings = networkSettings;
            _tokenProvider = tokenProvider;
            _clipboard = clipboard;
            _developerPermission = developerPermission;
            _contacter = contacter;
            _analytics = analytics;
            _settingsStorage = settingsStorage;
            _storageManager = storageManager;
            _cache = cache;
            _mediaDownloader = mediaDownloader;
            _appNavigator = appNavigator;
            _logoutService = logoutService;
            _uriOpener = uriOpener;
            _exceptionHandler = exceptionHandler;
            _profileLoader = profileLoader;
            _userStorage = userStorage;
            _remoteConfig = remoteConfig;
            _featureSupportInfoService = featureSupportInfoService;
            Messenger.Subscribe<SelectedStorageChangedMessage>(message => { ChangeStorageText(message.FileStorage); }, MvxReference.Strong);
        }

        public override void ViewCreated()
        {
            base.ViewCreated();
            PropertyChanged += OnPropertyChanged;
            _storageManager.Storages.CollectionChanged += OnStoragesCollectionChanged;
        }

        public override void ViewDestroy(bool viewFinishing = true)
        {
            base.ViewDestroy(viewFinishing);
            PropertyChanged -= OnPropertyChanged;
            _storageManager.Storages.CollectionChanged -= OnStoragesCollectionChanged;
        }

        private async void OnStoragesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            await BuildSections();
        }

        private async void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TextSource))
                await BuildSections();
        }

        private void ChangeStorageText(IFileStorage fileStorage)
        {
            _externalStorage.Text = CurrentStorageInfo(fileStorage);
        }

        public override async Task Initialize()
        {
            await base.Initialize();
            await BuildSections();
            _exceptionHandler.FireAndForget(LoadProfilePicture);
        }

        private async Task LoadProfilePicture()
        {
            var profile = await _profileLoader.LoadProfile();
            _profilePictureUrl = profile.Picture;
            await BuildSections();
        }

        private async Task BuildSections()
        {
            var items = new List<List<IListItem>>
            {
                await BuildProfileSection(),
                await BuildSettingsSection(),
                await BuildGeneralSection(),
                await BuildAboutSection()
            };

            ListItems = items.SelectMany(x => x).ToList();
        }

        private async Task<List<IListItem>> BuildProfileSection()
        {
            return new List<IListItem>
            {
                new ProfileListItem
                {
                    Text = TextSource[Translations.SettingsViewModel_Logout],
                    LogoutCommand = new ExceptionHandlingCommand(async () => await Logout()),
                    Title = TextSource[Translations.SettingsViewModel_LoggedInAs],
                    UserProfileUrl = _profilePictureUrl,
                    Username = _userStorage.GetUser().FullName,
                    EditProfileCommand = new ExceptionHandlingCommand(async () => _uriOpener.OpenUri(new Uri(_remoteConfig.EditProfileUrl)))
                }
            };
        }

        private async Task<List<IListItem>> BuildSettingsSection()
        {
            var items = new List<IListItem>
            {
                new SectionHeader {ShowDivider = false, Title = TextSource[Translations.SettingsViewModel_HeadlineSettings]},
                new CheckboxListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionAutoplayHeader],
                    Text = TextSource[Translations.SettingsViewModel_OptionAutoplayText],
                    IsChecked = await _settingsStorage.GetAutoplayEnabled(),
                    OnChanged = isChecked => _settingsStorage.SetAutoplayEnabled(isChecked)
                },
                new CheckboxListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionStreakHeader],
                    Text = TextSource[Translations.SettingsViewModel_OptionStreakText],
                    IsChecked = !await _settingsStorage.GetStreakHidden(),
                    OnChanged = isChecked => _settingsStorage.SetStreakHidden(!isChecked)
                },
                new CheckboxListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionDownloadMobileNetworkHeader],
                    Text = TextSource[Translations.SettingsViewModel_OptionDownloadMobileNetworkText],
                    IsChecked = await _networkSettings.GetMobileNetworkDownloadAllowed(),
                    OnChanged = mobileNetworkDownloadAllowed =>
                    {
                        _settingsStorage.SetMobileNetworkDownloadAllowed(mobileNetworkDownloadAllowed);
                        _mediaDownloader.SynchronizeOfflineTracks();
                        Messenger.Publish(new MobileNetworkDownloadAllowedChangeMessage(this, mobileNetworkDownloadAllowed));
                    }
                },
                new CheckboxListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionPushNotifications],
                    Text = TextSource[Translations.SettingsViewModel_OptionPushNotificationsSubtitle],
                    IsChecked = await _networkSettings.GetPushNotificationsAllowed(),
                    OnChanged = notificationsEnabled =>
                    {
                        _settingsStorage.SetPushNotificationsAllowed(notificationsEnabled);
                        Messenger.Publish(new PushNotificationsStatusChangedMessage(this, notificationsEnabled));
                    }
                }
            };

            if (_storageManager.HasMultipleStorageSupport)
            {
                _externalStorage = new SelectableListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionExternalStorage],
                    Text = CurrentStorageInfo(_storageManager.SelectedStorage),
                    OnSelected = NavigationService.NavigateCommand<StorageManagementViewModel>()
                };
                items.Add(_externalStorage);
            }

            _analytics.LogEvent("Open Settings screen",
                new Dictionary<string, object>
                {
                    {"DownloadOverMobileNetworkAllowed", await _networkSettings.GetMobileNetworkDownloadAllowed()},
                    {"PushNotificationsAllowed", await _networkSettings.GetPushNotificationsAllowed()}
                });

            return items;
        }

        private string CurrentStorageInfo(IFileStorage storage)
        {
            var gigabytesString = ByteToStringHelper.BytesToMegaBytes(storage.UsableSpace);

            return storage.StorageKind == StorageKind.Internal
                ? TextSource.GetText(Translations.SettingsViewModel_OptionMBInternalText, gigabytesString)
                : TextSource.GetText(Translations.SettingsViewModel_OptionMBExternalText, gigabytesString);
        }

        private async Task<List<IListItem>> BuildGeneralSection()
        {
            var generalSectionItems = new List<IListItem>
            {
                new SectionHeader {Title = TextSource[Translations.SettingsViewModel_HeadlineGeneral]},
                new SelectableListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionLanguageAppHeader],
                    Text = TextSource[Translations.SettingsViewModel_OptionLanguageAppText],
                    OnSelected = NavigationService.NavigateCommand<LanguageAppViewModel>()
                },
                new SelectableListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionLanguageContentHeader],
                    Text = TextSource[Translations.SettingsViewModel_OptionLanguageContentText],
                    OnSelected = NavigationService.NavigateCommand<LanguageContentViewModel>()
                }
            };
            
            generalSectionItems.AddIf(() => _featureSupportInfoService.SupportsDarkMode, new SelectableListItem
            {
                Title = TextSource[Translations.SettingsViewModel_ThemeHeader],
                Text = TextSource[Translations.SettingsViewModel_ThemeText],
                OnSelected = NavigationService.NavigateCommand<ThemeSettingsViewModel>()
            });
            
            generalSectionItems.AddIf(() => _featureSupportInfoService.SupportsSiri, new SelectableListItem
            {
                Title = TextSource[Translations.SettingsViewModel_SiriShortcutsHeader],
                Text = TextSource[Translations.SettingsViewModel_SiriShortcutsText],
                OnSelected = NavigationService.NavigateCommand<SiriShortcutsViewModel>()
            });

            return generalSectionItems;
        }

        private async Task<List<IListItem>> BuildAboutSection()
        {
            var items = new List<IListItem>
            {
                new SectionHeader {Title = TextSource[Translations.SettingsViewModel_HeadlineAbout]},
                new SelectableListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_UserVoiceHeader],
                    Text = TextSource[Translations.SettingsViewModel_UserVoiceText],
                    OnSelected = new MvxCommand(() => _uriOpener.OpenUri(new Uri(_remoteConfig.UserVoiceLink)))
                },
                new SelectableListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionCopyrightHeader],
                    Text = TextSource[Translations.SettingsViewModel_OptionCopyrightText],
                    OnSelected = NavigationService.NavigateCommand<CopyrightViewModel>()
                },
                new SelectableListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionContactHeader],
                    Text = TextSource[Translations.SettingsViewModel_OptionContactText],
                    OnSelected = new MvxAsyncCommand(_contacter.Contact)
                },
                new SelectableListItem
                {
                    Title = TextSource[Translations.SettingsViewModel_OptionAppVersionHeader],
                    Text = GlobalConstants.AppVersion,
                    OnSelected = new ExceptionHandlingCommand(async () => await ShowInfo())
                }
            };

            var analyticsId =  _userStorage.GetUser().AnalyticsId;
            items.Add(new SelectableListItem
            {
                Title = "Analytics id",
                Text = analyticsId,
                OnSelected = new MvxCommand(() => _clipboard.CopyToClipboard(analyticsId))
            });

            if (_developerPermission.IsBmmDeveloper())
            {
                var firebaseToken = await _tokenProvider.GetToken();
                items.Add(new SelectableListItem
                {
                    Title = "Firebase Token",
                    Text = firebaseToken,
                    OnSelected = new MvxCommand(() => _clipboard.CopyToClipboard(firebaseToken))
                });
            }

            if (_developerPermission.IsBmmDeveloper())
            {
                items.Add(new SelectableListItem
                {
                    Title = "Clear cache",
                    Text = "Clear local cache and reload metadata from the server",
                    OnSelected = new ExceptionHandlingCommand(async () => await _cache.Clear())
                });

                items.Add(new SelectableListItem
                {
                    Title = "Crash the app",
                    Text = "Causes an exception that stops the app",
                    OnSelected = new MvxCommand(CrashTheApp)
                });
            }

            return items;
        }

        private void CrashTheApp()
        {
            throw new Exception("Forcefully crash the app!");
        }

        private async Task Logout()
        {
            var result = await Mvx.IoCProvider.Resolve<IUserDialogs>().ConfirmAsync(TextSource[Translations.SettingsViewModel_LogoutConfirm]);
            if (!result) return;

            await _logoutService.PerformLogout();
            await _appNavigator.NavigateToLogin(false);

            // Event is fired after UI updated to ensure depending UI changes
            // are not called too early
            Messenger.Publish(new LoggedOutMessage(this));
        }

        private async Task ShowInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(TextSource[Translations.SettingsViewModel_AppInfoAppVersion] + GlobalConstants.AppVersion);
            sb.AppendLine(TextSource[Translations.SettingsViewModel_AppInfoManufacturer] + _deviceInfo.Manufacturer);
            sb.AppendLine(TextSource[Translations.SettingsViewModel_AppInfoDeviceModel] + _deviceInfo.Model);
            sb.AppendLine(TextSource[Translations.SettingsViewModel_AppInfoDevicePlatform] + _deviceInfo.Platform);
            sb.AppendLine(TextSource[Translations.SettingsViewModel_AppInfoDeviceVersion] + _deviceInfo.VersionString);

            await Mvx.IoCProvider.Resolve<IUserDialogs>()
                .AlertAsync(new AlertConfig
                {
                    Title = TextSource[Translations.SettingsViewModel_AppInfoTitle],
                    Message = sb.ToString()
                });
        }
    }
}