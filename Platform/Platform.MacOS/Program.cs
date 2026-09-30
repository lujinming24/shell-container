using System;
using AppKit;
using Foundation;

namespace ShellContainer
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            // 初始化 NSApplication
            var app = NSApplication.SharedApplication;
            app.SetActivationPolicy(NSApplicationActivationPolicy.Regular);

            var delegateObj = new AppDelegate();
            app.Delegate = delegateObj;

            app.Run();
        }
    }
}
