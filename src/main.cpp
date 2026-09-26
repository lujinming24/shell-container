#include "payload.h"
#include "launcher.h"
#include "browser.h"
#include <cstdio>
#include <cstdlib>

int main(int argc, char** argv) {
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
