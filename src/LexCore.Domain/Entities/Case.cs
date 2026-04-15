using System.ComponentModel.DataAnnotations.Schema;
using LexCore.Domain.Enums;

namespace LexCore.Domain.Entities;

public class Case : BaseEntity
{
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    [Column("Description")]
    public string? CaseBackground { get; set; }
    public string? CaseType { get; set; }
    public string? CourtName { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Active;
    public DateTime? FiledDate { get; set; }
    [Column("InternalNotes")]
    public string? PrivateNotes { get; set; }
    [Column("ClientVisibleNotes")]
    public string? ClientInstructions { get; set; }

    // Client & Parties
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientWhatsApp { get; set; }
    public string? ClientPosition { get; set; }
    public string? OppositeParty { get; set; }
    public string? OppositePartyLawyer { get; set; }

    // Court & Legal
    public string? SectionAct { get; set; }
    public string? FIRNumber { get; set; }
    public string? CaseStage { get; set; }
    public string? ReliefSought { get; set; }

    // Fees
    public string? FeeType { get; set; }
    public decimal? AgreedFees { get; set; }
    public decimal? AdvancePaid { get; set; }
    public decimal? PerHearingFee { get; set; }
    public bool VakalatnamaSigned { get; set; } = false;

    // Screen 1 — Court hierarchy (new)
    public string? CourtType { get; set; }       // "SupremeCourt","HighCourt","DistrictCourt","Tribunal","ConsumerCourt","RevenueCourt","FamilyCourt","SpecialCourt"
    public string? StateUT { get; set; }          // state/UT name
    public string? District { get; set; }         // district name
    public string? CourtHierarchyName { get; set; } // full court name from hierarchy
    public string? CaseTypeCode { get; set; }     // official code e.g. "OS","ST","WP(C)"
    public string? CaseNature { get; set; }       // "Civil","Criminal","Constitutional","Revenue","CivilAndCriminal"
    public DateTime? TotalFeeLockedAt { get; set; } // set once at creation, never updated

    // Screen 2 — Client details (new)
    public string? ClientType { get; set; }       // "Individual","Company","Firm"
    public string? ClientFatherName { get; set; }
    public int? ClientAge { get; set; }
    public string? ClientAddress { get; set; }
    public string? ClientIDDocumentType { get; set; } // "Aadhaar Card","PAN Card" etc.
    public string? CompanyName { get; set; }
    public string? CompanyCIN { get; set; }
    public string? CompanyGST { get; set; }
    public string? AuthorisedRepresentative { get; set; }
    public string? AuthorisedRepresentativeDesignation { get; set; }

    // Screen 3 — Opposite party (new)
    public string? OppositePartiesJson { get; set; } // JSON array of multiple parties
    public string? OppositeCounselName { get; set; }
    public string? OppositeCounselPhone { get; set; }
    public string? OppositeCounselEnrollment { get; set; }
    public string? OppositeCounselCity { get; set; }

    // Screen 4 — Legal details (new)
    public string? ActsAndSectionsJson { get; set; } // JSON array of {act, sections}
    public DateTime? FIRDate { get; set; }
    public string? PSDistrict { get; set; }
    public string? PSState { get; set; }
    public string? NatureOfOffence { get; set; }
    public string? CaseNotesHtml { get; set; }  // rich text from Notion editor

    // Screen 5 — Fee (new)
    public string? PaymentMode { get; set; }     // "Cash","UPI","BankTransfer","Cheque","DD"
    public string? PaymentReference { get; set; } // UTR/cheque number/transaction ID

    // Screen 6 — Notifications (new)
    public bool HearingReminderEvening { get; set; } = true;
    public bool HearingReminderMorning { get; set; } = true;
    public bool LimitationAlertEnabled { get; set; } = true;
    public bool InvoiceOverdueAlert { get; set; } = true;
    public bool CaseStageChangeAlert { get; set; } = false;
    public bool ClientWhatsAppEnabled { get; set; } = false;
    public string? PrivateNotesHtml { get; set; }    // rich text private notes
    public string? ClientInstructionsHtml { get; set; } // rich text client instructions

    // Dates
    public DateTime? LimitationDate { get; set; }

    // Notification tracking
    public bool NotifyClientOnHearing { get; set; } = true;
    public bool NotifyClientOnStatus { get; set; } = true;
    public bool LimitationAlertSent30 { get; set; } = false;
    public bool LimitationAlertSent7 { get; set; } = false;
    public bool LimitationAlertSent1 { get; set; } = false;

    public ICollection<Payment> AdvancePayments { get; set; } = new List<Payment>();
    public ICollection<CaseLawyer> CaseLawyers { get; set; } = new List<CaseLawyer>();
    public ICollection<CaseClient> CaseClients { get; set; } = new List<CaseClient>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Hearing> Hearings { get; set; } = new List<Hearing>();
    public ICollection<Chat> Chats { get; set; } = new List<Chat>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<CaseNote> CaseNotes { get; set; } = new List<CaseNote>();
}
