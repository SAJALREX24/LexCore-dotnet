namespace LexCore.Domain.Enums;

public enum UserRole
{
    // [Obsolete] FirmAdmin is retained for backwards compatibility
    // with any serialized audit log data. LexCore v1 is solo-only
    // and no new user is created with this role.
    [System.Obsolete("FirmAdmin role is deprecated in v1 solo-only. Do not use.")]
    FirmAdmin = 1,

    Lawyer = 2,

    // [Obsolete] Client role is retained for backwards compatibility.
    // LexCore v1 does not support client logins. Clients are stored
    // as text data on the Case entity (clientName, clientPhone, etc.).
    [System.Obsolete("Client role is deprecated in v1 solo-only. Do not use.")]
    Client = 3,

    SuperAdmin = 4
}
