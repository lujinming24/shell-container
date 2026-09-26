cmake_minimum_required(VERSION 3.15)
project(ShellContainer CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(COMMON_SOURCES
    src/main.cpp
    src/payload.cpp
    src/launcher.cpp
)

if(WIN32)
    list(APPEND COMMON_SOURCES src/browser_win.cpp)
    add_executable(container WIN32 ${COMMON_SOURCES})

    set_target_properties(container PROPERTIES
        LINK_FLAGS "/ENTRY:wWinMainCRTStartup")

    set_property(TARGET container PROPERTY
        MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

    target_link_libraries(container PRIVATE
        ole32 oleaut32 shlwapi comctl32 user32 gdi32)

    target_compile_definitions(container PRIVATE
        UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)

elseif(APPLE)
    list(APPEND COMMON_SOURCES src/browser_mac.mm)
    add_executable(container MACOSX_BUNDLE ${COMMON_SOURCES})

    target_link_libraries(container PRIVATE
        "-framework Cocoa"
        "-framework WebKit"
        "-framework Foundation")

    set_source_files_properties(src/browser_mac.mm PROPERTIES
        COMPILE_FLAGS "-fobjc-arc")

elseif(UNIX)
    list(APPEND COMMON_SOURCES src/browser_linux.cpp)

    find_package(PkgConfig REQUIRED)
    pkg_check_modules(GTK REQUIRED gtk+-3.0)
    pkg_check_modules(WEBKIT REQUIRED webkit2gtk-4.1)

    add_executable(container ${COMMON_SOURCES})

    target_include_directories(container PRIVATE
        ${GTK_INCLUDE_DIRS} ${WEBKIT_INCLUDE_DIRS})
    target_link_libraries(container PRIVATE
        ${GTK_LIBRARIES} ${WEBKIT_LIBRARIES})
    target_compile_options(container PRIVATE
        ${GTK_CFLAGS_OTHER} ${WEBKIT_CFLAGS_OTHER})
endif()
