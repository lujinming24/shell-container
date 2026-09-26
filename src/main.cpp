#include "payload.h"
#include "launcher.h"
#include "browser.h"
#include <cstdio>
#include <cstdlib>
#include <fstream>

#ifdef _WIN32
#include <windows.h>
#endif

static void Log(const std::string& msg) {
#ifdef _WIN32
    char path[MAX_PATH];
    GetEnvironmentVariableA("USERPROFILE", path, MAX_PATH);
    std::string logPath = std::string(path) + "\\container_log.txt";
    std::ofstream log(logPath, std::ios::app);
    log << msg << std::endl;
#endif
}

static int RunApp() {
    Log("=== RunApp 开始 ===");

    Payload payload;
    bool ok = ReadSelfPayload(payload);
    Log(std::string("ReadSelfPayload 返回: ") + (ok ? "true" : "false"));

    if (!ok) {
        Log("无壳数据，退出");
        return 1;
    }

    Log("target 长度: " + std::to_string(payload.target.size()));
    Log("html 长度: " + std::to_string(payload.html.size()));

    Log("准备调用 ShowBrowser");
    ShowBrowser(payload.html, [&payload]() {
        Log("Launch 被调用");
        LaunchTarget(payload.target);
        exit(0);
    });
    Log("ShowBrowser 返回");

    Log("准备进入消息循环");
    RunMessageLoop();
    Log("RunMessageLoop 返回");
    return 0;
}

#ifdef _WIN32
int WINAPI wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int) {
    return RunApp();
}
#else
int main(int argc, char** argv) {
    return RunApp();
}
#endif
