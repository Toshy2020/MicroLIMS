namespace MicroLIMS.Application.Interfaces;

public interface IBarcodeEncoder
{
    bool[] EncodeCode128(string content);      // one entry per module, true = bar, no quiet zone
    bool[,] EncodeDataMatrix(string content);  // [row, column], true = dark, square, no quiet zone
}
