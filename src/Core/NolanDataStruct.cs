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
    [JsonConverter(typeof(F3NolanScriptConverter))]
    public struct F3NolanScriptData
    {
        public static F3NolanScriptData Empty => new F3NolanScriptData(new F3NolanStatData(), new F3NolanRuleData[] { }, F3NolanTextBook.Empty);
        public F3NolanStatData InitialStat;
        public F3NolanRuleData[] RuleBook;
        public F3NolanTextBook TextBook;
        public F3NolanScriptData(F3NolanStatData stat, F3NolanRuleData[] rulebook, F3NolanTextBook textbook)
        {
            InitialStat = stat;
            RuleBook = rulebook;
            TextBook = textbook;
        }

        public struct Transient
        {
            public F3NolanRuleData[] RuleBook { get; }
            public F3NolanTextBook TextBook { get; }
            public F3NolanStatData InitialStat { get { return StatStack.Count() == 0 ? F3NolanStatData.Empty : StatStack.First(); } }
            public F3NolanStatData CurrentStat { get { return StatStack.Count() == 0 ? F3NolanStatData.Empty : StatStack.Last(); } }
            public string InitialScene { get { return StatStack.Count() == 0 ? string.Empty : SceneStack.First(); } }
            public string CurrentScene { get { return StatStack.Count() == 0 ? string.Empty : SceneStack.Last(); } }
            public Transient(F3NolanRuleData[] rulebook, F3NolanTextBook textbook, string scene, F3NolanStatData data, string chapter)
            {
                RuleBook = rulebook;
                TextBook = textbook;

                string[] chapterIntro = textbook.GetInitialKeys(chapter);

                SceneStack = new List<string>() { scene };
                StatStack = new List<F3NolanStatData>() { data };
                TextStack = new List<KeyValuePair<string, string[]>>() { new KeyValuePair<string, string[]>(chapter, chapterIntro) };

                SeqTextStack = new Dictionary<string, int>(); // auto-increment indexes for text sequences (for both looping and not)
            }

            private List<string> SceneStack;
            private List<F3NolanStatData> StatStack;
            public List<KeyValuePair<string, string[]>> TextStack;
            private Dictionary<string, int> SeqTextStack;

            public void Push(string scene, F3NolanStatData data, string textKey, string[] textValue)
            {
                SceneStack.Add(scene);

                StatStack.Add(data);

                TextStack.Add(new KeyValuePair<string, string[]>(textKey, textValue));
            }

            public bool HasText()
            {
                return TextStack.Last().Value.Count() > 0 && string.IsNullOrEmpty(TextStack.Last().Value.First()) == false;
            }

            public string[] ComputeKeys(string[] keys)
            {
                List<string> results = new List<string>();

                foreach (string key in keys)
                {
                    TextBook.ComputeKeys(key, CurrentStat, ref SeqTextStack, ref results);
                }

                return results.ToArray();
            }

            public bool Undo()
            {
                if (SceneStack.Count > 1)
                {
                    SceneStack.Remove(SceneStack.Last());
                }
                else
                {
                    return false;
                }

                if (StatStack.Count > 1)
                {
                    StatStack.Remove(StatStack.Last());
                }
                else
                {
                    return false;
                }

                if (TextStack.Count > 1)
                {
                    TextStack.Remove(TextStack.Last());
                }
                else
                {
                    return false;
                }

                SeqTextStack.Clear(); // TODO fix text indexes reseted with proper undo logic
                return true;
            }
        }
    }

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

    public struct F3NolanRuleMeta : IEquatable<F3NolanRuleMeta>
    {
        /// <summary>
        /// </summary>
        public ENolanRuleOperation RuleOperation { get; private set; }

        /// <summary>
        /// </summary>
        public string TagOrKeyName { get; private set; }

        /// <summary>
        /// </summary>
        public string LocationOrGoal { get; private set; }

        public F3NolanRuleMeta(ENolanRuleOperation operation, string tagName, string locationName)
        {
            RuleOperation = operation;
            TagOrKeyName = tagName;
            LocationOrGoal = locationName;
        }

        public override bool Equals(object? obj)
        {
            return obj is F3NolanRuleMeta meta && Equals(meta);
        }

        public bool Equals(F3NolanRuleMeta other)
        {
            return RuleOperation == other.RuleOperation &&
                   TagOrKeyName == other.TagOrKeyName &&
                   LocationOrGoal == other.LocationOrGoal;
        }

        public override int GetHashCode()
        {
            return RuleOperation.GetHashCode() ^
                   TagOrKeyName.GetHashCode() ^
                   LocationOrGoal.GetHashCode();
        }
    }

    /// <summary>
    /// Représente une règle Nolan, définissant un comportement basé sur un contexte, un coût, un comportement, une charge utile et un gain.
    /// </summary>
    [JsonConverter(typeof(F3NolanRuleDataConverter))]
    public struct F3NolanRuleData : IEquatable<F3NolanRuleData>
    {
        public static F3NolanRuleData Empty => new F3NolanRuleData(new F3NolanGameTag("None"), F3NolanGameTagSet.Empty, F3NolanGameTagSet.Empty, F3NolanGameTagSet.Empty, F3NolanGameTagSet.Empty, false);
        /// <summary>
        /// Le comportement principal de la règle.
        /// </summary>
        public F3NolanGameTag Match { get; }
        /// <summary>
        /// Le contexte dans lequel la règle peut être appliquée.
        /// </summary>
        public F3NolanGameTagSet Context { get; }
        /// <summary>
        /// La charge utile résultant de l'application de la règle.
        /// </summary>
        public F3NolanGameTagSet Payload { get; }
        /// <summary>
        /// Indique si le coût est retiré de l'inventaire.
        /// </summary>
        public bool IsDrag { get; }
        /// <summary>
        /// Le coût à payer pour appliquer la règle.
        /// </summary>
        public F3NolanGameTagSet Cost { get; }
        /// <summary>
        /// Le gain obtenu après l'application de la règle.
        /// </summary>
        public F3NolanGameTagSet Gain { get; }

        public bool HasText { get { return string.IsNullOrEmpty(Keys) == false; } }
        public string Text { get { return Keys ?? throw NolanException.ContextError($"Text is null.", ENolanScriptContext.Rule, ENolanScriptError.NullOrEmpty); } }
        private string? Keys;

        /// <summary>
        /// Constructeur pour F3NolanRuleData.
        /// </summary>
        /// <param name="context">Le contexte de la règle.</param>
        /// <param name="match">Le comportement de la règle.</param>
        /// <param name="payload">La charge utile de la règle.</param>
        /// <param name="isDrag">Vrai si le coût est de l'inventaire, faux sinon.</param>
        /// <param name="cost">Le coût de la règle.</param>
        /// <param name="gain">Le gain de la règle.</param>
        public F3NolanRuleData(
            F3NolanGameTag match,
            F3NolanGameTagSet context,
            F3NolanGameTagSet cost,
            F3NolanGameTagSet payload,
            F3NolanGameTagSet gain,
            bool isDrag,
            F3NolanTextData? textData = null)
        {
            Match = match;
            Context = context;
            Cost = cost;
            Payload = payload;
            Gain = gain;
            IsDrag = isDrag;
            Keys = textData.HasValue ? textData.Value.Name : null;
        }
        public F3NolanRuleData(
            F3NolanGameTag match,
            F3NolanGameTagSet context,
            F3NolanGameTagSet cost,
            F3NolanGameTagSet payload,
            F3NolanGameTagSet gain,
            bool isDrag,
            string textString)
        {
            Match = match;
            Context = context;
            Cost = cost;
            Payload = payload;
            Gain = gain;
            IsDrag = isDrag;
            Keys = string.IsNullOrEmpty(textString) ? null : textString;
        }

        public bool Accept(in F3NolanStatData stat, out KeyValuePair<string, F3NolanRuleMeta[]> meta)
        {
            var ruleMatch = Match;

            foreach (var location in stat.Locations.Where(loc => loc.Value.Any(t => t.Value == ruleMatch.Value)))
            {
                var ruleTags = new HashSet<string>(location.Value.Select(t => t.Value));
                var ruleMeta = new List<F3NolanRuleMeta>();

                if (ruleMatch.TagOperation == ENolanTagOperation.FailedIfPresent)
                {
                    meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                    return false; // the match tag is not present or invalid syntax (match tag can not be FailedIfPresent)
                }

                if (string.IsNullOrEmpty(Match.Location) == false)
                {
                    ruleMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.GotoScene, string.Empty, Match.Location));
                }

                var locationName = location.Key;

                ruleMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, Match.Value, locationName));

                if (ruleMatch.TagOperation == ENolanTagOperation.RemoveOrAppend)
                {
                    ruleMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, Match.Value, locationName));
                }

                bool bCostIsValidated = true;

                foreach (var tag in Cost) // validate drop syntax within each rule locations
                {
                    if (bCostIsValidated == false)
                    {
                        break;
                    }
                    else if (IsDrag == true)
                    {
                        continue;
                    }

                    switch (tag.TagOperation)
                    {
                        case ENolanTagOperation.RemoveOrAppend:
                            bCostIsValidated = ruleTags.Contains(tag.Value);
                            if (bCostIsValidated)
                            {
                                ruleMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, locationName));
                                ruleMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, locationName));
                            }
                            break;

                        case ENolanTagOperation.SucceedIfPresent:
                            bCostIsValidated = ruleTags.Contains(tag.Value);
                            if (bCostIsValidated)
                            {
                                ruleMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, locationName));
                            }
                            break;

                        case ENolanTagOperation.FailedIfPresent:
                            bCostIsValidated = (ruleTags.Contains(tag.Value) == false);
                            if (bCostIsValidated)
                            {
                                ruleMeta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, locationName));
                            }
                            break;
                    }
                }

                if (bCostIsValidated && AcceptInternal(stat, in locationName, ref ruleMeta))
                {
                    meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.IsNullOrEmpty(Keys) ? string.Empty : Keys, ruleMeta.ToArray());
                    return true;
                }
            }

            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
            return false; // Cost tags not validated
        }

        private bool AcceptInternal(in F3NolanStatData stat, in string name, ref List<F3NolanRuleMeta> meta)
        {
            var dragTags = new HashSet<string>(stat.Locations.Where(loc => loc.Key == "DRAG").SelectMany(loc => loc.Value).Select(t => t.Value));

            foreach (var tag in Cost)
            {
                if (IsDrag == false) // already handled in Accept method
                {
                    continue;
                }

                switch (tag.TagOperation)
                {
                    case ENolanTagOperation.RemoveOrAppend:
                        if (dragTags.Contains(tag.Value) == false)
                        {
                            return false; // Cost tag must be present in DRAG
                        }

                        meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, "DRAG"));
                        meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, "DRAG"));
                        break;

                    case ENolanTagOperation.SucceedIfPresent:
                        if (dragTags.Contains(tag.Value) == false)
                        {
                            return false; // Cost tag (from drag) must be present in DRAG location
                        }

                        meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, "DRAG"));
                        break;

                    case ENolanTagOperation.FailedIfPresent:
                        if (dragTags.Contains(tag.Value))
                        {
                            return false; // Cost tag must not be present in DRAG
                        }

                        meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, "DRAG"));
                        break;
                }
            }

            // 3. Handle gain tags into DRAG
            foreach (var tag in Gain)
            {
                if (dragTags.Contains(tag.Value) == false && tag.TagOperation == ENolanTagOperation.RemoveOrAppend)
                {
                    meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, "DRAG"));
                    meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, tag.Value, "DRAG"));
                }
                else if (Cost.Contains(tag.Value))
                {
                    throw NolanException.ContextError($"Tag '?{tag.Value}' into Cost (ENolanTagOperation.SucceedIfPresent). ", ENolanScriptContext.Rule);
                }
                else
                {
                    return false; // the gain tag is already present or invalid syntax (behavior tag must be RemoveOrAppend)
                }
            }

            // 4. Handle context tags in any locations
            var statLocations = stat.Locations.Where(loc => loc.Key != "DRAG");

            foreach (var tag in Context)
            {
                string? locationName = null;

                if (tag.IsGreaterSyntax || tag.IsLowerSyntax || tag.IsRangeSyntax)
                {
                    bool bIsInRange, bTagFound = false;

                    foreach (var location in statLocations)
                    {
                        foreach (var test in location.Value)
                        {
                            if (tag.IsGreaterSyntax)
                            {
                                bIsInRange = test.Meter.Value < short.MaxValue && test.Meter.Value >= tag.Meter.Start;
                            }
                            else if (tag.IsLowerSyntax)
                            {
                                bIsInRange = test.Meter.Value <= tag.Meter.End && test.Meter.Value > short.MinValue;
                            }
                            else // tag.IsRangeSyntax
                            {
                                bIsInRange = test.Meter.Value >= tag.Meter.Start && test.Meter.Value <= tag.Meter.End;
                            }

                            if (bIsInRange && test.IsMeterSyntax && test.Value.StartsWith(tag.Value))
                            {
                                if (tag.TagOperation == ENolanTagOperation.FailedIfPresent)
                                {
                                    return false;
                                }
                                else if (tag.TagOperation == ENolanTagOperation.SucceedIfPresent)
                                {
                                    meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, test.Value, location.Key));
                                }

                                bTagFound = true;
                            }
                        }
                    }

                    if (tag.TagOperation == ENolanTagOperation.SucceedIfPresent && bTagFound == false)
                    {
                        return false;
                    }
                }
                else
                {
                    foreach (var location in statLocations)
                    {
                        if (location.Value.Any(t => t.Value.Equals(tag.Value)))
                        {
                            if (locationName == null || name.Equals(location.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                locationName = location.Key;
                            }
                        }

                        if (tag.TagOperation == ENolanTagOperation.FailedIfPresent)
                        {
                            meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, location.Key));
                        }
                    }

                    switch (tag.TagOperation)
                    {
                        case ENolanTagOperation.RemoveOrAppend:
                            if (locationName == null)
                            {
                                return false; // Context tag must be present in one location
                            }

                            meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, locationName));
                            meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, locationName ?? name));
                            break;

                        case ENolanTagOperation.SucceedIfPresent:
                            if (locationName == null)
                            {
                                return false; // Context tag must be present in one location
                            }

                            meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, locationName ?? name));
                            break;

                        case ENolanTagOperation.FailedIfPresent:
                            if (locationName != null)
                            {
                                return false; // Context tag must not be present in any location
                            }
                            break;
                    }
                }
            }

            // 4. Handle payload tags in any locations
            foreach (var tag in Payload)
            {
                if (tag.TagOperation != ENolanTagOperation.RemoveOrAppend || tag.Location.Equals("DRAG", StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Payload tag invalid syntax (must be RemoveOrAppend and not targeting DRAG stat)
                }

                foreach (var location in statLocations)
                {
                    if (location.Value.Any(t => t.Value == tag.Value))
                    {
                        bool bIsSameLocation = string.IsNullOrEmpty(tag.Location) || tag.Location.Equals(location.Key, StringComparison.OrdinalIgnoreCase);

                        if (bIsSameLocation && tag.TagOperation == ENolanTagOperation.FailedIfPresent)
                        {
                            return false; // Payload tag must not be already present in same location
                        }

                        meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, location.Key));
                    }
                }

                meta.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, tag.Value, string.IsNullOrEmpty(tag.Location) ? name : tag.Location));
            }

            return true;
        }

        /// <summary>
        /// Retourne une représentation textuelle de la règle Nolan.
        /// </summary>
        /// <returns>Une chaîne de caractères représentant la règle.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("++ ");

            // Contexte (afficher uniquement s'il n'est pas vide)
            if (Context.Any())
            {
                sb.Append($"({Context})");
            }
            else if (IsDrag == false)
            {
                sb.Append("()");
            }

            // Coût (afficher uniquement s'il n'est pas vide)
            if (Cost.Any())
            {
                sb.Append(IsDrag ? "{" : "-{");
                sb.Append(Cost);
                sb.Append(IsDrag ? "}-" : "}");
            }
            else
            {
                if (IsDrag && Context.Any() == false)
                {
                    sb.Append("()");
                }

                sb.Append("-");
            }

            // Comportement
            sb.Append($"[{Match}]->");

            // Partie Payload (afficher uniquement si non vide)
            if (Payload.Any())
            {
                sb.Append($"({Payload})");
            }

            // Partie Gain (afficher uniquement si non vide)
            if (Gain.Any())
            {
                sb.Append($"{{{Gain}}}");
            }
            else if (Payload.Any() == false)
            {
                sb.Append("()");
            }

            return sb.ToString().Trim();
        }

        public override bool Equals(object? obj)
        {
            return obj is F3NolanRuleData data && Equals(data);
        }

        public bool Equals(F3NolanRuleData other)
        {
            // Comparer toutes les propriétés pour l'égalité
            return Context.Equals(other.Context) &&
                   Match.Equals(other.Match) &&
                   Payload.Equals(other.Payload) &&
                   IsDrag == other.IsDrag &&
                   Cost.Equals(other.Cost) &&
                   Gain.Equals(other.Gain);
        }

        public override int GetHashCode()
        {
            // Combiner les codes de hachage de toutes les propriétés
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Context.GetHashCode();
                hash = hash * 23 + Match.GetHashCode();
                hash = hash * 23 + Payload.GetHashCode();
                hash = hash * 23 + IsDrag.GetHashCode();
                hash = hash * 23 + Cost.GetHashCode();
                hash = hash * 23 + Gain.GetHashCode();
                return hash;
            }
        }

        // TODO support as an extension method on F3NolanRuleData class

        public void ResolveSyntax(in F3NolanStatData stat, ref List<F3NolanRuleData> rulebook)
        {
            if (ContainsSugarSyntax())
            {
                if (Match.ContainsSugarSyntax())
                {
                    foreach (KeyValuePair<string, F3NolanGameTagSet> location in stat.Locations)
                    {
                        foreach (var matchTag in Match.ResolveSyntax(location.Value))
                        {
                            ResolveMatchSyntax(matchTag, in stat, ref rulebook);
                        }
                    }
                }
                else
                {
                    ResolveMatchSyntax(Match, in stat, ref rulebook);
                }
            }
            else
            {
                rulebook.Add(this);
            }
        }

        private bool ContainsSugarSyntax()
        {
            return Match.ContainsSugarSyntax() ||
                   Context.ContainsSugarSyntax() ||
                   Cost.ContainsSugarSyntax() ||
                   Payload.ContainsSugarSyntax() ||
                   Gain.ContainsSugarSyntax();
        }

        private void ResolveMatchSyntax(F3NolanGameTag matchTag, in F3NolanStatData stat, ref List<F3NolanRuleData> rulebook)
        {
            if (Context.ContainsSugarSyntax())
            {
                foreach (KeyValuePair<string, F3NolanGameTagSet> location in stat.Locations)
                {
                    foreach (var contextSet in Context.ResolveSyntax(location.Value))
                    {
                        ResolveContextSyntax(matchTag, contextSet, in stat, ref rulebook);
                    }
                }
            }
            else
            {
                ResolveContextSyntax(matchTag, Context, in stat, ref rulebook);
            }
        }

        private void ResolveContextSyntax(F3NolanGameTag matchTag, F3NolanGameTagSet contextSet, in F3NolanStatData stat, ref List<F3NolanRuleData> rulebook)
        {
            if (Cost.ContainsSugarSyntax())
            {
                foreach (KeyValuePair<string, F3NolanGameTagSet> location in stat.Locations)
                {
                    foreach (var costSet in Cost.ResolveSyntax(location.Value))
                    {
                        ResolveCostSyntax(matchTag, contextSet, costSet, ref rulebook);
                    }
                }
            }
            else
            {
                ResolveCostSyntax(matchTag, contextSet, Cost, ref rulebook);
            }
        }

        private void ResolveCostSyntax(F3NolanGameTag matchTag, F3NolanGameTagSet contextSet, F3NolanGameTagSet costSet, ref List<F3NolanRuleData> rulebook)
        {
            if (Payload.ContainsSugarSyntax())
            {
                foreach (var payloadSet in Payload.ResolveSyntax(F3NolanGameTagSet.ConcatSet(matchTag, contextSet, costSet)))
                {
                    ResolvePayloadSyntax(matchTag, contextSet, costSet, payloadSet, ref rulebook);
                }
            }
            else
            {
                ResolvePayloadSyntax(matchTag, contextSet, costSet, Payload, ref rulebook);
            }
        }

        private void ResolvePayloadSyntax(F3NolanGameTag matchTag, F3NolanGameTagSet contextSet, F3NolanGameTagSet costSet, F3NolanGameTagSet payloadSet, ref List<F3NolanRuleData> rulebook)
        {
            if (Gain.ContainsSugarSyntax())
            {
                foreach (var gainSet in Gain.ResolveSyntax(F3NolanGameTagSet.ConcatSet(matchTag, contextSet, costSet)))
                {
                    F3NolanRuleData result = new F3NolanRuleData(
                        matchTag,
                        contextSet,
                        costSet,
                        payloadSet,
                        gainSet,
                        IsDrag,
                        Keys ?? string.Empty
                    );

                    if (rulebook.Contains(result) == false)
                    {
                        rulebook.Add(result);
                    }
                }
            }
            else
            {
                F3NolanRuleData result = new F3NolanRuleData(
                    matchTag,
                    contextSet,
                    costSet,
                    payloadSet,
                    Gain,
                    IsDrag,
                    Keys ?? string.Empty
                );

                if (rulebook.Contains(result) == false)
                {
                    rulebook.Add(result);
                }
            }
        }
    }
}