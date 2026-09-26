#include "browser.h"
#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>

@interface AppDelegate : NSObject <NSApplicationDelegate, WKScriptMessageHandler>
@property (nonatomic, strong) NSWindow* window;
@property (nonatomic, strong) WKWebView* webView;
@property (nonatomic, assign) std::function<void()>* onLaunch;
@end

@implementation AppDelegate

- (void)applicationDidFinishLaunching:(NSNotification*)note {
    NSRect frame = [[NSScreen mainScreen] frame];
    self.window = [[NSWindow alloc]
        initWithContentRect:frame
        styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskClosable
        backing:NSBackingStoreBuffered
        defer:NO];
    [self.window setTitle:@"启动中"];

    WKWebViewConfiguration* config = [[WKWebViewConfiguration alloc] init];
    [config.userContentController addScriptMessageHandler:self name:@"app"];

    self.webView = [[WKWebView alloc] initWithFrame:frame configuration:config];
    [self.window setContentView:self.webView];

    [self.window makeKeyAndOrderFront:nil];
    [NSApp activateIgnoringOtherApps:YES];
}

- (void)userContentController:(WKUserContentController*)uc
      didReceiveScriptMessage:(WKScriptMessage*)msg {
    if ([msg.name isEqualToString:@"app"] && self.onLaunch) {
        (*self.onLaunch)();
    }
}

- (BOOL)applicationShouldTerminateAfterLastWindowClosed:(NSApplication*)app {
    return YES;
}
@end

static AppDelegate* g_delegate = nil;

void ShowBrowser(const std::string& html, std::function<void()> onLaunch) {
    @autoreleasepool {
        [NSApplication sharedApplication];
        [NSApp setActivationPolicy:NSApplicationActivationPolicyRegular];

        g_delegate = [[AppDelegate alloc] init];
        g_delegate.onLaunch = new std::function<void()>(onLaunch);
        [NSApp setDelegate:g_delegate];

        NSString* htmlStr = [NSString stringWithUTF8String:html.c_str()];
        [g_delegate.webView loadHTMLString:htmlStr baseURL:nil];

        [NSApp run];
    }
}

void RunMessageLoop() {
}
