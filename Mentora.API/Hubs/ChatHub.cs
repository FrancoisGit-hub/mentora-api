using Mentora.Core.DTOs.Conversation;
using Mentora.Core.Enums;
using Mentora.Core.Interfaces;
using Mentora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Mentora.API.Hubs;

/// <summary>
/// Real-time chat hub for member/coach conversations.
/// Auth: JWT in query string ?access_token=&lt;jwt&gt; (see Program.cs JwtBearerEvents.OnMessageReceived).
/// [Authorize] requires any valid JWT — per-conversation access is enforced inside each Hub method.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IConversationService _conversationService;
    private readonly MentoraDbContext _db;

    public ChatHub(IConversationService conversationService, MentoraDbContext db)
    {
        _conversationService = conversationService;
        _db = db;
    }

    /// <summary>
    /// Joins the SignalR group for a conversation. Required before receiving ReceiveMessage broadcasts.
    /// </summary>
    public async Task JoinConversationAsync(Guid conversationId)
    {
        await EnsureCallerHasAccessAsync(conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    /// <summary>
    /// Leaves the SignalR group (e.g., when navigating away from the chat screen).
    /// </summary>
    public Task LeaveConversationAsync(Guid conversationId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    /// <summary>
    /// Persists a new message via IConversationService and broadcasts ReceiveMessage to the group.
    /// The sender's own connection also receives the broadcast as a visual acknowledgement.
    /// </summary>
    public async Task SendMessageAsync(Guid conversationId, string content)
    {
        await EnsureCallerHasAccessAsync(conversationId);

        var (memberId, coachId, senderType, senderId) = await ResolveContextAsync(conversationId);

        var message = await _conversationService.SendMessageAsync(
            memberId, coachId, senderType, senderId, content, Context.ConnectionAborted);

        await Clients.Group(GroupName(conversationId))
            .SendAsync("ReceiveMessage", message, Context.ConnectionAborted);
    }

    /// <summary>
    /// Marks all unread messages from the other party as read,
    /// then broadcasts MessagesRead to the group (only when at least one message was marked).
    /// </summary>
    public async Task MarkAsReadAsync(Guid conversationId)
    {
        await EnsureCallerHasAccessAsync(conversationId);

        var (memberId, coachId, readerType, _) = await ResolveContextAsync(conversationId);

        var markedCount = await _conversationService.MarkMessagesAsReadAsync(
            memberId, coachId, readerType, Context.ConnectionAborted);

        if (markedCount > 0)
        {
            await Clients.Group(GroupName(conversationId))
                .SendAsync("MessagesRead", markedCount, DateTime.UtcNow, Context.ConnectionAborted);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string GroupName(Guid conversationId) => $"conversation:{conversationId}";

    private async Task EnsureCallerHasAccessAsync(Guid conversationId)
    {
        var conversation = await _db.Conversations
            .Where(c => c.ConversationId == conversationId)
            .Select(c => new { c.MemberId, c.CoachId })
            .FirstOrDefaultAsync(Context.ConnectionAborted);

        if (conversation is null)
            throw new HubException("Conversation not found.");

        var memberClaim = Context.User?.FindFirst("memberId")?.Value;
        if (memberClaim is not null
            && Guid.TryParse(memberClaim, out var memberId)
            && conversation.MemberId == memberId)
            return;

        var coachClaim = Context.User?.FindFirst("coachId")?.Value;
        if (coachClaim is not null
            && Guid.TryParse(coachClaim, out var coachId)
            && conversation.CoachId == coachId)
            return;

        throw new HubException("You don't have access to this conversation.");
    }

    /// <summary>
    /// Resolves the conversation's (memberId, coachId) and the caller's role + id.
    /// Call only after EnsureCallerHasAccessAsync has confirmed access.
    /// </summary>
    private async Task<(Guid memberId, Guid coachId, MessageSenderType senderType, Guid senderId)>
        ResolveContextAsync(Guid conversationId)
    {
        var conversation = await _db.Conversations
            .Where(c => c.ConversationId == conversationId)
            .Select(c => new { c.MemberId, c.CoachId })
            .FirstAsync(Context.ConnectionAborted);

        var memberClaim = Context.User?.FindFirst("memberId")?.Value;
        if (memberClaim is not null
            && Guid.TryParse(memberClaim, out var memberId)
            && conversation.MemberId == memberId)
        {
            return (conversation.MemberId, conversation.CoachId, MessageSenderType.Member, memberId);
        }

        var coachClaim = Context.User?.FindFirst("coachId")?.Value;
        if (coachClaim is not null
            && Guid.TryParse(coachClaim, out var coachId)
            && conversation.CoachId == coachId)
        {
            return (conversation.MemberId, conversation.CoachId, MessageSenderType.Coach, coachId);
        }

        throw new HubException("Unable to resolve caller role for this conversation.");
    }
}
