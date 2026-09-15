using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroLIMS.API.Json;

// The System.Text.Json settings for every MVC response - applied in
// Program.cs and reused by tests, so a test serializes exactly what the API
// would send.
public static class ApiJsonOptions
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new UtcNullableDateTimeConverter());

        // Several endpoints return EF entities, and some navigations point
        // back at each other (MediaProduct.Configurations <->
        // MediaConfiguration.MediaProduct). When a request has tracked both
        // sides, the loop would fail serialization with a 500 AFTER the
        // change was saved (e.g. a prepared media lot). IgnoreCycles writes
        // the repeated reference as null instead; responses without a loop
        // are unchanged.
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    }
}
