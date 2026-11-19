using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Interfaces;

public sealed record LabelField(string Name, string Value);

public sealed record LabelContent(
    LabelEntityType Type,
    string Code,
    IReadOnlyList<LabelField> StandardFields,
    IReadOnlyList<LabelField> SmallFields);

public sealed record LabelRenderItem(
    LabelContent Content,
    decimal WidthMm,
    decimal HeightMm,
    LabelLayout Layout,
    int Copies,
    int PrintNumber,
    DateOnly PrintDate);

public sealed class LabelDoesNotFitException(string message) : Exception(message);

public interface ILabelRenderer
{
    byte[] Render(IReadOnlyList<LabelRenderItem> items);
}
