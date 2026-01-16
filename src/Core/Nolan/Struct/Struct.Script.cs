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
}