using BasicAdmin.Enums;

namespace BasicAdmin.Ents;

internal record struct ActivePunishment
{
    public int Id { get; init; }
    public PunishmentType Type { get; init; }
    public int Length { get; init; }
    public DateTime ExpiresAt { get; init; }
}
