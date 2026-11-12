using System;

namespace CrownGarden
{
    [Serializable]
    public sealed class OriginalLevelDefinition
    {
        public string sourceName,sourceSha256,embeddedName;
        public int width,height,moves,boxGoal,boardCount;
        // Native ordering: index=x+width*y, with y=0 at the bottom.
        public int[] tiles,colors;
        // Optional native board mask in the same x+width*y ordering. A false
        // entry is a missing board cell: it cannot match, move, fall through,
        // receive a refill, or host an obstacle. When omitted, tile id 0 is
        // treated as a missing cell for compatibility with compact level data.
        public bool[] active;
        public OriginalRefill[] predefined;
    }
    [Serializable]
    public sealed class OriginalRefill
    {
        public int x,y;
        public int[] ids;
    }
}
