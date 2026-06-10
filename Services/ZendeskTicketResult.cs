namespace DigifyCXIntranet.Services;

public record ZendeskTicketResult(bool Succeeded, long? TicketId, string TicketUrl, string Message);
