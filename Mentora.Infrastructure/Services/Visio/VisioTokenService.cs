using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mentora.Core.DTOs.Visio;
using Mentora.Core.Enums;
using Mentora.Core.Exceptions;
using Mentora.Core.Interfaces;
using Mentora.Core.Options;
using Mentora.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mentora.Infrastructure.Services.Visio;

public class VisioTokenService(MentoraDbContext db, IOptions<LiveKitOptions> liveKitOptions) : IVisioTokenService
{
    private readonly LiveKitOptions _options = liveKitOptions.Value;

    public async Task<VisioTokenResponse> IssueForCoachAsync(Guid coachId, Guid sessionId, CancellationToken ct)
    {
        var session = await LoadAsync(sessionId, ct);
        if (session is null || session.SessionCoachId != coachId)
            throw new NotFoundException("Session not found.");

        return Issue(session, identity: $"coach-{coachId:D}",
            displayName: $"{session.CoachFirstName} {session.CoachLastName}".Trim());
    }

    public async Task<VisioTokenResponse> IssueForMemberAsync(Guid memberId, Guid sessionId, CancellationToken ct)
    {
        var session = await LoadAsync(sessionId, ct);

        // Direct SessionMemberId comparison, not V_SESSION_MEMBERS — mirrors
        // SessionService.CancelByMemberAsync: visio-token issuance is only for the individual
        // VISIO booking path. A group session has a null SessionMemberId, so a group participant
        // never matches here and 404s, same as CancelByMemberAsync.
        if (session is null || session.SessionMemberId != memberId)
            throw new NotFoundException("Session not found.");

        return Issue(session, identity: $"member-{memberId:D}",
            displayName: $"{session.MemberFirstName} {session.MemberLastName}".Trim());
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    private Task<SessionProjection?> LoadAsync(Guid sessionId, CancellationToken ct) =>
        db.Sessions
            .AsNoTracking()
            .Where(s => s.SessionId == sessionId)
            .Select(s => new SessionProjection(
                s.SessionId,
                s.SessionCoachId,
                s.SessionMemberId,
                s.SessionOfferType,
                s.SessionStatus,
                s.SessionScheduledAt,
                s.SessionDurationMinutes,
                s.Coach.CoachFirstName,
                s.Coach.CoachLastName,
                s.Member != null ? s.Member.MemberFirstName : null,
                s.Member != null ? s.Member.MemberLastName : null))
            .FirstOrDefaultAsync(ct);

    private VisioTokenResponse Issue(SessionProjection session, string identity, string displayName)
    {
        // SESSION_SCHEDULED_AT is "timestamp with time zone" — Npgsql always returns it as
        // DateTime with Kind=Utc, same as DateTime.UtcNow below, so this arithmetic and the
        // later comparison against "now" are both directly comparable UTC instants.
        var opensAt  = session.SessionScheduledAt.AddMinutes(-_options.JoinBeforeMinutes);
        var closesAt = session.SessionScheduledAt.AddMinutes(session.SessionDurationMinutes + _options.JoinAfterEndMinutes);

        // Only individual VISIO or PRESENTIEL_SOLO sessions are eligible (an in-person session may
        // be run by visio) — a group session has a null SessionMemberId and is out of scope.
        if ((session.SessionOfferType != OfferType.Visio && session.SessionOfferType != OfferType.PresentielSolo)
            || session.SessionMemberId is null)
            throw Conflict(session.SessionId, "NOT_ELIGIBLE", opensAt, closesAt);

        if (session.SessionStatus == SessionStatus.Cancelled)
            throw Conflict(session.SessionId, "CANCELLED", opensAt, closesAt);

        var now = DateTime.UtcNow;
        if (now < opensAt)
            throw Conflict(session.SessionId, "WINDOW_NOT_OPEN", opensAt, closesAt);
        if (now > closesAt)
            throw Conflict(session.SessionId, "WINDOW_CLOSED", opensAt, closesAt);

        var roomName = $"session-{session.SessionId}";
        var token    = BuildToken(roomName, identity, displayName, now, closesAt);

        return new VisioTokenResponse(_options.Url, token, roomName, identity, closesAt);
    }

    private static ConflictException Conflict(Guid sessionId, string reason, DateTime opensAt, DateTime closesAt) =>
        new($"Cannot issue a visio token: {reason}.",
            new { sessionId, reason, opensAt, closesAt });

    // HS256 JWT, hand-built with the already-pinned System.IdentityModel.Tokens.Jwt — no LiveKit
    // SDK / new package. The "video" claim must be a JSON object on the wire, not a string, so it
    // is added with ValueType JsonClaimValueTypes.Json, which JwtSecurityTokenHandler recognizes
    // and embeds as raw JSON rather than an escaped string.
    private string BuildToken(string roomName, string identity, string displayName, DateTime now, DateTime expiresAt)
    {
        var videoClaimJson = JsonSerializer.Serialize(new
        {
            room = roomName,
            roomJoin = true,
            canPublish = true,
            canSubscribe = true,
            canPublishData = true
        });

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, identity),
            new("name", displayName),
            new("video", videoClaimJson, JsonClaimValueTypes.Json)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ApiSecret));
        var token = new JwtSecurityToken(
            issuer:             _options.ApiKey,
            claims:             claims,
            notBefore:          now,
            expires:            expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        // Never log the token or the signing secret.
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record SessionProjection(
        Guid SessionId,
        Guid SessionCoachId,
        Guid? SessionMemberId,
        OfferType SessionOfferType,
        SessionStatus SessionStatus,
        DateTime SessionScheduledAt,
        int SessionDurationMinutes,
        string CoachFirstName,
        string CoachLastName,
        string? MemberFirstName,
        string? MemberLastName);
}
