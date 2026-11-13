namespace MicroLIMS.Application.Abstractions.Security;

public interface IQrCodeRenderer
{
    string RenderSvg(string content);
}
