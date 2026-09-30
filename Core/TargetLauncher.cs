using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ShellContainer
{
    /// <summary>
    /// 释放目标程序（单文件或整目录），启动。
    /// 注意：此方法只在校验通过后调用，授权页启动阶段不碰目标数据。
    /// </summary>
    public sealed class TargetLauncher
    {
        private readonly Payload _payload;

        public TargetLauncher(Payload payload) => _payload = payload;

        public void Launch()
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(),
                    "shellpack_target_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                string exePath;
                if (_payload.IsDirectory)
                {
                    using var ms = new MemoryStream(_payload.Target);
                    using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                    zip.ExtractToDirectory(tempDir, overwriteFiles: true);
                    exePath = Path.Combine(tempDir, _payload.EntryName);
                }
                else
                {
                    string name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? "target.exe" : "target";
                    exePath = Path.Combine(tempDir, name);
                    File.WriteAllBytes(exePath, _payload.Target);
                }

                // Unix：加可执行权限
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        File.SetUnixFileMode(exePath,
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    }
                    catch { }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = tempDir,
                    UseShellExecute = false,
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("启动目标程序失败: " + ex.Message);
            }
        }
    }
}
