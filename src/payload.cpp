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

    {
        std::ofstream log("C:\\container_log.txt", std::ios::app);
        log << "[payload] self 路径: " << self << std::endl;
        log << "[payload] 文件是否存在: " << (std::ifstream(self).good() ? "yes" : "no") << std::endl;
    }

    if (self.empty()) return false;

    std::ifstream file(self, std::ios::binary | std::ios::ate);
    if (!file) return false;

    std::streamsize total = file.tellg();
    {
        std::ofstream log("C:\\container_log.txt", std::ios::app);
        log << "[payload] 文件总大小: " << total << std::endl;
    }

    if (total < MAGIC_LEN + 16) return false;

    std::streamsize scanSize = (std::min)((std::streamsize)65536, total);
    std::vector<char> tail((size_t)scanSize);
    file.seekg(total - scanSize);
    file.read(tail.data(), scanSize);

    int magicPos = -1;
    for (int i = (int)scanSize - MAGIC_LEN - 1; i >= 0; i--) {
        if (memcmp(tail.data() + i, MAGIC, MAGIC_LEN) == 0) {
            int hp = i + MAGIC_LEN;
            if (hp + 16 > scanSize) continue;

            int64_t tLen, hLen;
            memcpy(&tLen, tail.data() + hp, 8);
            memcpy(&hLen, tail.data() + hp + 8, 8);

            if (tLen <= 0 || hLen < 0) continue;

            int64_t expectEnd = (total - scanSize) + hp + 16 + tLen + hLen;
            if (expectEnd != total) continue;

            magicPos = i;
            break;
        }
    }

    {
        std::ofstream log("C:\\container_log.txt", std::ios::app);
        log << "[payload] magicPos: " << magicPos << " (scanSize=" << scanSize << ")" << std::endl;
        log << "[payload] 尾部前 20 字节: ";
        for (int i = 0; i < 20 && i < scanSize; i++) {
            char buf[8];
            snprintf(buf, sizeof(buf), "%02X ", (unsigned char)tail[i]);
            log << buf;
        }
        log << std::endl;
    }

    if (magicPos < 0) return false;

    int hp = magicPos + MAGIC_LEN;
    int64_t tLen, hLen;
    memcpy(&tLen, tail.data() + hp, 8);
    memcpy(&hLen, tail.data() + hp + 8, 8);
    int64_t dataStart = hp + 16;

    {
        std::ofstream log("C:\\container_log.txt", std::ios::app);
        log << "[payload] tLen: " << tLen << ", hLen: " << hLen << std::endl;
    }

    out.target.resize((size_t)tLen);
    if (dataStart + tLen <= scanSize) {
        memcpy(out.target.data(), tail.data() + dataStart, (size_t)tLen);
    } else {
        file.seekg(total - scanSize + dataStart);
        file.read((char*)out.target.data(), tLen);
    }

    out.html.resize((size_t)hLen);
    int64_t htmlOffset = dataStart + tLen;
    if (htmlOffset + hLen <= scanSize) {
        memcpy(&out.html[0], tail.data() + htmlOffset, (size_t)hLen);
    } else {
        file.seekg(total - scanSize + htmlOffset);
        file.read(&out.html[0], hLen);
    }

    return true;
}
