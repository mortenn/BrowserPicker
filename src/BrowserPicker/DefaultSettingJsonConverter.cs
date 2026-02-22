using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrowserPicker;

/// <summary>Enables System.Text.Json to deserialize <see cref="DefaultSetting"/> via its primary constructor.</summary>
internal sealed class DefaultSettingJsonConverter : JsonConverter<DefaultSetting>
{
	/// <inheritdoc />
	public override DefaultSetting Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		MatchType type = default;
		string? pattern = null;
		string? browser = null;

		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException();
		}

		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				break;
			}
			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				continue;
			}
			var name = reader.GetString();
			reader.Read();
			switch (name)
			{
				case "Type":
					var typeStr = reader.GetString();
					if (!string.IsNullOrEmpty(typeStr) && Enum.TryParse<MatchType>(typeStr, true, out var parsedType))
						type = parsedType;
					break;
				case "Pattern":
					pattern = reader.GetString();
					break;
				case "Browser":
					browser = reader.GetString();
					break;
				default:
					reader.Skip();
					break;
			}
		}

		return new DefaultSetting(type, pattern, browser);
	}

	/// <inheritdoc />
	public override void Write(Utf8JsonWriter writer, DefaultSetting value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteString("Type", value.Type.ToString());
		writer.WriteString("Pattern", value.Pattern);
		writer.WriteString("Browser", value.Browser);
		writer.WriteEndObject();
	}
}
