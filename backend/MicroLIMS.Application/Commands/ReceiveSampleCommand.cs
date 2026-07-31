namespace MicroLIMS.Application.Commands;

public record ReceiveSampleCommand(int ItemId, string BatchNumber, string ContainerNumber, string Cause);
