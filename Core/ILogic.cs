using System;

namespace LogicLib
{
    public interface ILogic
    {
        /// <summary>处理 HTML 发来的消息，返回要执行的 JS 字符串。</summary>
        string HandleMessage(string message, LogicContext ctx);
    }
}
