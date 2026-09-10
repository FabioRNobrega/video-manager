using WebApp.Services;

namespace WebApp.Tests.Services;

public sealed class EpubContentSanitizerTests
{
    private readonly EpubContentSanitizer _sanitizer = new();

    [Fact]
    public void Preserves_safe_semantic_formatting()
    {
        const string html =
            "<html><body><h1>Title</h1><p>Hello <em>world</em> and <strong>friends</strong>.</p>" +
            "<blockquote>A quote</blockquote><ul><li>One</li><li>Two</li></ul></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.Contains("<h1>Title</h1>", sanitized);
        Assert.Contains("<em>world</em>", sanitized);
        Assert.Contains("<strong>friends</strong>", sanitized);
        Assert.Contains("<blockquote>A quote</blockquote>", sanitized);
        Assert.Contains("<li>One</li>", sanitized);
        Assert.Contains("<li>Two</li>", sanitized);
    }

    [Fact]
    public void Strips_script_tags_and_their_content()
    {
        const string html = "<html><body><p>Safe</p><script>alert('xss')</script></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.Contains("Safe", sanitized);
        Assert.DoesNotContain("<script", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", sanitized);
    }

    [Fact]
    public void Strips_event_handler_attributes_but_keeps_the_element()
    {
        const string html = "<html><body><p onclick=\"doEvil()\">Click me</p></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.Contains("Click me", sanitized);
        Assert.DoesNotContain("onclick", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doEvil", sanitized);
    }

    [Fact]
    public void Strips_iframes_and_forms_entirely()
    {
        const string html =
            "<html><body><p>Text</p><iframe src=\"https://evil.example/\"></iframe>" +
            "<form action=\"https://evil.example/collect\"><input type=\"text\" /></form></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.Contains("Text", sanitized);
        Assert.DoesNotContain("<iframe", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", sanitized);
    }

    [Fact]
    public void Strips_images_and_remote_resource_urls()
    {
        const string html =
            "<html><body><img src=\"https://example.com/tracker.png\" />" +
            "<p style=\"background-image:url(https://example.com/x.png)\">Body</p></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.DoesNotContain("<img", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", sanitized);
        Assert.DoesNotContain("style=", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Strips_publisher_css_style_and_class_attributes()
    {
        const string html =
            "<html><head><style>body { color: red; }</style></head>" +
            "<body class=\"publisher-theme\"><p style=\"color:red\" class=\"fancy\">Text</p></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.DoesNotContain("<style", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class=", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Text", sanitized);
    }

    [Fact]
    public void Keeps_only_same_document_anchor_links()
    {
        const string html =
            "<html><body><a href=\"#section2\">Jump</a> and " +
            "<a href=\"https://example.com/evil\">External</a></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.Contains("href=\"#section2\"", sanitized);
        Assert.Contains(">Jump</a>", sanitized);
        Assert.Contains(">External</a>", sanitized);
        Assert.DoesNotContain("example.com", sanitized);
    }

    [Fact]
    public void Unwraps_unknown_or_unsafe_wrapper_elements_while_keeping_text()
    {
        const string html = "<html><body><section><p>Kept text</p></section></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.DoesNotContain("<section", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kept text", sanitized);
    }

    [Fact]
    public void Recovers_body_content_when_head_has_a_self_closed_title()
    {
        const string html =
            "<html><head><title/><link href=\"css/style.css\" rel=\"stylesheet\"/></head>" +
            "<body><p>Chapter text</p></body></html>";

        var sanitized = _sanitizer.Sanitize(html);

        Assert.Contains("Chapter text", sanitized);
    }

    [Fact]
    public void Returns_empty_string_for_null_or_whitespace_input()
    {
        Assert.Equal(string.Empty, _sanitizer.Sanitize(string.Empty));
        Assert.Equal(string.Empty, _sanitizer.Sanitize("   "));
    }
}
