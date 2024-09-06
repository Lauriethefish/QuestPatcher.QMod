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
                var strs = Enum.GetNames(typeof(ModLoader));
                try{
                    var str = reader.GetString(); // lambda function can't have reader.GetString() for the equals so this will have to do
                    if (Enum.TryParse(Array.Find(strs, t => t.Equals(str, StringComparison.InvariantCultureIgnoreCase)), out ModLoader modLoader))
                    {
                        return modLoader;
                    }
                }
                catch(ArgumentNullException e)
                {
                    throw new AggregateException(new Exception[]{new JsonException($"Unable to convert '{reader.GetString()}' to ModLoader enum."), e});
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
