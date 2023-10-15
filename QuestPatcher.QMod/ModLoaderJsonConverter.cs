using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuestPatcher.QMod
{
    public class ModLoaderJsonConverter : JsonConverter<ModLoader>
    {
        public override ModLoader Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                if (Enum.TryParse(reader.GetString(), out ModLoader modLoader))
                {
                    return modLoader;
                }
            }

            throw new JsonException($"Unable to convert '{reader.GetString()}' to ModLoader enum.");
        }

        public override void Write(Utf8JsonWriter writer, ModLoader value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
