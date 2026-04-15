using LexCore.Domain.Entities;

namespace LexCore.Application.Interfaces;

public interface IPdfService
{
    byte[] GenerateInvoicePdf(Invoice invoice, User client, User? lawyer = null);
}
