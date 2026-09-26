using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using Xunit;

namespace NativeTavern.Tests;

/// Opening a long conversation used to cost several queries per message, so the bundled read has to
/// answer exactly what reading message by message answered.
public class SessionBundleTests : IDisposable
{
    private readonly ChatViewModelHarness _harness = new();
    private readonly ChatMessageRepository _messages;
    private readonly ChatAttachmentRepository _attachments;
    private readonly MessageSwipeRepository _swipes;

    public SessionBundleTests()
    {
        _messages = new ChatMessageRepository(_harness.Factory);
        _attachments = new ChatAttachmentRepository(_harness.Factory);
        _swipes = new MessageSwipeRepository(_harness.Factory);
    }

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task BundleAnswersTheSameAsReadingMessageByMessage()
    {
        var session = await _harness.CreateSessionAsync("长对话");
        var ids = new List<long>();
        for (var i = 0; i < 6; i++)
            ids.Add(await AddMessageAsync(session.Id, i % 2 == 0 ? ChatRole.User : ChatRole.Assistant, $"第 {i} 句"));
        await _attachments.AddAsync(Attachment(ids[1], "a.png"));
        await _attachments.AddAsync(Attachment(ids[3], "b.png"));
        await _swipes.AddAsync(Swipe(ids[3], 0, "第 3 句"));
        await _swipes.AddAsync(Swipe(ids[3], 1, "另一种写法"));

        var bundle = await _harness.Service.GetSessionBundleAsync(session.Id);

        Assert.Equal(6, bundle.Messages.Count);
        foreach (var message in bundle.Messages)
        {
            Assert.Equal(await _harness.Service.GetSwipeCountAsync(message), bundle.SwipeCountFor(message.Id));
            Assert.Equal(
                (await _harness.Service.GetAttachmentsAsync(message.Id)).Select(x => x.FileName),
                bundle.AttachmentsFor(message.Id).Select(x => x.FileName));
        }
        Assert.Equal(2, bundle.SwipeCountFor(ids[3]));
        Assert.Empty(bundle.AttachmentsFor(ids[5]));
    }

    [Fact]
    public async Task FirstSwipeIsBackfilledOnceAndOnlyForWrittenText()
    {
        var session = await _harness.CreateSessionAsync("补齐候选");
        var user = await AddMessageAsync(session.Id, ChatRole.User, "提问");
        var assistant = await AddMessageAsync(session.Id, ChatRole.Assistant, "回答");
        var blank = await AddMessageAsync(session.Id, ChatRole.Assistant, string.Empty);

        var first = await _harness.Service.GetSessionBundleAsync(session.Id);
        var again = await _harness.Service.GetSessionBundleAsync(session.Id);

        Assert.Equal(1, first.SwipeCountFor(assistant));
        Assert.Equal(0, first.SwipeCountFor(user));
        Assert.Equal(0, first.SwipeCountFor(blank));
        Assert.Equal(first.SwipeCountFor(assistant), again.SwipeCountFor(assistant));
        Assert.Empty(await _swipes.GetByMessageAsync(blank));
        // The back-filled copy carries the reply itself, so swiping left still returns to it.
        var stored = await _swipes.GetByMessageAsync(assistant);
        Assert.Equal("回答", Assert.Single(stored).Content);
    }

    private Task<long> AddMessageAsync(long sessionId, ChatRole role, string content) =>
        _messages.AddAsync(new ChatMessage
        {
            ChatSessionId = sessionId, Role = role, Content = content, CreatedAt = DateTimeOffset.UtcNow
        });

    private static ChatAttachment Attachment(long messageId, string fileName) => new()
    {
        ChatMessageId = messageId, FileName = fileName, FilePath = $"/tmp/{fileName}",
        MimeType = "image/png", SizeBytes = 12, CreatedAt = DateTimeOffset.UtcNow
    };

    private static MessageSwipe Swipe(long messageId, int index, string content) => new()
    {
        ChatMessageId = messageId, SwipeIndex = index, Content = content, CreatedAt = DateTimeOffset.UtcNow
    };
}
