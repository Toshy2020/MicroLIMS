namespace MicroLIMS.Application.Commands;

public record ApproveCommand(int TestOrderId, string Decision, string? Comment, int DecidedByUserId);
