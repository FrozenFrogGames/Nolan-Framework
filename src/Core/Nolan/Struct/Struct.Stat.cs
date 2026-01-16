using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Immutable;
using System.Linq.Expressions;
using Pidgin;

namespace FrozenFrogFramework.NolanTech
{
    public enum ENolanRuleOperation
    {
        /// <summary>
        /// </summary>
        AssertTagIn,
        /// <summary>
        /// </summary>
        AssertNoTag,
        /// <summary>
        /// </summary>
        AppendTag,
        /// <summary>
        /// </summary>
        RemoveTag,
        /// <summary>
        /// </summary>
        PushText,
        /// <summary>
        /// </summary>
        PushScene,
        /// <summary>
        /// </summary>
        GotoScene,
        /// <summary>
        /// </summary>
        UnlockGoal
    }

    /// <summary>
    /// Représente l'état global du jeu Nolan, composé de plusieurs emplacements.
    /// Peut également indiquer un état de fin de jeu.
    /// Changé de classe à struct et implémente IEquatable<F3NolanStat>.
    /// </summary>
    public struct F3NolanStatData : IEquatable<F3NolanStatData>, IEnumerable<KeyValuePair<string, F3NolanGameTagSet>>
    {
        public static F3NolanStatData Empty => new F3NolanStatData();
        static public string EmptyToString => @"== [DRAG, ]";

        public List<KeyValuePair<string, F3NolanGameTagSet>> Locations { get; }
        public bool IsEmpty { get { return Locations.Count() == 0; } }
        public F3NolanStatData()
        {
            Locations = new List<KeyValuePair<string, F3NolanGameTagSet>>();
        }

        public F3NolanStatData(KeyValuePair<string, F3NolanGameTagSet>[] locations)
        {
            Locations = new List<KeyValuePair<string, F3NolanGameTagSet>>(locations);
        }

        public KeyValuePair<string, F3NolanGameTagSet> this[string name]
        {
            get
            {
                var location = Locations.FirstOrDefault(loc => loc.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (location.Key == null)
                {
                    throw NolanException.ScriptError($"Location '{name}' not found in stat.", ENolanScriptError.KeyNotFound);
                }
                return location;
            }
        }

        public F3NolanStatData Apply(in F3NolanRuleMeta[] meta, in F3NolanRuleData[] rulebook, ref string scene)
        {
            var statLocations = Locations.ToDictionary(
                loc => loc.Key,
                loc => loc.Value.ToList()
            );

            foreach (var op in meta)
            {
                if (op.RuleOperation == ENolanRuleOperation.RemoveTag)
                {
                    if (statLocations.TryGetValue(op.LocationOrGoal, out var tags))
                    {
                        tags.RemoveAll(t => t == op.TagOrKeyName);

                        if (tags.Count() == 0) statLocations.Remove(op.LocationOrGoal);
                    }
                    else
                    {
                        throw NolanException.ScriptError($"Tag '{op.TagOrKeyName}' not in Location '{op.LocationOrGoal}'.", ENolanScriptError.KeyNotFound);
                    }
                }
                else if (op.RuleOperation == ENolanRuleOperation.AppendTag)
                {
                    if (statLocations.TryGetValue(op.LocationOrGoal, out var tags))
                    {
                        tags.Add(op.TagOrKeyName);
                    }
                    else
                    {
                        statLocations.Add(op.LocationOrGoal, new List<string>() { op.TagOrKeyName });
                    }
                }
                else if (op.RuleOperation == ENolanRuleOperation.GotoScene)
                {
                    scene = op.LocationOrGoal;
                }
            }

            return new F3NolanStatData(statLocations.Select(pair => new KeyValuePair<string, F3NolanGameTagSet>(pair.Key, new F3NolanGameTagSet(pair.Value))).ToArray());
        }

        /// <summary>
        /// Retourne une représentation textuelle de l'état du jeu Nolan.
        /// </summary>
        /// <returns>Une chaîne de caractères représentant l'état.</returns>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return F3NolanStatData.EmptyToString;
            }

            var sb = new StringBuilder();
            sb.Append("== ");

            List<string> locationString = Locations.Select(loc => loc.ToString()).ToList();

            locationString.Sort((a, b) =>
            {
                if (a.StartsWith("[DRAG,"))
                {
                    return 1;
                }
                else if (b.StartsWith("[DRAG,"))
                {
                    return -1;
                }
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            foreach (var location in locationString)
            {
                sb.Append(location + " ");
            }

            return sb.ToString().Trim();
        }

        public override bool Equals(object? obj)
        {
            return obj is F3NolanStatData stat && Equals(stat);
        }

        public bool Equals(F3NolanStatData other)
        {
            string rawValue = this.ToString();
            return rawValue.Equals(other.ToString());
        }

        public override int GetHashCode()
        {
            string rawValue = this.ToString();
            return rawValue.GetHashCode();
        }

        public IEnumerator<KeyValuePair<string, F3NolanGameTagSet>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, F3NolanGameTagSet>>)Locations).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}