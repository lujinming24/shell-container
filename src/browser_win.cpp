#include "browser.h"
#include <windows.h>
#include <atlbase.h>
#include <exdisp.h>
#include <mshtml.h>
#include <string>

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

    CComPtr<IOleObject> ole;
    browser->QueryInterface(IID_IOleObject, (void**)&ole);
    ole->DoVerb(OLEIVERB_SHOW, NULL, NULL, 0, g_hwnd, &rc);

    CComPtr<IDispatch> disp;
    browser->get_Document(&disp);
    if (disp) {
        CComPtr<IHTMLDocument2> doc;
        disp->QueryInterface(IID_IHTMLDocument2, (void**)&doc);
        if (doc) doc->put_script(&g_external);
    }

    g_browser = browser;

    std::wstring htmlW(html.begin(), html.end());
    BSTR bstr = SysAllocString(htmlW.c_str());

    VARIANT vEmpty; VariantInit(&vEmpty);
    browser->Navigate2(&CComVariant(L"about:blank"), &vEmpty, &vEmpty, &vEmpty, &vEmpty);
    Sleep(200);

    browser->get_Document(&disp);
    if (disp) {
        CComPtr<IHTMLDocument2> doc;
        disp->QueryInterface(IID_IHTMLDocument2, (void**)&doc);
        if (doc) {
            CComPtr<IHTMLWindow2> win;
            doc->get_parentWindow(&win);
            if (win) {
                SAFEARRAY* sa = SafeArrayCreateVector(VT_VARIANT, 0, 1);
                VARIANT* pv;
                SafeArrayAccessData(sa, (void**)&pv);
                pv->vt = VT_BSTR;
                pv->bstrVal = bstr;
                SafeArrayUnaccessData(sa);
                win->execScript(L"document.write", L"JavaScript", sa);
                SafeArrayDestroy(sa);
            }
        }
    }
    SysFreeString(bstr);
}

void RunMessageLoop() {
    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
}
