#include "payload.h"
#include "launcher.h"
#include "browser.h"
#include <cstdio>
#include <cstdlib>

#ifdef _WIN32
#include <windows.h>
#endif

static int RunApp() {
    Payload payload;
    if (!ReadSelfPayload(payload)) {
        fprintf(stderr, "无壳数据\n");
        return 1;
    }

    ShowBrowser(payload.html, [&payload]() {
        LaunchTarget(payload.target);
        exit(0);
    });

    RunMessageLoop();
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
