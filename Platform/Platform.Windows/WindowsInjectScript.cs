namespace ShellContainer
{
    internal static class WindowsInjectScript
    {
        public static string Build()
        {
            return @"
<script>
function send(action, data) {
    var obj = { action: action };
    if (data) for (var k in data) obj[k] = data[k];
    window.chrome.webview.postMessage(JSON.stringify(obj));
}
window.chrome.webview.addEventListener('message', function(e) {
    try { eval(e.data); } catch (err) { console.error(err); }
});
</script>";
        }
    }
}
