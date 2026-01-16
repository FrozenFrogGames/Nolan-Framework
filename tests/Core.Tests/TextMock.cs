using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Linq;
using FrozenFrogFramework.NolanTech;

namespace FrozenFrogFramework.Tests;

public struct TextMock
{
    private Dictionary<string, List<string>> _textbook;
    private Dictionary<string, F3NolanRouteStruct> _routes;
    public TextMock()
    {
        _textbook = new Dictionary<string, List<string>>();
        _routes = new Dictionary<string, F3NolanRouteStruct>();
    }

    public TextMock Text(string key, params string[] lines)
    {
        if (!_textbook.ContainsKey(key))
        {
            _textbook[key] = new List<string>();
        }

        _textbook[key].AddRange(lines);
        return this;
    }
    private Dictionary<string, List<string>> BuildText()
    {
        var result = new Dictionary<string, List<string>>(_textbook);
        _textbook.Clear();
        return result;
    }
    public Dictionary<string, List<string>> TextData => BuildText();

    public TextMock Route(string key, params string[] text)
    {
        FinalizeRoute();

        _currentRoute = new KeyValuePair<string, string[]>(key, text);
        return this;
    }
    private Dictionary<string, F3NolanRouteStruct> BuildRoute()
    {
        FinalizeRoute();

        var result = new Dictionary<string, F3NolanRouteStruct>(_routes);
        _routes.Clear();
        return result;
    }
    public Dictionary<string, F3NolanRouteStruct> RouteData => BuildRoute();
    private KeyValuePair<string,string[]>? _currentRoute = null;
    private string? _routeGoto = null;
    private List<F3NolanStitchStruct> _pendingStitch = new List<F3NolanStitchStruct>();
    private KeyValuePair<string,string>? _currentStitch = null;
    private List<string> _stitchContext = new List<string>();
    private List<string> _stitchCost = new List<string>();
    private List<string> _stitchPayload = new List<string>();
    private List<string> _stitchGain = new List<string>();
    private void FinalizeRoute()
    {
        FinalizeStitch();

        if (_currentRoute.HasValue)
        {
            F3NolanRouteStruct route = new F3NolanRouteStruct(_currentRoute.Value.Value);

            if (_pendingStitch.Count() > 0)
            {
                route.Flow.AddRange(_pendingStitch);

                _pendingStitch.Clear();
            }

            route.Goto = _routeGoto ?? null;

            _routes.Add(_currentRoute.Value.Key, route);
        }

        _currentRoute = null;
        _routeGoto = null;
    }
    private void FinalizeStitch()
    {
        if (_currentStitch.HasValue)
        {
            F3NolanStitchStruct stitch = new F3NolanStitchStruct(_currentStitch.Value.Key, _currentStitch.Value.Value);

            if (_stitchContext.Count() > 0)
            {
                stitch.Context = new F3NolanGameTagSet(_stitchContext.ToArray());

                _stitchContext.Clear();
            }

            if (_stitchCost.Count() > 0)
            {
                stitch.Cost = new F3NolanGameTagSet(_stitchCost.ToArray());

                _stitchCost.Clear();
            }

            if (_stitchPayload.Count() > 0)
            {
                stitch.Payload = new F3NolanGameTagSet(_stitchPayload.ToArray());

                _stitchPayload.Clear();
            }

            if (_stitchGain.Count() > 0)
            {
                stitch.Gain = new F3NolanGameTagSet(_stitchGain.ToArray());

                _stitchGain.Clear();
            }

            _pendingStitch.Add(stitch);
        }

        _currentStitch = null;
    }
    public TextMock Goto(string gotoName)
    {
        _routeGoto = gotoName;
        return this;
    }
    public TextMock Flow(string key, string next)
    {
        FinalizeStitch();

        _currentStitch = new KeyValuePair<string, string>(key, next);
        return this;
    }
    public TextMock Context(params string[] tags)
    {
        _stitchContext.AddRange(tags);
        return this;
    }
    public TextMock Cost(params string[] tags)
    {
        _stitchCost.AddRange(tags);
        return this;
    }
    public TextMock Payload(params string[] tags)
    {
        _stitchPayload.AddRange(tags);
        return this;
    }
    public TextMock Gain(params string[] tags)
    {
        _stitchGain.AddRange(tags);
        return this;
    }
}
