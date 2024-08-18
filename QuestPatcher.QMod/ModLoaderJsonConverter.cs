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
                var strs = Enum.GetNames<ModLoader>();
                try{
                    if (Enum.TryParse(str.Find(t => t.Equals(reader.GetString(), StringComparison.InvariantCultureIgnoreCase), out ModLoader modLoader))
                    {
                        return modLoader;
                    }
                }
                catch(ArgumentNullException e)
                {
                    throw new AggrigateException([new JsonException($"Unable to convert '{reader.GetString()}' to ModLoader enum."), e]);
                }
            }
            else if (reader.TokenType == JsonTokenType.Number)
                return (ModLoader)reader.GetInt64();

            throw new JsonException($"Unable to convert '{reader.GetString()}' to ModLoader enum.");
        }

        public override void Write(Utf8JsonWriter writer, ModLoader value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
