using MvvmCross.Binding.BindingContext;
using Foundation;
using System;
using System.Globalization;
using BMM.Core.ValueConverters;
using BMM.UI.iOS.Constants;

namespace BMM.UI.iOS
{
    public partial class LanguageContentTableViewCell : BaseBMMTableViewCell
    {
        public int Index {
            get { return 0; }
            set { IndexLabel.Text = value + ""; }
        }

        public static readonly NSString Key = new NSString(nameof(LanguageContentTableViewCell));

        public LanguageContentTableViewCell(IntPtr handle)
            : base(handle)
        {
            this.DelayBind(() =>
            {
                var set = this.CreateBindingSet<LanguageContentTableViewCell, CultureInfo>();
                set.Bind(TextLabel).WithConversion<LanguageNameValueConverter>();
                set.Apply();
            });
        }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            TextLabel.ApplyTextTheme(AppTheme.Title2);
            IndexLabel.ApplyTextTheme(AppTheme.Title2);
        }
    }
}