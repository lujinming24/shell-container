using System;
using System.IO;
using System.Reflection;

namespace Container
{
    public class LogicHost
    {
        private object? _instance;
        private MethodInfo? _handleMethod;
        private Assembly? _asm;

        public bool Load(byte[]? logicBytes)
        {
            if (logicBytes == null || logicBytes.Length == 0)
                return false;

            try
            {
                string tmpDir = Path.Combine(Path.GetTempPath(),
                    "ShellLogic_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tmpDir);
                string dllPath = Path.Combine(tmpDir, "Logic.dll");
                File.WriteAllBytes(dllPath, logicBytes);

                _asm = Assembly.LoadFrom(dllPath);

                var logicType = _asm.GetType("LogicLib.Logic");
                if (logicType == null) return false;

                _instance = Activator.CreateInstance(logicType);
                if (_instance == null) return false;

                _handleMethod = logicType.GetMethod("HandleMessage");
                return _handleMethod != null;
            }
            catch
            {
                return false;
            }
        }

        public string Handle(string message,
                              Action<string> executeJs,
                              Action launchTarget,
                              Action exitApp)
        {
            if (_instance == null || _handleMethod == null || _asm == null)
                return "";

            try
            {
                var ctxType = _asm.GetType("LogicLib.LogicContext");
                if (ctxType == null) return "";

                var ctx = Activator.CreateInstance(ctxType);
                if (ctx == null) return "";

                SetField(ctx, ctxType, "ExecuteJs", executeJs);
                SetField(ctx, ctxType, "LaunchTarget", launchTarget);
                SetField(ctx, ctxType, "ExitApp", exitApp);

                var result = _handleMethod.Invoke(_instance, new object[] { message, ctx });
                return result as string ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static void SetField(object obj, Type type, string name, Delegate value)
        {
            var field = type.GetField(name);
            if (field != null) { field.SetValue(obj, value); return; }

            var prop = type.GetProperty(name);
            if (prop != null && prop.CanWrite) { prop.SetValue(obj, value); }
        }
    }
}
