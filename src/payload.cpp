#include "payload.h"
#include <fstream>
#include <cstring>
#include <algorithm>

#ifdef _WIN32
#include <windows.h>
#elif defined(__APPLE__)
#include <mach-o/dyld.h>
#include <limits.h>
#include <unistd.h>
#else
#include <unistd.h>
#include <limits.h>
#endif

static const char MAGIC[] = "SHELLPACK_V1";
static const int MAGIC_LEN = 12;

std::string GetSelfPath() {
#ifdef _WIN32
    char buf[MAX_PATH];
    GetModuleFileNameA(NULL, buf, MAX_PATH);
    return std::string(buf);
#elif defined(__APPLE__)
    char buf[PATH_MAX];
    uint32_t size = sizeof(buf);
    if (_NSGetExecutablePath(buf, &size) != 0) return "";
    return std::string(buf);
#else
    char buf[PATH_MAX];
    ssize_t n = readlink("/proc/self/exe", buf, sizeof(buf) - 1);
    if (n <= 0) return "";
    buf[n] = 0;
    return std::string(buf);
#endif
}

bool ReadSelfPayload(Payload& out) {
    std::string self = GetSelfPath();
    if (self.empty()) return false;

    std::ifstream file(self, std::ios::binary | std::ios::ate);
    if (!file) return false;

    std::streamsize total = file.tellg();
    // 末尾至少有：MAGIC(12) + tLen(8) + hLen(8) = 28 字节
    if (total < 28) return false;

    // 读最后 28 字节
    file.seekg(total - 28);
    char tail[28];
    file.read(tail, 28);

    // 校验 MAGIC
    if (memcmp(tail, MAGIC, 12) != 0) return false;

    int64_t tLen, hLen;
    memcpy(&tLen, tail + 12, 8);
    memcpy(&hLen, tail + 20, 8);

    if (tLen <= 0 || hLen < 0) return false;
    if (28 + tLen + hLen > total) return false;

    int64_t dataStart = total - 28 - tLen - hLen;

    // 读目标 exe
    out.target.resize((size_t)tLen);
    file.seekg(dataStart);
    file.read((char*)out.target.data(), tLen);

    // 读 HTML
    out.html.resize((size_t)hLen);
    file.seekg(dataStart + tLen);
    file.read(&out.html[0], hLen);

    return true;
}
