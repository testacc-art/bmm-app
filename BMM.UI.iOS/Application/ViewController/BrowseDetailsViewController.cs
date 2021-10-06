﻿using System;
using BMM.Core.ValueConverters;
using BMM.Core.ViewModels;
using BMM.UI.iOS.Constants;
using MvvmCross.Binding.BindingContext;
using MvvmCross.Platforms.Ios.Views;

namespace BMM.UI.iOS
{
    public partial class BrowseDetailsViewController : BaseViewController<BrowseDetailsViewModel>, IHaveLargeTitle
    {
        public BrowseDetailsViewController() : base(nameof(BrowseViewController))
        {
        }

        public double? InitialLargeTitleHeight { get; set; }

        public override Type ParentViewControllerType => typeof(ContainmentNavigationViewController);

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            var refreshControl = new MvxUIRefreshControl
            {
                TintColor = AppColors.RefreshControlTintColor
            };

            BrowseTableView.RefreshControl = refreshControl;

            var source = new NotSelectableDocumentsTableViewSource(BrowseTableView);

            var set = this.CreateBindingSet<BrowseDetailsViewController, BrowseViewModel>();

            set.Bind(source)
                .To(vm => vm.Documents)
                .WithConversion<DocumentListValueConverter>(ViewModel);

            set.Bind(source)
                .For(s => s.SelectionChangedCommand)
                .To(s => s.DocumentSelectedCommand)
                .WithConversion<DocumentSelectedCommandValueConverter>();

            set.Bind(source)
                .For(s => s.IsFullyLoaded)
                .To(vm => vm.IsLoading)
                .WithConversion<InvertedVisibilityConverter>();

            set.Bind(refreshControl)
                .For(r => r.IsRefreshing)
                .To(vm => vm.IsRefreshing);

            set.Bind(refreshControl)
                .For(r => r.RefreshCommand)
                .To(vm => vm.ReloadCommand);

            set.Apply();
        }
    }
}