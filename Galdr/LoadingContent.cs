using System;
using System.Text;
using SharpWebview.Content;

namespace Galdr;

/// <summary>
/// Provides immediate loading content while the main application starts up.
/// </summary>
internal sealed class LoadingContent : IWebviewContent
{
    private readonly string _loadingHtml;

    public LoadingContent(string loadingMessage = "Galdr", string backgroundColor = "#f5f5f5")
    {
        _loadingHtml = CreateLoadingHtml(loadingMessage, backgroundColor);
    }

    public string Html => _loadingHtml;

    public string ToWebviewUrl()
    {
        var webviewUrl = new StringBuilder("data:text/html,");
        webviewUrl.Append(Uri.EscapeDataString(_loadingHtml));

        bool containsLocalhost = webviewUrl.ToString().Contains("localhost");

        return webviewUrl.ToString();
    }

    private static string CreateLoadingHtml(string loadingMessage, string backgroundColor)
    {
        return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <title>{loadingMessage}</title>
                <style>
                    body {{
                        margin: 0;
                        padding: 0;
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                        background-color: {backgroundColor};
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                        flex-direction: column;
                    }}
                    .spinner {{
                        width: 40px;
                        height: 40px;
                        border: 4px solid #f3f3f3;
                        border-top: 4px solid #3498db;
                        border-radius: 50%;
                        animation: spin 1s linear infinite;
                        margin-bottom: 20px;
                    }}
                    @keyframes spin {{
                        0% {{ transform: rotate(0deg); }}
                        100% {{ transform: rotate(360deg); }}
                    }}
                    .loading-text {{
                        color: #666;
                        font-size: 16px;
                    }}
                </style>
            </head>
            <body>
                <div style='display: none'>localhost</div>
                <div class='spinner'></div>
                <div class='loading-text'>{loadingMessage}...</div>
            </body>
            </html>";
    }
}
