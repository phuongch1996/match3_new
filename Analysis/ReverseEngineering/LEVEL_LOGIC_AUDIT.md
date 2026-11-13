# Level logic audit

## 1. Item falling and obstacle falling

- Normal items fall straight inside each active column segment.
- Empty cells caused by a match are refilled from the top.
- A second pass supports diagonal cross-fall into holes beside a blocked or missing cell.
- Inactive cells from the native grid remain holes and are excluded from swaps, matches, gravity and refills.
- Static obstacles block gravity. Cell overlays such as Ice, Jelly, Ivy, Curtain, Chain, Sand and Puzzle remain on the cell while items move through the board.
- Column and MetalColumn footprints stay grouped and do not fall as normal jewels.

## 2. Special item creation and special combinations

- Three in a line clears the line.
- Four in a line creates a horizontal or vertical rocket.
- Five in a line creates a rainbow.
- T and L shapes create a bomb.
- A 2x2 square creates a propeller, including a square with extra same-color tiles outside the square.
- A swap passes the destination cell as the preferred creation cell.
- Rocket plus rocket clears both lines. Bomb plus rocket clears a three-wide cross. Bomb plus bomb clears a 5x5 area. Rainbow combinations convert eligible ordinary items and then activate them. Propeller combinations use a remote target and carry the partner payload.

## 3. Obstacle characteristics

- Layered obstacles, grouped walls, Columns, MetalColumns and native multi-cell obstacles retain their layers and shared groups.
- Native item variants are retained for Lyre, Target, Gem, MagicOrb, Magnet, Cannon, Lily, Volcano, Lightbulb, Armory, JellyMaker, Tablet, MagicGlobe, Barrel, MagicCards, Starfish, Dragon and FireDragonEgg families.
- Cell overlays are loaded from Grid.Cells and can coexist with an item on the same cell. Level 6 therefore keeps 9 wood boxes and 32 ice cells.
- Overlay layers are damaged when a match or power reaches the cell and count toward recovered-level completion.
- JellyMaker and Volcano are currently represented as static native obstacles. Their exact turn-based spawning of new obstacles still needs native runtime confirmation before adding automatic generation rules.

## 4. Map creation and missing cells

- Width, height, active mask and board IDs are loaded from the recovered level envelope.
- Missing cells remain inactive holes. They are not filled with random jewels and do not become swap targets.
- All 2,300 adapters have complete arrays for tiles, powers, active cells and cell overlays.

## 5. Items and obstacles in one level

- The adapter preserves base item IDs and independent cell overlays, so one level can combine boxes, ice, native obstacles, special items, holes and color goals.
- A scan found 932 levels containing both a base static item and at least one cell overlay.
- The level 6 case was checked directly against the source envelope and adapter.

## Validation

- 2,300 adapter files were scanned.
- Every overlay array has width*height entries.
- Overlay totals are 10,471 Ice, 861 Jelly, 6,440 Ivy, 5,642 Curtain, 1,960 Chain, 0 Sand, 3,463 Border and 1,278 Puzzle cells.
- Unity Play Mode was not run during this audit. The data and runtime source were checked statically.
