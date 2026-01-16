using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Linq;
using FrozenFrogFramework.NolanTech;

namespace FrozenFrogFramework.Tests;

public struct StatMock
{
    public FrozenFrogFramework.NolanTech.F3NolanStatData Data => Build();
    private F3NolanStatData Build()
    {
        F3NolanStatData result = new F3NolanStatData(_pending.ToArray());

        _pending.Clear();
        return result;
    }

    private List<KeyValuePair<string, F3NolanGameTagSet>> _pending;
    public StatMock()
    {
        _pending = new List<KeyValuePair<string, F3NolanGameTagSet>>();
    }
    public StatMock Mock(string name, params string[] tags)
    {
        _pending.Add(new KeyValuePair<string, F3NolanGameTagSet>(name, new F3NolanGameTagSet(tags)));

        return this;
    }
}
