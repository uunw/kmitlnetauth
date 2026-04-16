using System.Text.Json.Serialization;

namespace KmitlNetAuth.Core.Platform;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, CredentialPayload>))]
internal partial class CredentialJsonContext : JsonSerializerContext;

public sealed class CredentialPayload
{
    public string Iv { get; set; } = "";
    public string Data { get; set; } = "";
}
