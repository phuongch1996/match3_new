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
        public OriginalRefill[] predefined;
    }
    [Serializable]
    public sealed class OriginalRefill
    {
        public int x,y;
        public int[] ids;
    }
}
