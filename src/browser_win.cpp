#include "browser.h"
#include <windows.h>
#include <atlbase.h>
#include <exdisp.h>
#include <mshtml.h>
#include <string>
#include <fstream>

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
    g_onLaunch = onLaunch;
    CoInitialize(NULL);

    HINSTANCE hInst = GetModuleHandle(NULL);
    WNDCLASSW wc = {};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInst;
    wc.lpszClassName = L"ShellContainerWindow";
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)GetStockObject(WHITE_BRUSH);
    RegisterClassW(&wc);

    g_hwnd = CreateWindowExW(0, L"ShellContainerWindow", L"启动中",
        WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 900, 600,
        NULL, NULL, hInst, NULL);

    ShowWindow(g_hwnd, SW_SHOWMAXIMIZED);
    UpdateWindow(g_hwnd);

    CComPtr<IWebBrowser2> browser;
    CoCreateInstance(CLSID_WebBrowser, NULL, CLSCTX_INPROC_SERVER,
                     IID_IWebBrowser2, (void**)&browser);

    RECT rc; GetClientRect(g_hwnd, &rc);
    browser->put_Width(rc.right - rc.left);
    browser->put_Height(rc.bottom - rc.top);

    // 把 web browser 嵌入窗口
    CComPtr<IOleObject> ole;
    browser->QueryInterface(IID_IOleObject, (void**)&ole);
    ole->DoVerb(OLEIVERB_SHOW, NULL, NULL, 0, g_hwnd, &rc);

    // 注册 window.external
    CComPtr<IDispatch> disp;
    browser->get_Document(&disp);
    if (disp) {
        CComPtr<IHTMLDocument2> doc;
        if (SUCCEEDED(disp->QueryInterface(IID_IHTMLDocument2, (void**)&doc)) && doc) {
            // IHTMLDocument2 继承自 IDispatch，put_script 需要通过 IDispatch 调用
            // 直接写 HTML 到 document 最简单的方式是用 IPersistStreamInit
            // 但我们用更简单的方式：把 HTML 存文件再导航过去
        }
    }

    // 把 HTML 写到临时文件
    wchar_t tmpPath[MAX_PATH];
    GetTempPathW(MAX_PATH, tmpPath);
    std::wstring htmlFile = std::wstring(tmpPath) + L"shell_html_"
        + std::to_wstring(GetTickCount()) + L".html";

    {
        std::ofstream out(htmlFile, std::ios::binary);
        out.write(html.data(), html.size());
    }

    // 导航到该 HTML 文件
    std::wstring fileUrl = L"file:///" + htmlFile;
    for (auto& ch : fileUrl) if (ch == L'\\') ch = L'/';

    VARIANT vEmpty; VariantInit(&vEmpty);
    browser->Navigate2(&CComVariant(fileUrl.c_str()), &vEmpty, &vEmpty, &vEmpty, &vEmpty);

    g_browser = browser;

    // 导航完成后注册 window.external
    // 用计时器轮询检查 document 是否就绪
    // 由于没有消息循环，这里简单 Sleep + 尝试
    Sleep(500);

    browser->get_Document(&disp);
    if (disp) {
        CComPtr<IHTMLDocument2> doc;
        if (SUCCEEDED(disp->QueryInterface(IID_IHTMLDocument2, (void**)&doc)) && doc) {
            // put_script 在 IHTMLDocument2 上实际不可用（ATL 未导出），
            // 用 IDispatch 的 GetIDsOfNames + Invoke 调用
            // 简化方案：直接通过 IDispatch 接口调用 put_script
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
            }
        }
    }
}

void RunMessageLoop() {
    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
}
