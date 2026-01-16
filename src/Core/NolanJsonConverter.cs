using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrozenFrogFramework.NolanTech
{
    /// <summary>
    /// Custom JsonConverter pour F3NolanGameTag afin de le sérialiser/désérialiser en tant que chaîne.
    /// Cela garantit que la logique de parsing personnalisée dans le constructeur F3NolanGameTag est utilisée
    /// lors de la désérialisation, et la méthode ToString() est utilisée pour la sérialisation.
    /// </summary>
    public class F3NolanGameTagConverter : JsonConverter<F3NolanGameTag>
    {
        public override F3NolanGameTag Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string but got {reader.TokenType}");
            }
            string? tagString = reader.GetString();
            if (tagString == null)
            {
                // Gérer le cas de chaîne nulle, peut-être retourner une valeur par défaut ou lancer une exception plus spécifique
                throw new JsonException("F3NolanGameTag string cannot be null during deserialization.");
            }
            // Utiliser le constructeur F3NolanGameTag pour parser la chaîne en structure
            return new F3NolanGameTag(tagString);
        }

        public override void Write(Utf8JsonWriter writer, F3NolanGameTag value, JsonSerializerOptions options)
        {
            // Utiliser la RawValue pour la sérialisation afin de préserver le format original
            writer.WriteStringValue(value.RawValue);
        }
    }

    /// <summary>
    /// Custom JsonConverter pour F3NolanGameTagSet afin de le sérialiser/désérialiser en tant que tableau de chaînes.
    /// Cela garantit que la logique de parsing personnalisée dans le constructeur F3NolanGameTagSet est utilisée
    /// lors de la désérialisation, et la méthode ToString() de chaque tag est utilisée pour la sérialisation.
    /// </summary>
    public class F3NolanGameTagSetConverter : JsonConverter<F3NolanGameTagSet>
    {
        public override F3NolanGameTagSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected StartArray but got {reader.TokenType}");
            }

            var tags = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException($"Expected string within array but got {reader.TokenType}");
                }
                string? tagString = reader.GetString();
                if (tagString != null)
                {
                    tags.Add(tagString);
                }
            }
            return new F3NolanGameTagSet(tags);
        }

        public override void Write(Utf8JsonWriter writer, F3NolanGameTagSet value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var tag in value)
            {
                writer.WriteStringValue(tag.RawValue); // Sérialiser chaque tag en utilisant sa RawValue
            }
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// Custom JsonConverter pour F3NolanRuleData pour gérer la sérialisation/désérialisation.
    /// </summary>
    public class F3NolanRuleDataConverter : JsonConverter<F3NolanRuleData>
    {
        public override F3NolanRuleData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject but got {reader.TokenType}");
            }

            F3NolanGameTag? match = null;
            F3NolanGameTagSet context = new F3NolanGameTagSet(Enumerable.Empty<string>());
            F3NolanGameTagSet cost = new F3NolanGameTagSet(Enumerable.Empty<string>());
            F3NolanGameTagSet payload = new F3NolanGameTagSet(Enumerable.Empty<string>());
            F3NolanGameTagSet gain = new F3NolanGameTagSet(Enumerable.Empty<string>());
            bool isCostFromInventory = true;
            string text = string.Empty;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Expected PropertyName but got {reader.TokenType}");
                }

                string? propertyName = reader.GetString();
                reader.Read(); // Avance au jeton de valeur

                switch (propertyName)
                {
                    case nameof(F3NolanRuleData.Context):
                        context = JsonSerializer.Deserialize<F3NolanGameTagSet>(ref reader, options) ?? new F3NolanGameTagSet(Enumerable.Empty<string>());
                        break;
                    case nameof(F3NolanRuleData.Match):
                        match = JsonSerializer.Deserialize<F3NolanGameTag>(ref reader, options);
                        break;
                    case nameof(F3NolanRuleData.Payload):
                        payload = JsonSerializer.Deserialize<F3NolanGameTagSet>(ref reader, options) ?? new F3NolanGameTagSet(Enumerable.Empty<string>());
                        break;
                    case nameof(F3NolanRuleData.IsDrag):
                        isCostFromInventory = reader.GetBoolean();
                        break;
                    case nameof(F3NolanRuleData.Cost):
                        cost = JsonSerializer.Deserialize<F3NolanGameTagSet>(ref reader, options) ?? new F3NolanGameTagSet(Enumerable.Empty<string>());
                        break;
                    case nameof(F3NolanRuleData.Gain):
                        gain = JsonSerializer.Deserialize<F3NolanGameTagSet>(ref reader, options) ?? new F3NolanGameTagSet(Enumerable.Empty<string>());
                        break;
                    case nameof(F3NolanRuleData.Text):
                        text = reader.GetString() ?? string.Empty;
                        break;
                    default:
                        reader.Skip(); // Ignorer les propriétés inconnues
                        break;
                }
            }

            return new F3NolanRuleData(match ?? throw new JsonException($"Expected 'match' property but got null."),
                context,
                cost,
                payload,
                gain,
                isCostFromInventory,
                text);
        }

        public override void Write(Utf8JsonWriter writer, F3NolanRuleData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(F3NolanRuleData.Match));
            JsonSerializer.Serialize(writer, value.Match, options);
            if (value.Context.Any())
            {
                writer.WritePropertyName(nameof(F3NolanRuleData.Context));
                JsonSerializer.Serialize(writer, value.Context, options);
            }
            if (value.Cost.Any())
            {
                writer.WritePropertyName(nameof(F3NolanRuleData.Cost));
                JsonSerializer.Serialize(writer, value.Cost, options);

                if (value.IsDrag == false)
                {
                    writer.WriteBoolean(nameof(F3NolanRuleData.IsDrag), value.IsDrag);
                }
            }
            if (value.Payload.Any())
            {
                writer.WritePropertyName(nameof(F3NolanRuleData.Payload));
                JsonSerializer.Serialize(writer, value.Payload, options);
            }
            if (value.Gain.Any())
            {
                writer.WritePropertyName(nameof(F3NolanRuleData.Gain));
                JsonSerializer.Serialize(writer, value.Gain, options);
            }
            if (value.HasText)
            {
                writer.WriteString(nameof(F3NolanRuleData.Text), value.Text);
            }
            writer.WriteEndObject();
        }
    }
    /// <summary>
    /// Custom JsonConverter for F3NolanStitchStruct.
    /// </summary>
    public class F3NolanStitchStructConverter : JsonConverter<F3NolanStitchStruct>
    {
        public override F3NolanStitchStruct Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject but got {reader.TokenType}");
            }

            string key = string.Empty;
            string next = string.Empty;
            F3NolanGameTagSet context = F3NolanGameTagSet.Empty;
            F3NolanGameTagSet cost = F3NolanGameTagSet.Empty;
            F3NolanGameTagSet payload = F3NolanGameTagSet.Empty;
            F3NolanGameTagSet gain = F3NolanGameTagSet.Empty;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Expected PropertyName but got {reader.TokenType}");
                }

                string? propertyName = reader.GetString();
                reader.Read(); // Advance to value

                switch (propertyName)
                {
                    case nameof(F3NolanStitchStruct.Choice):
                        key = reader.GetString() ?? string.Empty;
                        break;
                    case nameof(F3NolanStitchStruct.Next):
                        next = reader.GetString() ?? string.Empty;
                        break;
                    case nameof(F3NolanStitchStruct.Context):
                        context = JsonSerializer.Deserialize<F3NolanGameTagSet>(ref reader, options) ?? F3NolanGameTagSet.Empty;
                        break;
                    case nameof(F3NolanStitchStruct.Cost):
                        cost = JsonSerializer.Deserialize<F3NolanGameTagSet>(ref reader, options) ?? F3NolanGameTagSet.Empty;
                        break;
                    case nameof(F3NolanStitchStruct.Payload):
                        payload = JsonSerializer.Deserialize<F3NolanGameTagSet>(ref reader, options) ?? F3NolanGameTagSet.Empty;
                        break;
                    case nameof(F3NolanStitchStruct.Gain):
                        gain = JsonSerializer.Deserialize<F3NolanGameTagSet>(ref reader, options) ?? F3NolanGameTagSet.Empty;
                        break;
                    default:
                        reader.Skip(); // Skip unknown properties
                        break;
                }
            }
            // Create the struct and then set the Condition property
            var stitch = new F3NolanStitchStruct(key, next);
            stitch.Context = context;
            stitch.Cost = cost;
            stitch.Payload = payload;
            stitch.Gain = gain;
            return stitch;
        }

        public override void Write(Utf8JsonWriter writer, F3NolanStitchStruct value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(F3NolanStitchStruct.Choice), value.Choice);
            writer.WriteString(nameof(F3NolanStitchStruct.Next), value.Next);
            if (value.Context.Any())
            {
                writer.WritePropertyName(nameof(F3NolanStitchStruct.Context));
                JsonSerializer.Serialize(writer, value.Context, options);
            }
            if (value.Cost.Any())
            {
                writer.WritePropertyName(nameof(F3NolanStitchStruct.Cost));
                JsonSerializer.Serialize(writer, value.Cost, options);
            }
            if (value.Payload.Any())
            {
                writer.WritePropertyName(nameof(F3NolanStitchStruct.Payload));
                JsonSerializer.Serialize(writer, value.Payload, options);
            }
            if (value.Gain.Any())
            {
                writer.WritePropertyName(nameof(F3NolanStitchStruct.Gain));
                JsonSerializer.Serialize(writer, value.Gain, options);
            }
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Custom JsonConverter for F3NolanRouteStruct.
    /// </summary>
    public class F3NolanRouteStructConverter : JsonConverter<F3NolanRouteStruct>
    {
        public override F3NolanRouteStruct Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject but got {reader.TokenType}");
            }

            string[] textKeys = Array.Empty<string>();
            List<F3NolanStitchStruct> flow = new List<F3NolanStitchStruct>();
            string? gotoName = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Expected PropertyName but got {reader.TokenType}");
                }

                string? propertyName = reader.GetString();
                reader.Read(); // Advance to value

                switch (propertyName)
                {
                    case nameof(F3NolanRouteStruct.Text):
                        textKeys = JsonSerializer.Deserialize<string[]>(ref reader, options) ?? Array.Empty<string>();
                        break;
                    case nameof(F3NolanRouteStruct.Flow):
                        flow = JsonSerializer.Deserialize<List<F3NolanStitchStruct>>(ref reader, options) ?? new List<F3NolanStitchStruct>();
                        break;
                    case nameof(F3NolanRouteStruct.Goto):
                        gotoName = reader.GetString();
                        break;
                    default:
                        reader.Skip(); // Skip unknown properties
                        break;
                }
            }

            // Create the struct and then set all the properties
            var route = new F3NolanRouteStruct(textKeys);
            route.Flow = flow;
            route.Goto = gotoName;
            return route;
        }

        public override void Write(Utf8JsonWriter writer, F3NolanRouteStruct value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(F3NolanRouteStruct.Text));
            JsonSerializer.Serialize(writer, value.Text, options);
            if (value.Flow.Count > 0)
            {
                writer.WritePropertyName(nameof(F3NolanRouteStruct.Flow));
                JsonSerializer.Serialize(writer, value.Flow, options);
            }
            if (string.IsNullOrEmpty(value.Goto) == false)
            {
                writer.WriteString(nameof(F3NolanRouteStruct.Goto), value.Goto);
            }
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Custom JsonConverter for F3NolanTextBook.
    /// Serializes the 'Ranges' dictionary and the 'Routes' dictionary.
    /// Deserialization will populate both 'Ranges' and 'Routes' properties.
    /// </summary>
    public class F3NolanTextBookConverter : JsonConverter<F3NolanTextBook>
    {
        public override F3NolanTextBook Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject but got {reader.TokenType}");
            }

            Dictionary<string, string[]> bookLines = new Dictionary<string, string[]>();
            Dictionary<string, F3NolanRouteStruct> bookRoutes = new Dictionary<string, F3NolanRouteStruct>();
            Dictionary<string, Range[]> bookRanges = new Dictionary<string, Range[]>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Expected PropertyName but got {reader.TokenType}");
                }

                string? propertyName = reader.GetString();
                reader.Read(); // Advance to value

                switch (propertyName)
                {
                    case "Text":
                        bookLines = JsonSerializer.Deserialize<Dictionary<string, string[]>>(ref reader, options) ?? new Dictionary<string, string[]>();
                        break;
                    case "Route":
                        bookRoutes = JsonSerializer.Deserialize<Dictionary<string, F3NolanRouteStruct>>(ref reader, options) ?? new Dictionary<string, F3NolanRouteStruct>();
                        break;
                    case "Range":
                        bookRanges = JsonSerializer.Deserialize<Dictionary<string, Range[]>>(ref reader, options) ?? new Dictionary<string, Range[]>();
                        break;
                    default:
                        reader.Skip(); // Skip unknown properties
                        break;
                }
            }

            return new F3NolanTextBook(bookLines, bookRanges, bookRoutes);
        }

        public override void Write(Utf8JsonWriter writer, F3NolanTextBook value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("Text");
            JsonSerializer.Serialize(writer, value.Lines, options);

            // Serialize the 'Routes' dictionary
            writer.WritePropertyName("Route");
            JsonSerializer.Serialize(writer, value.Routes, options);

            // Serialize the 'Ranges' dictionary
            writer.WritePropertyName("Range");
            JsonSerializer.Serialize(writer, value.Ranges, options);

            writer.WriteEndObject();
        }
    }

    public class F3NolanScriptConverter : JsonConverter<F3NolanScriptData>
    {
        public override F3NolanScriptData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject but got {reader.TokenType}");
            }

            string? initialStat = null;
            F3NolanRuleData[]? rulebook = null;
            Dictionary<string, string[]> bookLines = new Dictionary<string, string[]>();
            Dictionary<string, F3NolanRouteStruct> bookRoutes = new Dictionary<string, F3NolanRouteStruct>();
            Dictionary<string, Range[]> bookRanges = new Dictionary<string, Range[]>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Expected PropertyName but got {reader.TokenType}");
                }

                string? propertyName = reader.GetString();
                reader.Read(); // Advance to value

                switch (propertyName)
                {
                    case "Text":
                        bookLines = JsonSerializer.Deserialize<Dictionary<string, string[]>>(ref reader, options) ?? new Dictionary<string, string[]>();
                        break;
                    case "Stat":
                        initialStat = reader.GetString() ?? F3NolanStatData.EmptyToString;
                        break;
                    case "Rule":
                        rulebook = JsonSerializer.Deserialize<F3NolanRuleData[]>(ref reader, options) ?? null;
                        break;
                    case "Route":
                        bookRoutes = JsonSerializer.Deserialize<Dictionary<string, F3NolanRouteStruct>>(ref reader, options) ?? new Dictionary<string, F3NolanRouteStruct>();
                        break;
                    case "Range":
                        bookRanges = JsonSerializer.Deserialize<Dictionary<string, Range[]>>(ref reader, options) ?? new Dictionary<string, Range[]>();
                        break;
                    default:
                        reader.Skip(); // Skip unknown properties
                        break;
                }
            }

            F3NolanTextBook textbook = new F3NolanTextBook(bookLines, bookRanges, bookRoutes);

            if (initialStat == null)
            {
                throw new JsonException($"Expected 'Stat' PropertyName");
            }

            var stat = F3NolanDataStatParser.Parse(initialStat);
            if (stat.Success == false)
            {
                throw new JsonException($"Invalid 'Stat' PropertyName");
            }

            return new F3NolanScriptData(stat.Value, rulebook ?? Array.Empty<F3NolanRuleData>(), textbook);
        }

        public override void Write(Utf8JsonWriter writer, F3NolanScriptData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Serialize the 'InitialStat' class
            writer.WriteString("Stat", value.InitialStat.ToString());

            // Serialize the 'Rulebook' list
            writer.WritePropertyName("Rule");
            JsonSerializer.Serialize(writer, value.RuleBook, options);

            // Serialize the 'Lines' dictionary
            writer.WritePropertyName("Text");
            JsonSerializer.Serialize(writer, value.TextBook.Lines, options);

            // Serialize the 'Routes' dictionary
            writer.WritePropertyName("Route");
            JsonSerializer.Serialize(writer, value.TextBook.Routes, options);

            // Serialize the 'Ranges' dictionary
            writer.WritePropertyName("Range");
            JsonSerializer.Serialize(writer, value.TextBook.Ranges, options);

            writer.WriteEndObject();
        }
    }

    public static class NolanJsonSerializer
    {
        public static string SerializeNolanRuleData(F3NolanRuleData data, bool prettyPrint = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = prettyPrint,
                Converters =
                {
                    new RangeConverter(),
                    new F3NolanGameTagConverter(),
                    new F3NolanGameTagSetConverter(),
                    new F3NolanRuleDataConverter(),
                    new F3NolanScriptConverter()
                }
            };
            return JsonSerializer.Serialize(data, options);
        }

        public static string SerializeNolanRouteData(F3NolanRouteStruct data, bool prettyPrint = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = prettyPrint,
                Converters =
                {
                    new RangeConverter(),
                    new F3NolanGameTagConverter(),
                    new F3NolanGameTagSetConverter(),
                    new F3NolanRuleDataConverter(),
                    new F3NolanRouteStructConverter(),
                    new F3NolanStitchStructConverter(),
                    new F3NolanScriptConverter()
                }
            };
            return JsonSerializer.Serialize(data, options);
        }

        public static string SerializeNolanScript(F3NolanScriptData product, bool prettyPrint = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = prettyPrint,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Converters =
                {
                    new RangeConverter(),
                    new F3NolanGameTagConverter(),
                    new F3NolanGameTagSetConverter(),
                    new F3NolanRuleDataConverter(),
                    new F3NolanRouteStructConverter(),
                    new F3NolanStitchStructConverter(),
                    new F3NolanScriptConverter()
                }
            };
            return JsonSerializer.Serialize(product, options);
        }

        public static F3NolanScriptData DeserializeNolanScript(string jsonString)
        {
            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new RangeConverter(),
                    new F3NolanGameTagConverter(),
                    new F3NolanGameTagSetConverter(),
                    new F3NolanRuleDataConverter(),
                    new F3NolanRouteStructConverter(),
                    new F3NolanStitchStructConverter(),
                    new F3NolanScriptConverter()
                }
            };

            return JsonSerializer.Deserialize<F3NolanScriptData>(jsonString, options);
        }

        /// <summary>
        /// Custom JsonConverter for System.Range to serialize/deserialize its Start and End values as integers.
        /// </summary>
        internal class RangeConverter : JsonConverter<Range>
        {
            public override Range Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException($"Expected StartObject but got {reader.TokenType}");
                }

                int start = 0;
                int end = 0;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException($"Expected PropertyName but got {reader.TokenType}");
                    }

                    string? propertyName = reader.GetString();
                    reader.Read(); // Advance to value

                    switch (propertyName)
                    {
                        case "Start":
                            start = reader.GetInt32();
                            break;
                        case "End":
                            end = reader.GetInt32();
                            break;
                        default:
                            reader.Skip(); // Skip unknown properties
                            break;
                    }
                }
                return new Range(start, end);
            }

            public override void Write(Utf8JsonWriter writer, Range value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("Start", value.Start.Value);
                writer.WriteNumber("End", value.End.Value);
                writer.WriteEndObject();
            }
        }

        public static string SerializeNolanRuleBook(F3NolanRuleData[] ruleBook, bool prettyPrint = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = prettyPrint,
                Converters =
                {
                    new F3NolanGameTagConverter(),
                    new F3NolanGameTagSetConverter(),
                    new F3NolanRuleDataConverter(),
                    new RangeConverter(),
                    new F3NolanStitchStructConverter(),
                    new F3NolanRouteStructConverter(),
                    new F3NolanTextBookConverter()
                }
            };
            return JsonSerializer.Serialize(ruleBook, options);
        }

        public static F3NolanRuleData[] DeserializeNolanRuleBook(string jsonString)
        {
            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new F3NolanGameTagConverter(),
                    new F3NolanGameTagSetConverter(),
                    new F3NolanRuleDataConverter(),
                    new RangeConverter(),
                    new F3NolanStitchStructConverter(),
                    new F3NolanRouteStructConverter(),
                    new F3NolanTextBookConverter()
                }
            };
            return JsonSerializer.Deserialize<F3NolanRuleData[]>(jsonString, options) ?? Array.Empty<F3NolanRuleData>();
        }

        /// <summary>
        /// Serializes an F3NolanTextBook object into a JSON string with custom format.
        /// </summary>
        /// <param name="textBook">The F3NolanTextBook object to serialize.</param>
        /// <param name="prettyPrint">If true, the JSON will be indented for readability.</param>
        /// <returns>A JSON representation of the F3NolanTextBook object.</returns>
        public static string SerializeNolanTextBook(F3NolanTextBook textBook, bool prettyPrint = true)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = prettyPrint,
                Converters =
                {
                    new RangeConverter(),
                    new F3NolanStitchStructConverter(),
                    new F3NolanRouteStructConverter(),
                    new F3NolanTextBookConverter() // Use the specific TextBook converter
                }
            };
            return JsonSerializer.Serialize(textBook, options);
        }

        /// <summary>
        /// Deserializes a JSON string into an F3NolanTextBook object.
        /// Note: Due to the custom serialization format and existing class structure,
        /// the deserialized F3NolanTextBook will only have its 'Ranges' property populated
        /// from the JSON. Its 'Lines' property will be empty.
        /// </summary>
        /// <param name="jsonString">The JSON string to deserialize.</param>
        /// <returns>An F3NolanTextBook object.</returns>
        public static F3NolanTextBook DeserializeNolanTextBook(string jsonString)
        {
            var options = new JsonSerializerOptions
            {
                Converters =
                {
                    new RangeConverter(),
                    new F3NolanStitchStructConverter(),
                    new F3NolanRouteStructConverter(),
                    new F3NolanTextBookConverter() // Use the specific TextBook converter
                }
            };

            return JsonSerializer.Deserialize<F3NolanTextBook>(jsonString, options) ?? throw new JsonException("Deserialize failed to parse Nolan Text Book.");
        }
    }
}
