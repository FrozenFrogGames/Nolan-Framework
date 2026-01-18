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
    }
}