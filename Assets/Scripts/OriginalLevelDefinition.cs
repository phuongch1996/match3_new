using System;

namespace CrownGarden
{
    [Serializable]
    public sealed class OriginalLevelDefinition
    {
        public string sourceName,sourceSha256,embeddedName;
        public int width,height,moves,boxGoal,boardCount;
        // Recovered levels keep the native color-goal counts and the power
        // carried by any special item in the initial grid.
        public int[] goalTargets;
        public bool requireObstacleClear;
        // Native ordering: index=x+width*y, with y=0 at the bottom.
        public int[] tiles,colors;
        public int[] powers;
        // Cell overlays from the native Grid.Cells table. These can coexist
        // with an item on the same cell (for example box + ice in level 6).
        public bool[] ice,jelly;
        public int[] ivy,curtain,chain,sand,border,puzzle;
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

