#include "browser.h"
#include <windows.h>
#include <atlbase.h>
#include <exdisp.h>
#include <mshtml.h>
#include <string>
#include <fstream>

static void Log(const std::string& msg) {
    std::ofstream log("C:\\container_log.txt", std::ios::app);
    log << "[browser_win] " << msg << std::endl;
}

static CComPtr<IWebBrowser2> g_browser;
static HWND g_hwnd = NULL;
static std::function<void()> g_onLaunch;

class __declspec(uuid("12345678-1234-1234-1234-123456789012"))
    ExternalDispatch : public IDispatch {
public:
    STDMETHOD(QueryInterface)(REFIID riid, void** ppv) {
        if (riid == IID_IUnknown || riid == IID_IDispatch) { *ppv = this; AddRef(); return S_OK; }
        *ppv = NULL; return E_NOINTERFACE;
    }
    STDMETHOD_(ULONG, AddRef)() { return 1; }
    STDMETHOD_(ULONG, Release)() { return 1; }
    STDMETHOD(GetTypeInfoCount)(UINT* p) { *p = 0; return S_OK; }
    STDMETHOD(GetTypeInfo)(UINT, LCID, ITypeInfo**) { return E_NOTIMPL; }
    STDMETHOD(GetIDsOfNames)(REFIID, LPOLESTR* names, UINT c, LCID, DISPID* ids) {
        if (c != 1) return E_INVALIDARG;
        if (_wcsicmp(names[0], L"Launch") == 0) { *ids = 1; return S_OK; }
        return DISP_E_UNKNOWNNAME;
    }
    STDMETHOD(Invoke)(DISPID id, REFIID, LCID, WORD, DISPPARAMS*,
                      VARIANT*, EXCEPINFO*, UINT*) {
        if (id == 1 && g_onLaunch) { g_onLaunch(); return S_OK; }
        return DISP_E_MEMBERNOTFOUND;
    }
};

static ExternalDispatch g_external;

static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM w, LPARAM l) {
    switch (msg) {
    case WM_SIZE:
        if (g_browser) {
            RECT rc; GetClientRect(hwnd, &rc);
            g_browser->put_Width(rc.right - rc.left);
            g_browser->put_Height(rc.bottom - rc.top);
        }
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    }
    return DefWindowProc(hwnd, msg, w, l);
}

void ShowBrowser(const std::string& html, std::function<void()> onLaunch) {
    Log("ShowBrowser 开始");
    g_onLaunch = onLaunch;

    CoInitialize(NULL);
    Log("CoInitialize 完成");

    HINSTANCE hInst = GetModuleHandle(NULL);
    WNDCLASSW wc = {};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInst;
    wc.lpszClassName = L"ShellContainerWindow";
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)GetStockObject(WHITE_BRUSH);
    RegisterClassW(&wc);
    Log("RegisterClass 完成");

    g_hwnd = CreateWindowExW(0, L"ShellContainerWindow", L"启动中",
        WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 900, 600,
        NULL, NULL, hInst, NULL);
    Log("CreateWindow 完成");

    ShowWindow(g_hwnd, SW_SHOWMAXIMIZED);
    UpdateWindow(g_hwnd);
    Log("ShowWindow 完成");

    CComPtr<IWebBrowser2> browser;
    HRESULT hr = CoCreateInstance(CLSID_WebBrowser, NULL, CLSCTX_INPROC_SERVER,
                     IID_IWebBrowser2, (void**)&browser);
    Log("CoCreateInstance hr=" + std::to_string(hr));
    if (FAILED(hr)) {
        Log("CoCreateInstance 失败");
        return;
    }

    RECT rc; GetClientRect(g_hwnd, &rc);
    browser->put_Width(rc.right - rc.left);
    browser->put_Height(rc.bottom - rc.top);

    CComPtr<IOleObject> ole;
    browser->QueryInterface(IID_IOleObject, (void**)&ole);
    ole->DoVerb(OLEIVERB_SHOW, NULL, NULL, 0, g_hwnd, &rc);
    Log("DoVerb 完成");

    // 注册 window.external
    CComPtr<IDispatch> disp;
    browser->get_Document(&disp);
    if (disp) {
        CComPtr<IHTMLDocument2> doc;
        if (SUCCEEDED(disp->QueryInterface(IID_IHTMLDocument2, (void**)&doc)) && doc) {
            LPOLESTR name = (LPOLESTR)L"Script";
            DISPID dispid;
            if (SUCCEEDED(doc->GetIDsOfNames(IID_NULL, &name, 1, LOCALE_USER_DEFAULT, &dispid))) {
                VARIANT arg;
                VariantInit(&arg);
                arg.vt = VT_DISPATCH;
                arg.pdispVal = &g_external;
                DISPPARAMS params = { &arg, NULL, 1, 0 };
                doc->Invoke(dispid, IID_NULL, LOCALE_USER_DEFAULT,
                            DISPATCH_PROPERTYPUT, &params, NULL, NULL, NULL);
                Log("window.external 注册完成");
            } else {
                Log("GetIDsOfNames 失败");
            }
        }
    } else {
        Log("get_Document 返回空");
    }

    // 写 HTML 到临时文件
    wchar_t tmpPath[MAX_PATH];
    GetTempPathW(MAX_PATH, tmpPath);
    std::wstring htmlFile = std::wstring(tmpPath) + L"shell_html_"
        + std::to_wstring(GetTickCount()) + L".html";
    {
        std::ofstream out(htmlFile, std::ios::binary);
        out.write(html.data(), html.size());
    }
    Log("HTML 已写临时文件");

    std::wstring fileUrl = L"file:///" + htmlFile;
    for (auto& ch : fileUrl) if (ch == L'\\') ch = L'/';

    VARIANT vEmpty; VariantInit(&vEmpty);
    browser->Navigate2(&CComVariant(fileUrl.c_str()), &vEmpty, &vEmpty, &vEmpty, &vEmpty);
    Log("Navigate2 完成");

    g_browser = browser;
    Log("ShowBrowser 结束");
}

void RunMessageLoop() {
    Log("RunMessageLoop 进入");
    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    Log("RunMessageLoop 退出");
}
