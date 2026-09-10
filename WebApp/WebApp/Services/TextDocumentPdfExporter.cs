using HtmlAgilityPack;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace WebApp.Services;

internal interface ITextDocumentPdfExporter
{
    byte[] Export(string documentTitle, string previewHtml);
}

internal sealed class TextDocumentPdfExporter : ITextDocumentPdfExporter
{
    private static readonly string[] BlockTags =
    {
        "p", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "table",
        "blockquote", "pre", "hr", "dl"
    };

    public byte[] Export(string documentTitle, string previewHtml)
    {
        var document = new HtmlDocument();
        document.LoadHtml(previewHtml);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily(Fonts.Calibri));

                page.Header()
                    .Text(documentTitle)
                    .FontSize(16)
                    .Bold();

                page.Content()
                    .PaddingTop(12)
                    .Column(column =>
                    {
                        column.Spacing(6);
                        RenderChildren(column, document.DocumentNode);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
            });
        }).GeneratePdf();
    }

    private static void RenderChildren(ColumnDescriptor column, HtmlNode parent)
    {
        foreach (var node in parent.ChildNodes)
        {
            RenderBlock(column, node);
        }
    }

    private static void RenderBlock(ColumnDescriptor column, HtmlNode node)
    {
        if (node.NodeType != HtmlNodeType.Element)
        {
            var text = node.InnerText?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                column.Item().Text(text);
            }

            return;
        }

        switch (node.Name.ToLowerInvariant())
        {
            case "h1":
                column.Item().PaddingTop(6).Text(t => AppendInline(t, node, 20, bold: true));
                break;
            case "h2":
                column.Item().PaddingTop(5).Text(t => AppendInline(t, node, 17, bold: true));
                break;
            case "h3":
                column.Item().PaddingTop(4).Text(t => AppendInline(t, node, 15, bold: true));
                break;
            case "h4":
            case "h5":
            case "h6":
                column.Item().PaddingTop(3).Text(t => AppendInline(t, node, 13, bold: true));
                break;
            case "p":
                column.Item().Text(t => AppendInline(t, node, 11));
                break;
            case "hr":
                column.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                break;
            case "ul":
                RenderList(column, node, ordered: false);
                break;
            case "ol":
                RenderList(column, node, ordered: true);
                break;
            case "dl":
                RenderDefinitionList(column, node);
                break;
            case "blockquote":
                column.Item().BorderLeft(2).BorderColor(Colors.Grey.Lighten1).PaddingLeft(10)
                    .Column(inner =>
                    {
                        inner.Spacing(4);
                        RenderChildren(inner, node);
                    });
                break;
            case "pre":
                RenderCodeBlock(column, node);
                break;
            case "table":
                RenderTable(column, node);
                break;
            default:
                RenderChildren(column, node);
                break;
        }
    }

    private static void RenderList(ColumnDescriptor column, HtmlNode listNode, bool ordered)
    {
        var index = 1;
        column.Item().PaddingLeft(4).Column(listColumn =>
        {
            listColumn.Spacing(3);
            foreach (var item in listNode.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element && n.Name.Equals("li", StringComparison.OrdinalIgnoreCase)))
            {
                var marker = ordered ? $"{index}." : "•";
                index++;

                listColumn.Item().Row(row =>
                {
                    row.ConstantItem(16).Text(marker);
                    row.RelativeItem().Column(itemColumn =>
                    {
                        itemColumn.Spacing(3);

                        var checkbox = item.SelectSingleNode("./input[@type='checkbox']");
                        if (checkbox is not null)
                        {
                            var isChecked = checkbox.Attributes.Contains("checked");
                            itemColumn.Item().Row(checkRow =>
                            {
                                checkRow.Spacing(4);
                                checkRow.ConstantItem(11).AlignMiddle().Element(box => RenderCheckboxGlyph(box, isChecked));
                                checkRow.RelativeItem().Text(t => AppendInlineChildren(t, item, 11, bold: false, skipInputs: true));
                            });
                            return;
                        }

                        var nestedLists = item.ChildNodes
                            .Where(n => n.NodeType == HtmlNodeType.Element &&
                                        (n.Name.Equals("ul", StringComparison.OrdinalIgnoreCase) || n.Name.Equals("ol", StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        itemColumn.Item().Text(t => AppendInlineChildren(t, item, 11, bold: false, stopAtBlockChildren: true));

                        foreach (var nested in nestedLists)
                        {
                            RenderBlock(itemColumn, nested);
                        }
                    });
                });
            }
        });
    }

    private static void RenderCheckboxGlyph(IContainer container, bool isChecked)
    {
        container
            .Width(11)
            .Height(11)
            .Border(1)
            .BorderColor(isChecked ? Colors.Blue.Darken2 : Colors.Grey.Darken1)
            .Background(isChecked ? Colors.Blue.Darken2 : Colors.White);
    }

    private static void RenderDefinitionList(ColumnDescriptor column, HtmlNode dlNode)
    {
        column.Item().Column(inner =>
        {
            inner.Spacing(2);
            foreach (var child in dlNode.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element))
            {
                if (child.Name.Equals("dt", StringComparison.OrdinalIgnoreCase))
                {
                    inner.Item().Text(t => AppendInline(t, child, 11, bold: true));
                }
                else if (child.Name.Equals("dd", StringComparison.OrdinalIgnoreCase))
                {
                    inner.Item().PaddingLeft(12).Text(t => AppendInline(t, child, 11));
                }
            }
        });
    }

    private static void RenderCodeBlock(ColumnDescriptor column, HtmlNode preNode)
    {
        var text = HtmlEntity.DeEntitize(preNode.InnerText) ?? string.Empty;
        column.Item()
            .Background(Colors.Grey.Lighten4)
            .Padding(8)
            .Text(text.TrimEnd('\n'))
            .FontFamily(Fonts.Courier)
            .FontSize(9.5f);
    }

    private static void RenderTable(ColumnDescriptor column, HtmlNode tableNode)
    {
        var rows = tableNode.Descendants("tr").ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var columnCount = rows.Max(r => r.Elements("th").Concat(r.Elements("td")).Count());
        if (columnCount == 0)
        {
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (var i = 0; i < columnCount; i++)
                {
                    columns.RelativeColumn();
                }
            });

            foreach (var row in rows)
            {
                var cells = row.Elements("th").Concat(row.Elements("td")).ToList();
                foreach (var cell in cells)
                {
                    var isHeader = cell.Name.Equals("th", StringComparison.OrdinalIgnoreCase);
                    var container = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);

                    if (isHeader)
                    {
                        container.Background(Colors.Grey.Lighten3).Text(t => AppendInline(t, cell, 10.5f, bold: true));
                    }
                    else
                    {
                        container.Text(t => AppendInline(t, cell, 10.5f));
                    }
                }
            }
        });
    }

    private static void AppendInline(TextDescriptor text, HtmlNode node, float fontSize, bool bold = false) =>
        AppendInlineChildren(text, node, fontSize, bold);

    private static void AppendInlineChildren(
        TextDescriptor text, HtmlNode parent, float fontSize, bool bold, bool skipInputs = false, bool stopAtBlockChildren = false)
    {
        foreach (var node in parent.ChildNodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var value = HtmlEntity.DeEntitize(node.InnerText);
                if (!string.IsNullOrEmpty(value))
                {
                    ApplyBold(text.Span(value).FontSize(fontSize), bold);
                }

                continue;
            }

            if (node.NodeType != HtmlNodeType.Element)
            {
                continue;
            }

            var name = node.Name.ToLowerInvariant();

            if (stopAtBlockChildren && BlockTags.Contains(name) && name != "li")
            {
                continue;
            }

            switch (name)
            {
                case "input":
                    if (!skipInputs)
                    {
                        var isChecked = node.Attributes.Contains("checked");
                        text.Span(isChecked ? "[x] " : "[ ] ").FontSize(fontSize).FontFamily(Fonts.Courier);
                    }
                    break;
                case "br":
                    text.Line(string.Empty);
                    break;
                case "strong":
                case "b":
                    AppendInlineChildren(text, node, fontSize, true, skipInputs, stopAtBlockChildren);
                    break;
                case "em":
                case "i":
                    foreach (var run in EnumerateRuns(node))
                    {
                        ApplyBold(text.Span(run).FontSize(fontSize), bold).Italic();
                    }
                    break;
                case "del":
                case "s":
                case "strike":
                    foreach (var run in EnumerateRuns(node))
                    {
                        ApplyBold(text.Span(run).FontSize(fontSize), bold).Strikethrough();
                    }
                    break;
                case "ins":
                case "mark":
                    foreach (var run in EnumerateRuns(node))
                    {
                        ApplyBold(text.Span(run).FontSize(fontSize), bold).Underline();
                    }
                    break;
                case "sub":
                    foreach (var run in EnumerateRuns(node))
                    {
                        ApplyBold(text.Span(run).FontSize(fontSize * 0.75f), bold).Subscript();
                    }
                    break;
                case "sup":
                    foreach (var run in EnumerateRuns(node))
                    {
                        ApplyBold(text.Span(run).FontSize(fontSize * 0.75f), bold).Superscript();
                    }
                    break;
                case "code":
                    foreach (var run in EnumerateRuns(node))
                    {
                        text.Span(run).FontSize(fontSize * 0.95f).FontFamily(Fonts.Courier);
                    }
                    break;
                case "a":
                    foreach (var run in EnumerateRuns(node))
                    {
                        ApplyBold(text.Span(run).FontSize(fontSize), bold).Underline().FontColor(Colors.Blue.Darken1);
                    }
                    break;
                default:
                    AppendInlineChildren(text, node, fontSize, bold, skipInputs, stopAtBlockChildren);
                    break;
            }
        }
    }

    private static TextSpanDescriptor ApplyBold(TextSpanDescriptor span, bool bold) => bold ? span.Bold() : span;

    private static IEnumerable<string> EnumerateRuns(HtmlNode node)
    {
        var value = HtmlEntity.DeEntitize(node.InnerText);
        if (!string.IsNullOrEmpty(value))
        {
            yield return value;
        }
    }
}
