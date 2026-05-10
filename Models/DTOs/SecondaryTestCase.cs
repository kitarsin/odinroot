using System.Text.Json.Serialization;

namespace ODIN.Api.Models.DTOs;

public record SecondaryTestCase(
    [property: JsonPropertyName("find")]          string Find,
    [property: JsonPropertyName("replace")]       string Replace,
    [property: JsonPropertyName("expectedOutput")] string ExpectedOutput);
