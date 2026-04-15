using System.Text.Json;
using LexCore.Application.Interfaces;
using LexCore.Domain.Entities;
using LexCore.Domain.Enums;
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

    public byte[] GenerateInvoicePdf(Invoice invoice, User client, User? lawyer = null)
    {
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, invoice, lawyer));
                page.Content().Element(c => ComposeContent(c, invoice, client));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, Invoice invoice, User? lawyer = null)
    {
        // v1 solo-only: use lawyer's own name/details as the practice header.
        var practiceName = lawyer?.Name ?? "LexCore Legal";
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(practiceName).FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                // Bar enrollment number
                var barEnrollment = lawyer?.BarEnrollmentNumber;
                if (!string.IsNullOrEmpty(barEnrollment))
                    column.Item().Text($"Bar Enr. No: {barEnrollment}").FontSize(9).FontColor(Colors.Grey.Darken1);
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

            column.Item().Row(row =>
            {
                // Bill To (left side)
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Bill To:").Bold();
                    col.Item().Text(client.Name);
                    if (!string.IsNullOrEmpty(client.Email))
                        col.Item().Text(client.Email)
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrEmpty(client.Phone))
                        col.Item().Text($"Ph: {client.Phone}")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                // Case details (right side)
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignRight().Text("Case Details:").Bold();
                    if (invoice.Case != null)
                    {
                        col.Item().AlignRight()
                            .Text(invoice.Case.Title)
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(invoice.Case.CaseNumber))
                            col.Item().AlignRight()
                                .Text($"Case No: {invoice.Case.CaseNumber}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(invoice.Case.CourtName))
                            col.Item().AlignRight()
                                .Text($"Court: {invoice.Case.CourtName}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(invoice.Case.CaseType))
                            col.Item().AlignRight()
                                .Text($"Type: {invoice.Case.CaseType}")
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });
            });

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

                if (string.IsNullOrEmpty(invoice.LineItems))
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text(invoice.Description ?? "Legal Services");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text($"₹{invoice.Amount:N2}");
                }

                if (!string.IsNullOrEmpty(invoice.LineItems))
                {
                    try
                    {
                        var items = JsonSerializer.Deserialize<
                            List<Dictionary<string, string>>>(invoice.LineItems);
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                var desc = item.GetValueOrDefault("description", "");
                                var amtStr = item.GetValueOrDefault("amount", "0");
                                var amt = decimal.TryParse(amtStr,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out var parsed) ? parsed : 0;
                                if (string.IsNullOrWhiteSpace(desc) && amt == 0)
                                    continue;
                                table.Cell().BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2).Padding(5)
                                    .Text(desc);
                                table.Cell().BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2).Padding(5)
                                    .AlignRight().Text($"₹{amt:N2}");
                            }
                        }
                    }
                    catch
                    {
                        // Fallback: show raw string if JSON parse fails
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                            .Padding(5).Text(invoice.LineItems);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                            .Padding(5).Text("");
                    }
                }
            });

            column.Item().AlignRight().Width(200).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Cell().Padding(3).AlignRight().Text("Subtotal:");
                table.Cell().Padding(3).AlignRight().Text($"₹{invoice.Amount:N2}");

                if (invoice.GstAmount > 0)
                {
                    if (invoice.IsInterState)
                    {
                        table.Cell().Padding(3).AlignRight().Text("IGST (18%):");
                        table.Cell().Padding(3).AlignRight()
                            .Text($"₹{invoice.GstAmount:N2}");
                    }
                    else
                    {
                        table.Cell().Padding(3).AlignRight().Text("CGST (9%):");
                        table.Cell().Padding(3).AlignRight()
                            .Text($"₹{invoice.GstAmount / 2:N2}");
                        table.Cell().Padding(3).AlignRight().Text("SGST (9%):");
                        table.Cell().Padding(3).AlignRight()
                            .Text($"₹{invoice.GstAmount / 2:N2}");
                    }
                }

                table.Cell().Padding(3).Background(Colors.Blue.Lighten4).AlignRight().Text("Total:").Bold();
                table.Cell().Padding(3).Background(Colors.Blue.Lighten4).AlignRight().Text($"₹{invoice.TotalAmount:N2}").Bold();
            });

            column.Item().PaddingTop(20).Row(row =>
            {
                var statusColor = invoice.Status.ToString() switch
                {
                    "Paid" => Colors.Green.Darken1,
                    "Overdue" => Colors.Red.Darken1,
                    _ => Colors.Orange.Darken1
                };
                row.AutoItem().Text($"Status: {invoice.Status}")
                    .FontSize(12).Bold().FontColor(statusColor);
            });

            if (invoice.Status == InvoiceStatus.Paid)
            {
                column.Item().PaddingTop(8).Column(payCol =>
                {
                    payCol.Item().Text("Payment Details:").FontSize(9).Bold();
                    payCol.Item().Text(
                        $"Amount Paid: ₹{invoice.PaidAmount:N2}  |  " +
                        $"Mode: {invoice.PaymentMode ?? "—"}  |  " +
                        $"Date: {(invoice.PaymentDate.HasValue ? invoice.PaymentDate.Value.ToString("dd MMM yyyy") : "—")}"
                    ).FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrEmpty(invoice.TxnReference))
                        payCol.Item().Text($"TXN Ref: {invoice.TxnReference}")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            }
            else
            {
                column.Item().PaddingTop(20).Text("Payment Terms:")
                    .FontSize(9).Bold();
                column.Item().Text(
                    "Payment is due within 30 days of invoice date. " +
                    "Please include invoice number in payment reference.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            }
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