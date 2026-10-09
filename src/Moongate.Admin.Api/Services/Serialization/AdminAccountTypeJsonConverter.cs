using System.Text.Json;
using System.Text.Json.Serialization;
using Moongate.Admin.Api.Types.Accounts;

namespace Moongate.Admin.Api.Services.Serialization;

public sealed class AdminAccountTypeJsonConverter : JsonConverter<AdminAccountType>
{
    public override AdminAccountType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } value)
        {
            if (value.Equals("regular", StringComparison.OrdinalIgnoreCase))
            {
                return AdminAccountType.Regular;
            }

            if (value.Equals("gameMaster", StringComparison.OrdinalIgnoreCase))
            {
                return AdminAccountType.GameMaster;
            }

            if (value.Equals("administrator", StringComparison.OrdinalIgnoreCase))
            {
                return AdminAccountType.Administrator;
            }
        }

        throw new JsonException("A single supported account type is required.");
    }

    public override void Write(Utf8JsonWriter writer, AdminAccountType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            AdminAccountType.Regular => "regular",
            AdminAccountType.GameMaster => "gameMaster",
            AdminAccountType.Administrator => "administrator",
            _ => throw new JsonException("Unknown account type.")
        });
    }
}
