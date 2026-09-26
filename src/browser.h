#pragma once
#include <string>
#include <functional>

void ShowBrowser(const std::string& html, std::function<void()> onLaunch);
void RunMessageLoop();
