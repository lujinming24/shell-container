#include "browser.h"
#include <gtk/gtk.h>
#include <webkit2/webkit2.h>

static std::function<void()>* g_onLaunch = nullptr;

static void OnScriptMessage(WebKitUserContentManager* mgr,
                            WebKitJavascriptResult* res,
                            gpointer) {
    if (g_onLaunch) (*g_onLaunch)();
}

static void OnDestroy(GtkWidget*, gpointer) {
    gtk_main_quit();
}

void ShowBrowser(const std::string& html, std::function<void()> onLaunch) {
    g_onLaunch = new std::function<void()>(onLaunch);

    gtk_init(nullptr, nullptr);

    GtkWidget* win = gtk_window_new(GTK_WINDOW_TOPLEVEL);
    gtk_window_set_title(GTK_WINDOW(win), "启动中");
    gtk_window_maximize(GTK_WINDOW(win));
    g_signal_connect(win, "destroy", G_CALLBACK(OnDestroy), nullptr);

    WebKitUserContentManager* mgr = webkit_user_content_manager_new();
    g_signal_connect(mgr, "script-message-received::app",
                     G_CALLBACK(OnScriptMessage), nullptr);
    webkit_user_content_manager_register_script_message_handler(mgr, "app");

    GtkWidget* web = webkit_web_view_new_with_user_content_manager(mgr);
    gtk_container_add(GTK_CONTAINER(win), web);
    gtk_widget_show_all(win);

    webkit_web_view_load_html(WEBKIT_WEB_VIEW(web), html.c_str(), nullptr);

    gtk_main();
}

void RunMessageLoop() {
}
