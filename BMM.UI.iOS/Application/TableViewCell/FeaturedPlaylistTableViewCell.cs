﻿using BMM.Core.ViewModels;
using MvvmCross.Binding.BindingContext;
using Foundation;
using System;
using BMM.Core.ValueConverters;
using BMM.UI.iOS.Extensions;
using UIKit;
using BMM.Api.Implementation.Models;
using BMM.Api.Implementation.Models.Interfaces;
using BMM.UI.iOS.Constants;
using BMM.UI.iOS.Helpers;

namespace BMM.UI.iOS
{
    public partial class FeaturedPlaylistTableViewCell : BaseBMMTableViewCell
    {
        public static readonly UINib Nib = UINib.FromName(nameof(FeaturedPlaylistTableViewCell), NSBundle.MainBundle);
        public static readonly NSString Key = new NSString(nameof(FeaturedPlaylistTableViewCell));

        public FeaturedPlaylistTableViewCell(IntPtr handle)
            : base(handle)
        {
            this.DelayBind(() =>
            {
                var set = this.CreateBindingSet<FeaturedPlaylistTableViewCell, CellWrapperViewModel<ITrackListDisplayable>>();
                set.Bind(TitleLabel).To(vm => vm.Item.Title);
                CoverImageView.ErrorAndLoadingPlaceholderImagePathForCover();
                set.Bind(CoverImageView)
                    .For(v => v.ImagePath)
                    .To(vm => vm.Item.Cover)
                    .WithConversion<CoverUrlToFallbackImageValueConverter>(IosConstants.CoverPlaceholderImage);
                set.Apply();
            });
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            TitleLabel.ApplyTextTheme(AppTheme.Title2);
        }
    }
}