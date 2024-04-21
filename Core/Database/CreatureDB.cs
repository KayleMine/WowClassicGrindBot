﻿using System.Collections.Frozen;

using SharedLib;

using static System.IO.File;
using static System.IO.Path;
using static Newtonsoft.Json.JsonConvert;

namespace Core.Database;

public sealed class CreatureDB
{
    public FrozenDictionary<int, string> Entries { get; }

    public CreatureDB(DataConfig dataConfig)
    {
        Creature[] creatures = DeserializeObject<Creature[]>(
            ReadAllText(Join(dataConfig.ExpDbc, "creatures.json")))!;

        Entries = creatures
            .ToFrozenDictionary(c => c.Entry, c => c.Name);
    }
}
