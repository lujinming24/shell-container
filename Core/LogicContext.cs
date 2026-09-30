using System;

namespace LogicLib
{
    public sealed class LogicContext
    {
        /// <summary>向 HTML 界面发送并执行一段 JavaScript。</summary>
        public Action<string> ExecuteJs { get; set; } = _ => { };

        /// <summary>启动内嵌的目标程序。</summary>
        public Action LaunchTarget { get; set; } = () => { };

        /// <summary>退出壳程序。</summary>
        public Action ExitApp { get; set; } = () => { };
    }
}
