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
    [JsonConverter(typeof(F3NolanStitchStructConverter))]
    public struct F3NolanStitchStruct
    {
        public F3NolanStitchStruct(string key, string next)
        {
            Choice = key;
            Next = next;

            Context = F3NolanGameTagSet.Empty;
            Cost = F3NolanGameTagSet.Empty;
            Payload = F3NolanGameTagSet.Empty;
            Gain = F3NolanGameTagSet.Empty;
        }

        public string Choice;
        public F3NolanGameTagSet Context;
        public F3NolanGameTagSet Cost;
        public F3NolanGameTagSet Payload;
        public F3NolanGameTagSet Gain;
        public string Next;

        public bool Accept(in F3NolanStatData stat, out KeyValuePair<string, F3NolanRuleMeta[]> meta)
        {
            var metaList = new List<F3NolanRuleMeta>();

            var dragTags = new HashSet<string>(stat.Locations.Where(loc => loc.Key == "DRAG").SelectMany(loc => loc.Value).Select(t => t.Value));

            foreach (var tag in Cost)
            {
                switch (tag.TagOperation)
                {
                    case ENolanTagOperation.RemoveOrAppend:
                        if (dragTags.Contains(tag.Value) == false)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                            return false; // Cost tag must be present in DRAG
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, "DRAG"));
                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, "DRAG"));
                        break;

                    case ENolanTagOperation.SucceedIfPresent:
                        if (dragTags.Contains(tag.Value) == false)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                            return false; // Cost tag must be present in DRAG
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, "DRAG"));
                        break;

                    case ENolanTagOperation.FailedIfPresent:
                        if (dragTags.Contains(tag.Value))
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                            return false; // Cost tag must not be present in DRAG
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, "DRAG"));
                        break;
                }
            }

            foreach (var tag in Gain)
            {
                if (dragTags.Contains(tag.Value) == false && tag.TagOperation == ENolanTagOperation.RemoveOrAppend)
                {
                    metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, "DRAG"));
                    metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, tag.Value, "DRAG"));
                }
                else
                {
                    meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                    return false; // the gain tag is already present or invalid syntax (gain tag must be RemoveOrAppend)
                }
            }

            var statLocations = stat.Locations.Where(loc => loc.Key != "DRAG");

            foreach (var tag in Context)
            {
                string? locationName = null;

                foreach (var location in statLocations)
                {
                    if (location.Value.Any(t => t.Value == tag.Value))
                    {
                        if (locationName == null)
                        {
                            locationName = location.Key;
                        }
                        else
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                            return false; // Context tag must be present only once
                        }
                    }

                    if (tag.TagOperation == ENolanTagOperation.FailedIfPresent)
                    {
                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, location.Key));
                    }
                }

                switch (tag.TagOperation)
                {
                    case ENolanTagOperation.RemoveOrAppend:
                        if (locationName == null)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                            return false; // Context tag must be present in one location
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, locationName));
                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.RemoveTag, tag.Value, locationName ?? string.Empty));
                        break;

                    case ENolanTagOperation.SucceedIfPresent:
                        if (locationName == null)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                            return false; // Context tag must be present in one location
                        }

                        metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertTagIn, tag.Value, locationName ?? string.Empty));
                        break;

                    case ENolanTagOperation.FailedIfPresent:
                        if (locationName != null)
                        {
                            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] {});
                            return false; // Context tag must not be present in any location
                        }
                        break;
                }
            }

            foreach (var tag in Payload)
            {
                if (tag.TagOperation != ENolanTagOperation.RemoveOrAppend || string.IsNullOrEmpty(tag.Location)|| tag.Location.Equals("DRAG", StringComparison.OrdinalIgnoreCase))
                {
                    meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                    return false; // Payload tag invalid syntax (must be RemoveOrAppend, have a location that's not DRAG)
                }

                foreach (var location in statLocations)
                {
                    if (location.Value.Any(t => t.Value == tag.Value))
                    {
                        meta = new KeyValuePair<string, F3NolanRuleMeta[]>(string.Empty, new F3NolanRuleMeta[] { });
                        return false; // Payload tag must not be present in any location
                    }

                    metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AssertNoTag, tag.Value, location.Key));
                }

                metaList.Add(new F3NolanRuleMeta(ENolanRuleOperation.AppendTag, tag.Value, tag.Location));
            }

            meta = new KeyValuePair<string, F3NolanRuleMeta[]>(Next, metaList.ToArray());
            return true;
        }
    }
}
