using System;

namespace ShellContainer
{
    /// <summary>
    /// 平台 WebView 宿主抽象。
    /// 三平台各实现一份。
    /// </summary>
    public interface IShellHost
    {
        /// <summary>创建窗口（不显示）。</summary>
        void CreateWindow(int width, int height, string title);

        /// <summary>后台初始化 WebView（不阻塞）。</summary>
        void InitWebView();

        /// <summary>加载 HTML（此时窗口仍未显示）。</summary>
        void LoadHtml(string html);

        /// <summary>显示窗口（WebView 渲染完成后调用）。</summary>
        void ShowWindow();

        /// <summary>关闭窗口。</summary>
        void CloseWindow();

        /// <summary>JS → C# 消息。</summary>
        event Action<string> MessageReceived;

        /// <summary>C# → JS 消息（实际执行 JS）。</summary>
        void SendMessage(string js);

        /// <summary>进入消息循环（阻塞）。</summary>
        void Run();

        /// <summary>WebView 渲染完成事件（内部用）。</summary>
        event Action RenderCompleted;
    }
}
