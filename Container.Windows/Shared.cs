using System;

namespace LogicLib
{
    public interface ILogic
    {
        string HandleMessage(string message, LogicContext ctx);
    }

    public class LogicContext
    {
        public Action<string>? ExecuteJs;
        public Action? LaunchTarget;
        public Action? ExitApp;
    }

    public static class JsHelper
    {
        public static string GetJsonValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string pattern = "\"" + key + "\"";
            int i = json.IndexOf(pattern);
            if (i < 0) return "";
            int colon = json.IndexOf(':', i);
            if (colon < 0) return "";
            int start = json.IndexOf('"', colon);
            if (start < 0) return "";
            int end = json.IndexOf('"', start + 1);
            if (end < 0) return "";
            return json.Substring(start + 1, end - start - 1);
        }

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\r", "")
                    .Replace("\n", "\\n");
        }
    }
}
