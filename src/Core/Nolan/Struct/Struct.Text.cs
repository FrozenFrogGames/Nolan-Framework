using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace FrozenFrogFramework.NolanTech
{
    public struct F3NolanTextData
    {
        private Dictionary<string, List<string>> textKeys = new Dictionary<string, List<string>>();

        public F3NolanTextData(in string name, in string line, Func<string, string, string[]> add)
        {
            textKeys.Add(name, new List<string>());

            textKeys[name].AddRange(add(name, line));
        }

        public F3NolanTextData(in List<string> names, in List<string> lines, Func<string, string, string[]> add)
        {
            foreach (string name in names)
            {
                textKeys.Add(name, new List<string>());
            }

            int nameIndex;

            for (int i = 0; i < lines.Count; i++)
            {
                nameIndex = i % names.Count;

                textKeys[names[nameIndex]].AddRange(add(names[nameIndex], lines[i]));
            }
        }

        public string Name { get { return textKeys.Keys.First() ?? 
                throw NolanException.ContextError($"Name is null.", ENolanScriptContext.Text, ENolanScriptError.NullOrEmpty); }  }
        public string[] Value { get { return textKeys[Name].ToArray(); } }
        public override string ToString()
        {
            List<string> results = new List<string>();

            foreach (var result in textKeys.Values)
            {
                results.Add(string.Join(", ", result));
            }

            return string.Join(", ", results);
        }
        // Text Bank Interface
        public string[] Keys
        {
            get { return textKeys.Keys.ToArray(); }
        }
        public string[] this[string name]
        {
            get
            {
                if (textKeys.ContainsKey(name) == false)
                {
                    throw NolanException.ContextError($"Text '{name}' not found.", ENolanScriptContext.Text, ENolanScriptError.KeyNotFound);
                }

                return textKeys[name].ToArray();
            }
        }
        // Text Syntax Helper
        static public string ParseTextFormat(in string textLine)
        {
            string result = textLine.Replace("<>", "\\n");

            int emphasisLast = 0;
            int emphasisIndex = textLine.IndexOf("<*", emphasisLast);

            while (emphasisIndex > -1)
            {
                emphasisLast = result.IndexOf("/>") + 2;

                string signal = result.Substring(emphasisIndex, emphasisLast - emphasisIndex);
                result = result.Replace(signal, signal.Replace("<*", "\\n<Emphasis>").Replace("/>", "</>"));

                emphasisIndex = textLine.IndexOf("<*", emphasisLast);
            }

            return result;
        }
    }
}
