namespace MicroLIMS.Application.Commands;

public record SaveResultCommand(int TestOrderId, string RawValue, int EnteredByUserId);
