using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Linq;
using FrozenFrogFramework.NolanTech;

namespace FrozenFrogFramework.Tests;

public struct RulebookMock
{
    public FrozenFrogFramework.NolanTech.F3NolanRuleData[] Data => Build();
    private F3NolanRuleData[] Build()
    {
        FinalizeMock();

        F3NolanRuleData[] result = _rulebook.ToArray();

        _rulebook.Clear();
        return result;
    }

    private string? _matchTag;
    private string? _textKey;
    private List<string> _contextTags;
    private List<string> _costTags;
    private List<string> _payloadTags;
    private List<string> _gainTags;
    private bool _isDrag = true;

    private List<F3NolanRuleData> _rulebook = new List<F3NolanRuleData>();
    public RulebookMock Mock(string tag, string? text = null)
    {
        FinalizeMock();

        _matchTag = string.IsNullOrEmpty(tag) ? null : tag;
        _textKey = text;
        return this;
    }
    private void FinalizeMock()
    {
        if (_matchTag != null)
        {
            _rulebook.Add(new F3NolanRuleData(
                new F3NolanGameTag(_matchTag),
                new F3NolanGameTagSet(_contextTags),
                new F3NolanGameTagSet(_costTags),
                new F3NolanGameTagSet(_payloadTags),
                new F3NolanGameTagSet(_gainTags),
                _isDrag,
                _textKey ?? string.Empty
            ));

            _matchTag = null;
            _textKey = null;
        }

        _contextTags.Clear();
        _costTags.Clear();
        _payloadTags.Clear();
        _gainTags.Clear();

        _isDrag = true;
    }

    public RulebookMock()
    {
        _matchTag = null;
        _textKey = null;
        _contextTags = new List<string>();
        _costTags = new List<string>();
        _payloadTags = new List<string>();
        _gainTags = new List<string>();
        _isDrag = true;
    }

    public RulebookMock Context(params string[] tags)
    {
        _contextTags.AddRange(tags);
        return this;
    }
    public RulebookMock Payload(params string[] tags)
    {
        _payloadTags.AddRange(tags);
        return this;
    }
    public RulebookMock Cost(bool fromDrag = true, params string[] tags)
    {
        _isDrag = fromDrag;
        _costTags.AddRange(tags);
        return this;
    }
    public RulebookMock Gain(params string[] tags)
    {
        _gainTags.AddRange(tags);
        return this;
    }
}
