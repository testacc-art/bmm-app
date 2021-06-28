using System;
using System.ComponentModel;
using CoreGraphics;
using Foundation;
using UIKit;

namespace BMM.UI.iOS
{
    [Register(nameof(LeftImageButton)), DesignTimeVisible(true)]
    public class LeftImageButton : UIButton
    {
        public LeftImageButton()
        {
        }

        public LeftImageButton(IntPtr handle)
            : base(handle)
        {
        }

        public override UIControlContentHorizontalAlignment EffectiveContentHorizontalAlignment => UIControlContentHorizontalAlignment.Left;

        public override CGRect TitleRectForContentRect(CGRect rect)
        {
            var titleRect = base.TitleRectForContentRect(rect);
            var imageSize = CurrentImage.Size;
            var availableWidth = rect.Width - ImageEdgeInsets.Right - imageSize.Width * 2 - titleRect.Width;
            titleRect.Offset(Math.Round(availableWidth / 2), 0);
            return titleRect;
        }
    }
}