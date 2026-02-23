using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LexCore.Infrastructure.Services;

public class PdfService : IPdfService
{
    public PdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateInvoicePdf(Invoice invoice, Firm firm, User client)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, firm, invoice));
                page.Content().Element(c => ComposeContent(c, invoice, client));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, Firm firm, Invoice invoice)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(firm.Name).FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                if (!string.IsNullOrEmpty(firm.Address))
                    column.Item().Text(firm.Address).FontSize(9).FontColor(Colors.Grey.Darken1);
                if (!string.IsNullOrEmpty(firm.GstNumber))
                    column.Item().Text($"GSTIN: {firm.GstNumber}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            row.ConstantItem(150).Column(column =>
            {
                column.Item().AlignRight().Text("TAX INVOICE").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                column.Item().AlignRight().Text($"Invoice #: {invoice.InvoiceNumber}").FontSize(9);
                column.Item().AlignRight().Text($"Date: {invoice.CreatedAt:dd MMM yyyy}").FontSize(9);
                if (invoice.DueDate.HasValue)
                    column.Item().AlignRight().Text($"Due Date: {invoice.DueDate:dd MMM yyyy}").FontSize(9);
            });
        });
    }

    private void ComposeContent(IContainer container, Invoice invoice, User client)
    {
        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(15);

            // Bill To Section
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Bill To:").Bold();
                    col.Item().Text(client.Name);
                    col.Item().Text(client.Email).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });

            // Line Items Table
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").Bold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Amount").Bold();
                });

                // Main item
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                    .Text(invoice.Description ?? "Legal Services");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                    .AlignRight().Text($"₹{invoice.Amount:N2}");

                // Parse and display line items if any
                if (!string.IsNullOrEmpty(invoice.LineItems))
                {
                    var items = invoice.LineItems.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in items)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text("");
                    }
                }
            });

            // Totals
            column.Item().AlignRight().Width(200).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Cell().Padding(3).Text("Subtotal:").AlignRight();
                table.Cell().Padding(3).Text($"₹{invoice.Amount:N2}").AlignRight();

                table.Cell().Padding(3).Text("GST (18%):").AlignRight();
                table.Cell().Padding(3).Text($"₹{invoice.GstAmount:N2}").AlignRight();

                table.Cell().Padding(3).Background(Colors.Blue.Lighten4).Text("Total:").Bold().AlignRight();
                table.Cell().Padding(3).Background(Colors.Blue.Lighten4).Text($"₹{invoice.TotalAmount:N2}").Bold().AlignRight();
            });

            // Status
            column.Item().PaddingTop(20).Row(row =>
            {
                var statusColor = invoice.Status.ToString() switch
                {
                    "Paid" => Colors.Green.Darken1,
                    "Overdue" => Colors.Red.Darken1,
                    _ => Colors.Orange.Darken1
                };

                row.AutoItem().Text($"Status: {invoice.Status}").FontSize(12).Bold().FontColor(statusColor);
            });

            // Notes
            column.Item().PaddingTop(20).Text("Payment Terms:").FontSize(9).Bold();
            column.Item().Text("Payment is due within 30 days of invoice date. Please include invoice number in payment reference.")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Generated by LexCore | ").FontSize(8).FontColor(Colors.Grey.Medium);
            text.Span(DateTime.UtcNow.ToString("dd MMM yyyy HH:mm UTC")).FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }
}
