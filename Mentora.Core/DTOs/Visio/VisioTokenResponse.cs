namespace Mentora.Core.DTOs.Visio;

/// <param name="ServerUrl">LiveKit server WebSocket URL to connect to.</param>
/// <param name="Token">Signed LiveKit room-access JWT.</param>
/// <param name="RoomName">LiveKit room name for this session ("session-{sessionId}").</param>
/// <param name="Identity">Participant identity embedded in the token ("coach-{coachId}" or "member-{memberId}").</param>
/// <param name="ExpiresAt">Token expiration (UTC) — the end of the join window.</param>
public record VisioTokenResponse(
    string ServerUrl,
    string Token,
    string RoomName,
    string Identity,
    DateTime ExpiresAt);
