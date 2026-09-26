#include "launcher.h"
#include <fstream>
#include <string>
#include <cstdlib>
#include <ctime>

#ifdef _WIN32
#include <windows.h>
#include <shellapi.h>
#else
#include <unistd.h>
#include <sys/stat.h>
#include <sys/wait.h>
#endif

static std::string MakeTempDir() {
    char buf[512];
#ifdef _WIN32
    GetTempPathA(512, buf);
    std::string base(buf);
    std::string dir = base + "ShellApp_" + std::to_string((long)time(nullptr))
                    + "_" + std::to_string(rand());
    CreateDirectoryA(dir.c_str(), NULL);
    return dir;
#else
    const char* tmp = getenv("TMPDIR");
    if (!tmp) tmp = "/tmp/";
    std::string base(tmp);
    if (!base.empty() && base.back() != '/') base += '/';
    std::string dir = base + "ShellApp_" + std::to_string((long)time(nullptr))
                    + "_" + std::to_string(rand());
    mkdir(dir.c_str(), 0755);
    return dir;
#endif
}

void LaunchTarget(const std::vector<uint8_t>& targetBytes) {
    if (targetBytes.empty()) return;

    std::string dir = MakeTempDir();

#ifdef _WIN32
    std::string exePath = dir + "\\target.exe";
    std::ofstream out(exePath, std::ios::binary);
    out.write((const char*)targetBytes.data(), targetBytes.size());
    out.close();
    ShellExecuteA(NULL, "open", exePath.c_str(), NULL, dir.c_str(), SW_SHOWNORMAL);
#else
    std::string exePath = dir + "/target";
    std::ofstream out(exePath, std::ios::binary);
    out.write((const char*)targetBytes.data(), targetBytes.size());
    out.close();
    chmod(exePath.c_str(), 0755);

    pid_t pid = fork();
    if (pid == 0) {
        chdir(dir.c_str());
        execl(exePath.c_str(), exePath.c_str(), (char*)NULL);
        _exit(1);
    }
#endif
}
