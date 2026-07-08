namespace MyBudget.Features.SharedKernel.Email;

public sealed record EmailMessage(string To, string Subject, string Body);
