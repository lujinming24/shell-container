using System;
using System.IO;
using System.Reflection;
using LogicLib;

namespace ShellContainer
{
    /// <summary>从内存字节加载 Logic.dll，反射实例化 LogicLib.Logic。</summary>
    public static class LogicBridge
    {
        public static ILogic Load(byte[] logicBytes)
        {
            if (logicBytes == null || logicBytes.Length == 0)
                throw new InvalidOperationException("Logic.dll 数据为空");

            // ★ 内存加载，不落地磁盘
            var asm = Assembly.Load(logicBytes);

            var type = asm.GetType("LogicLib.Logic")
                ?? throw new InvalidOperationException("Logic.dll 中找不到类型 LogicLib.Logic");

            var instance = Activator.CreateInstance(type) as ILogic
                ?? throw new InvalidOperationException("LogicLib.Logic 未实现 ILogic 接口");

            return instance;
        }
    }
}
