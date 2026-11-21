namespace Application.Common.Interfaces;

public record EmailMessage(string ToEmail, string Subject, string HtmlBody);

public interface IEmailBackgroundQueue
{
    void QueueEmail(EmailMessage message);
    IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct);
}
