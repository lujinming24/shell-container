#pragma once
#include <vector>
#include <string>
#include <cstdint>

struct Payload {
    std::vector<uint8_t> target;
    std::string html;
};

bool ReadSelfPayload(Payload& out);
std::string GetSelfPath();
