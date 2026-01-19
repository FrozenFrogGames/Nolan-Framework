using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Text.Json.Serialization;

namespace FrozenFrogFramework.NolanTech
{
    /// <summary>
    /// Définit le type de comportement d'un F3NolanGameTag en fonction de son préfixe.
    /// </summary>
    public enum ENolanTagOperation
    {
        /// <summary>
        /// Pas de préfixe : Le tag doit être retiré (si coût/comportement) ou ajouté (si gain/charge utile).
        /// </summary>
        RemoveOrAppend,
        /// <summary>
        /// Préfixe '?' : La présence du tag est vérifiée pour le succès, mais il n'est pas retiré.
        /// </summary>
        SucceedIfPresent,
        /// <summary>
        /// Préfixe '!' : L'absence du tag est vérifiée pour le succès, mais il n'est pas ajouté.
        /// </summary>
        FailedIfPresent
    }

    /// <summary>
    /// Représente un seul tag de jeu avec un type de comportement déterminé par un préfixe optionnel
    /// et un suffixe de localisation optionnel.
    /// </summary>
    [JsonConverter(typeof(F3NolanGameTagConverter))] // Référence du convertisseur Json
    public struct F3NolanGameTag : IEquatable<F3NolanGameTag>
    {
        public record MeterRange(short Start, short End, short Value = short.MaxValue, short Step = 0);
        /// <summary>
        /// La valeur brute de la chaîne du tag telle qu'elle a été saisie, y compris tout préfixe et suffixe de localisation.
        /// </summary>
        public string RawValue { get; private set; }

        /// <summary>
        /// La valeur réelle du tag sans aucun préfixe ni suffixe de localisation.
        /// </summary>
        public string Value { get; private set; }

        public ENolanTagOperation TagOperation { get; private set; }
        public MeterRange Meter { get; private set; }

        /// <summary>
        /// La localisation optionnelle associée au tag, extraite du suffixe <LOCATION>.
        /// Chaîne vide si aucune localisation n'est présente.
        /// </summary>
        public string Location { get; private set; }

        /// <summary>
        /// Constructeur pour F3NolanGameTag.
        /// Analyse la chaîne d'entrée pour déterminer le comportement et la valeur du tag.
        /// </summary>
        /// <param name="tagValue">La chaîne de caractères du tag, incluant les préfixes et suffixes.</param>
        public F3NolanGameTag(string tagValue)
        {
            RawValue = tagValue ?? throw NolanException.ScriptError("Tag is empty.", ENolanScriptError.NullOrEmpty);
            TagOperation = ENolanTagOperation.RemoveOrAppend;
            Value = RawValue;
            Location = string.Empty;

            // Vérifier et extraire le suffixe de localisation
            int locationStartIndex = RawValue.LastIndexOf('<');
            int locationEndIndex = RawValue.LastIndexOf('>');
            if (locationStartIndex != -1 && locationEndIndex != -1 && locationEndIndex > locationStartIndex)
            {
                Location = RawValue.Substring(locationStartIndex + 1, locationEndIndex - locationStartIndex - 1);
                Value = RawValue.Substring(0, locationStartIndex); // La valeur est la partie avant le suffixe de localisation
            }

            // Vérifier et extraire le préfixe de comportement
            if (Value.Length > 0)
            {
                char firstChar = Value.First();

                if (firstChar == '?')
                {
                    TagOperation = ENolanTagOperation.SucceedIfPresent;
                    Value = Value.Substring(1);
                }
                else if (firstChar == '!')
                {
                    TagOperation = ENolanTagOperation.FailedIfPresent;
                    Value = Value.Substring(1);
                }
            }

            int rangeIndex = Value.LastIndexOf("..");

            if (rangeIndex == -1)
            {
                short rangeMin = short.MinValue, rangeMax = short.MaxValue, rangeValue = short.MaxValue, rangeStep = 0;

                if (Value.Contains("++"))
                {
                    Value = ExtractNumberStep(Value, Value.LastIndexOf("++"), out rangeStep);
                }
                else if (Value.Contains("--"))
                {
                    Value = ExtractNumberStep(Value, Value.LastIndexOf("--"), out rangeStep);
                }
                else if (Value.Contains("<="))
                {
                    rangeStep = short.MinValue;

                    Value = ExtractNumberStep(Value, Value.LastIndexOf("<="), out rangeMax);
                }
                else if (Value.Contains(">="))
                {
                    rangeStep = short.MaxValue;

                    Value = ExtractNumberStep(Value, Value.LastIndexOf(">="), out rangeMin);
                }
                else
                {
                    ExtractNumberValue(Value, out rangeValue);
                }

                Meter = new MeterRange(rangeMin, rangeMax, rangeValue, rangeStep);
            }
            else if (Value.EndsWith(".."))
            {
                Value = Value.Substring(0, rangeIndex);

                Meter = new MeterRange(short.MinValue, short.MaxValue, short.MinValue);
            }
            else
            {
                short rangeStart, rangeEnd;

                Value = ExtractNumberRange(Value, rangeIndex, out rangeStart, out rangeEnd);

                Meter = new MeterRange(rangeStart, rangeEnd);
            }

            // keep track of every gameplay tags created ..
            if (GameTags.Contains(Value) == false)
            {
                int digitIndex = Value.IndexOf('_');
                // .. but ignore tag that ends with a digit
                if (digitIndex == -1 || int.TryParse(Value.Substring(digitIndex + 1), out int digitValue) == false)
                {
                    GameTags.Add(Value);
                }
            }
        }

        public static List<string> GameTags { get; private set; } = new List<string>();

        private static string ExtractNumberStep(string Tag, int Delimiter, out short Value)
        {
            if (Delimiter == Tag.Length - 2)
            {
                Value = 1;
            }
            else
            {
                Value = short.Parse(Tag.Substring(Delimiter + 2));
            }

            if (Tag[Delimiter] == '-')
            {
                Value = (short)-Value; // decrement syntax
            }

            return Tag.Substring(0, Delimiter);
        }

        private static void ExtractNumberValue(string Tag, out short Value)
        {
            int TagEnded = Tag.Length;

            while (TagEnded > 0 && Char.IsNumber(Tag[TagEnded - 1]))
            {
                --TagEnded;
            }

            if (TagEnded == 0 || TagEnded == Tag.Length)
            {
                Value = short.MinValue;
                return;
            }

            if (Tag[TagEnded - 1] == '-')
            {
                --TagEnded; // support negative numbers
            }

            try
            {
                Value = short.Parse(Tag.Substring(TagEnded));
            }
            catch
            {
                Value = short.MinValue;
            }
        }

        private static string ExtractNumberRange(string Tag, int Delimiter, out short Start, out short End)
        {
            int TagEnded = Delimiter - 1;

            while (TagEnded > 0 && Char.IsNumber(Tag[TagEnded - 1]))
            {
                --TagEnded;
            }

            if (Tag[TagEnded - 1] == '-')
            {
                --TagEnded; // support negative numbers
            }

            try
            {
                Start = short.Parse(Tag.Substring(TagEnded, Delimiter - TagEnded));

                End = short.Parse(Tag.Substring(Delimiter + 2));
            }
            catch
            {
                Start = End = short.MaxValue;
            }

            return Tag.Substring(0, TagEnded);
        }

        static private F3NolanGameTag ForceLocation(F3NolanGameTag inTag, string inLocation, ENolanTagOperation inOperation)
        {
            string newRawValue = string.IsNullOrEmpty(inLocation) ? inTag.Value : $"{inTag.Value}<{inLocation}>";

            if (inOperation == ENolanTagOperation.SucceedIfPresent)
            {
                return new F3NolanGameTag($"?{newRawValue}");
            }
            else if (inOperation == ENolanTagOperation.FailedIfPresent)
            {
                return new F3NolanGameTag($"!{newRawValue}");
            }

            return new F3NolanGameTag($"{newRawValue}");
        }

        public F3NolanGameTag Increment(short step, string inLocation, ENolanTagOperation inOperation)
        {
            string oldMeterValue = Meter.Value.ToString();
            short newMeterValue = (short)(Meter.Value + step);

            string newTagValue = Value.Replace(oldMeterValue, newMeterValue.ToString());
            string newRawValue = string.IsNullOrEmpty(inLocation) ? newTagValue : $"{newTagValue}<{inLocation}>";

            if (inOperation == ENolanTagOperation.SucceedIfPresent)
            {
                return new F3NolanGameTag($"?{newRawValue}");
            }
            else if (inOperation == ENolanTagOperation.FailedIfPresent)
            {
                return new F3NolanGameTag($"!{newRawValue}");
            }

            return new F3NolanGameTag($"{newRawValue}");
        }

        /// <summary>
        /// Retourne une représentation textuelle du F3NolanGameTag.
        /// </summary>
        /// <returns>Une chaîne de caractères représentant le tag.</returns>
        public override string ToString()
        {
            return RawValue; // Retourne la valeur brute pour la sérialisation/débogage
        }

        public override bool Equals(object? obj)
        {
            return obj is F3NolanGameTag tag && Equals(tag);
        }

        public bool Equals(F3NolanGameTag other)
        {
            return RawValue == other.RawValue;
        }

        public override int GetHashCode()
        {
            return RawValue.GetHashCode();
        }

        public bool IsJokerSyntax { get { return RawValue.Contains('*'); }}
        public bool IsRangeSyntax { get { return RawValue.Contains("..") && Meter.Start > short.MinValue && Meter.End < short.MaxValue; }}
        public bool IsGreaterSyntax { get { return RawValue.Contains(">=") && Meter.Step == short.MaxValue; }}
        public bool IsLowerSyntax { get { return RawValue.Contains("<=") && Meter.Step == short.MinValue; }}
        public bool IsIncrementSyntax { get { return RawValue.Contains("++") && Meter.Step > 0; }}
        public bool IsDecrementSyntax { get { return RawValue.Contains("--") && Meter.Step < 0; }}
        public bool IsMeterSyntax { get { return Meter.Value > short.MinValue && Meter.Value < short.MaxValue; }}

        public bool ContainsSugarSyntax()
        {
            return IsJokerSyntax || IsIncrementSyntax || IsDecrementSyntax || (TagOperation == ENolanTagOperation.RemoveOrAppend && (IsGreaterSyntax || IsLowerSyntax || RawValue.Contains("..")));
        }

        public IEnumerable<F3NolanGameTag> ResolveSyntax(F3NolanGameTagSet tags)
        {
            if (ContainsSugarSyntax())
            {
                bool bHaveJoker = Value.Contains(".*.");

                bool bHavePrefix = bHaveJoker || Value.EndsWith(".*");
                bool bHaveSuffix = bHaveJoker || Value.StartsWith("*.");

                if (bHavePrefix || bHaveSuffix)
                {
                    int jokerIndex = Value.IndexOf('*');
                    bool bJokerFound = false;

                    string tagPrefix = bHavePrefix ? Value.Substring(0, jokerIndex) : string.Empty;
                    string tagSuffix = bHaveSuffix ? Value.Substring(jokerIndex + 1) : string.Empty;

                    foreach (F3NolanGameTag tag in tags)
                    {
                        if (bHavePrefix && tag.Value.StartsWith(tagPrefix, StringComparison.InvariantCultureIgnoreCase) == false)
                        {
                            continue;
                        }

                        if (bHaveSuffix && tag.Value.EndsWith(tagSuffix, StringComparison.InvariantCultureIgnoreCase) == false)
                        {
                            continue;
                        }

                        bJokerFound = true;

                        yield return ForceLocation(tag, Location, TagOperation);
                    }

                    int keyIndexStart = Value.IndexOf('.');
                    int keyIndexEnd = Value.LastIndexOf('.');

                    if (bJokerFound == false && keyIndexStart < keyIndexEnd)
                    {
                        if (bHavePrefix == true && bHaveSuffix == false)
                        {
                            string lastPrefix = tagPrefix.Substring(keyIndexStart + 1);

                            foreach (F3NolanGameTag tag in tags)
                            {
                                if (tag.Value.StartsWith(lastPrefix, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    string newRawValue = RawValue.Replace("*", tag.Value.Substring(lastPrefix.Length), StringComparison.InvariantCultureIgnoreCase);
                                    yield return new F3NolanGameTag(newRawValue);
                                }
                            }
                        }
                        else if (bHaveSuffix == true && bHavePrefix == false)
                        {
                            string lastSuffix = tagSuffix.Substring(keyIndexEnd + 1);

                            foreach (F3NolanGameTag tag in tags)
                            {
                                if (tag.Value.EndsWith(lastSuffix, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    string newRawValue = RawValue.Replace("*", tag.Value.Substring(0, tag.Value.Length - lastSuffix.Length), StringComparison.InvariantCultureIgnoreCase);
                                    yield return new F3NolanGameTag(newRawValue);
                                }
                            }
                        }
                    }

                    yield break;
                }

                if (IsIncrementSyntax || IsDecrementSyntax)
                {
                    foreach (F3NolanGameTag tag in tags)
                    {
                        if (tag.Value.StartsWith(Value, StringComparison.InvariantCultureIgnoreCase) && tag.IsMeterSyntax)
                        {
                            yield return tag.Increment(Meter.Step, Location, TagOperation);
                        }
                    }

                    yield break;
                }

                if (IsGreaterSyntax || IsLowerSyntax)
                {
                    foreach (F3NolanGameTag tag in tags)
                    {
                        bool bIsInRange = IsGreaterSyntax ? tag.Meter.Value >= Meter.Start : tag.Meter.Value <= Meter.End;

                        if (bIsInRange && tag.Value.StartsWith(Value, StringComparison.InvariantCultureIgnoreCase) && tag.IsMeterSyntax)
                        {
                            yield return tag;
                        }
                    }

                    yield break;
                }

                if (IsRangeSyntax)
                {
                    foreach (F3NolanGameTag tag in tags)
                    {
                        // check if the value is within the range
                        if (tag.Value.StartsWith(Value, StringComparison.InvariantCultureIgnoreCase) && tag.Meter.Value > short.MinValue &&
                            Meter.Start <= tag.Meter.Value && Meter.End >= tag.Meter.Value)
                        {
                            yield return tag;
                        }
                    }

                    yield break;
                }

                if (Meter.Start == short.MinValue && Meter.End == short.MaxValue && Meter.Value == short.MinValue)
                {
                    foreach (F3NolanGameTag tag in tags)
                    {
                        if (tag.Value.StartsWith(Value, StringComparison.InvariantCultureIgnoreCase) && tag.IsMeterSyntax)
                        {
                            yield return tag;
                        }
                    }

                    yield break;
                }
            }

            yield return this;
        }
    }
}
