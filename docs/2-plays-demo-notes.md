# 2 plays demo notes

## Defensive behavior for the demo

For the initial 2-play demo, defense behavior is driven entirely by PlayData YAML reactions for the selected offensive play_number.

Currently:
- Offensive demo play: `play_number=10` ("T FAKE SWEEP R")
- Defensive scripts for play 10:
  - `DEF_RUN10_RUSH`: `rush_qb` (DL rush/pressure)
  - `DEF_RUN10_FIT`: `pursue_ballcarrier` (LB/DB flow-to-ball)

Source:
- `content/playdata/bank5_6_play_data.yaml`

Wiring:
- `MainGame.ApplyPlayDataScripts(offensivePlayNumber)` attaches scripts to defense entities by `PlayerRoleComponent.Slot`.

