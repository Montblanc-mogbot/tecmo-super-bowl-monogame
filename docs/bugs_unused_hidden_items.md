# Bugs, Unused, and Hidden Items Documentation

Source: `DOCS/BUGS_UNUSED_AND_HIDDEN_ITEMS.docx`

## Hidden/Unused Features

### Debug/Slow Motion Mode
- **Activation**: Press DOWN on control pad 7 times before pressing START at title screen
- **Pause**: Hold SELECT + A
- **Slow Motion**: Hold SELECT + B
- **Note**: These are not toggles - buttons must remain held
- **Video**: https://www.youtube.com/watch?v=YlGQebQ5h-Y

### Unused Penalty Cutscenes
Two penalty cutscreens exist but are not implemented:
- **OFFSIDES** cutscene
- **FALSE START** cutscene
- **Video**: https://www.youtube.com/watch?v=y4iEaHDEp54
- **Ref Animation**: https://www.youtube.com/watch?v=YPIw2hI4sl8

### Sound Test Menu
- **Access**: Hold B + LEFT at title screen

### Unused Music
- Extra solo section for introductory song (not used in game)

### Unused Screens
- Various partially completed screens for in-game yardage achievements

### Unused Animations
- Player "tumble animations" exist but aren't used

### Debug Routines in Bank 27
- Crude routine for viewing ALL in-game player statistics
- Crude routine for viewing current play distance

### Unused Audio
- Tiny pieces of DMC sound samples that aren't used

### Incomplete Features
- High level hooks exist for tipped pass to be caught or intercepted, but not fully developed

## Gameplay Bugs

### Player 2 Condition Bug
- **Issue**: Player 2's player ratings display incorrectly
- **Details**: Condition text (BAD, AVERAGE, GOOD, EXCELLENT) is correct, but ratings use Player 1's condition state

### Pass Overthrow Bug
- **Issue**: When QB overthrows WR and WR is >1.5 yards from ball
- **Details**: Game uses current script command instead of defender's interception rating
- **Result**: Balls bounce off high-INT defenders OR high-PC QBs get picked by low-INT DBs

### Deep Pass Defense Issue
- **Issue**: Game checks potential pass impact defenders when ball is 1/4 of way to destination
- **Requirement**: Defenders must be within 32 yards of final ball location at 1/4 point
- **Result**: Very deep passes (70-80+ yards) difficult to defend with slow QBs and fast WRs
- **Note**: Defenders appear to be running but have no impact on outcome

### Avoid Pass Block Bug
- **Issue**: If pass block goes to cutscene, QB's avoid pass block value gets overwritten
- **Result**: All QBs get 50 avoid pass block during cutscenes

### Avoid Kick Block Bug
- **Issue**: Game reads wrong location for kicker skill value
- **Result**: All kickers effectively have avoid kick block of 6 (worst value)

### Onsides Kicks
- **Issue**: Player 1 has large advantage recovering onsides kicks
- **Details**: Ball travels one less yard on an empty bar for P1
- **See**: `DOCS/onsides_kick_recovery_rates.xlsx` for data

### Lost Stats on Fumble in Endzone
- **Issue**: If player fumbles in endzone after TD, stats are lost

### Tipped Pass Safety Bug
- **Issue**: Pass blocked out of back of endzone results in safety

### Simulation Bugs

#### TE Target Bug
- **Issue**: In SKP vs SKP mode, TE hardly ever or never gets targeted

#### Punt Return Bug
- **Issue**: Game uses team's KR value instead of PR value for simulated punt returns

### Schedule Bug
- **Issue**: Teams have wildly different numbers of home and away games

## Notes for MonoGame Reimplementation

These bugs should be **fixed** in the MonoGame version:
1. Player 2 condition display
2. Pass overthrow logic (use proper INT ratings)
3. Deep pass defense (remove 32-yard check limitation)
4. Avoid pass block during cutscenes
5. Avoid kick block (read correct location)
6. Onsides kick fairness
7. Fumble in endzone stats preservation
8. Safety logic for tipped passes
9. TE targeting in sim mode
10. PR value usage in sim mode
11. Balanced home/away schedule

Unused features to consider implementing:
- Penalty cutscenes (OFFSIDES, FALSE START)
- Tumble animations
- Yardage achievement screens
