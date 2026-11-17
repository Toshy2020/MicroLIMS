namespace MicroLIMS.Shared.Messages;

// An exception that carries a message code (see MessageCodes) and the values
// its translated sentence needs, besides the English sentence it has always
// carried. ExceptionMiddleware answers it with ApiResponse.ErrorDetails so the
// interface can show the sentence in the user's language.
public interface ICodedMessage
{
    // Null for an exception built with only an English sentence.
    string? Code { get; }

    IReadOnlyDictionary<string, string>? Parameters { get; }
}
