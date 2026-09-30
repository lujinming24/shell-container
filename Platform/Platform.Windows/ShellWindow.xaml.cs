using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ShellContainer
{
    public partial class ShellWindow : Window
    {
        public ShellWindow()
        {
            InitializeComponent();
        }

        public void SetEnvironment(CoreWebView2Environment env)
        {
            _ = WebView.EnsureCoreWebView2Async(env);
        }

        public WebView2Raw WebViewRaw => new WebView2Raw(WebView);
    }

    /// <summary>把 WebView2 控件封装一层，方便外部访问。</summary>
    public sealed class WebView2Raw
    {
        private readonly Microsoft.Web.WebView2.Wpf.WebView2 _wv;
        public WebView2Raw(Microsoft.Web.WebView2.Wpf.WebView2 wv) => _wv = wv;

        public Microsoft.Web.WebView2.Wpf.WebView2 Control => _wv;
    }
}
