namespace MicroLIMS.Application.DTOs;

// One laboratory the Receiving page can target for a given item, with how
// many of the item's assigned tests belong to it.
public record ReceiptLabOptionDto(int SectionId, string SectionCode, string SectionName, int TestCount);
