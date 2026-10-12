# R3 hero artwork provenance

The three portrait artworks came from one Codex image-generation set. The actual PNG exports were rendered by `Backdrop.BackdropRenderer.RenderFiles` from the installed Backdrop assembly. A temporary helper outside the project called that renderer. It did not change product source or saved app settings.

## Generated source artworks

| File | Generated source | SHA-256 |
| --- | --- | --- |
| `stairway.png` | set `01a108ef-f136-7b31-8095-3450efa477f8`, `exec-6314bd0a-56ab-456f-aa29-2d4f498e5015.png` | `70C73764542822D7A2B03374BB49BDF80786B4710E873EF88E880053AC5617E6` |
| `arch.png` | set `01a108ef-f136-7b31-8095-3450efa477f8`, `exec-868b15fa-6a3e-49f6-942f-e51df8722c65.png` | `843CD9429DA47C262D9E99A244E0B6C65F9EF9C3501346FDDE0767CA3DDBC768` |
| `sphere.png` | set `01a108ef-f136-7b31-8095-3450efa477f8`, `exec-0b3b4cc6-185c-4b54-84b4-8dfcb90c07fa.png` | `54E809844A50D3B6EE3A4C0525763777D877BF5054986D794DC2CC1A8DC9B0F6` |

## Renderer settings and outputs

All three exports use the same three images, 1920 × 1080 canvas, Row layout, 7% image padding, and 18% shadow. The automatic gradient uses the Backdrop default and its 20% lightening. The solid variant uses `#D7E5F5`. The dot variant uses `#F4E6DB`.

| File | SHA-256 |
| --- | --- |
| `exports/gradient.png` | `2840A6081ACD84C57C3A0A9C3B5A3B720BFE31EE4558C79EE5A8BCDE12089F8E` |
| `exports/solid.png` | `01BF1107C4ECE550E24E29DC75FE78CB104A3578FEE8F8168FA6A71499A8DAE5` |
| `exports/dots.png` | `34AC1CABA64AE9B1C4796C75B3A56F943D2D2EC66CC658734858F336C7BCEED0` |
