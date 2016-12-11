# KSP-BonVoyage
Rovers background processing for KSP.

# Changelog
## 0.11.1
- Fixes for KSP 1.2.2
- Added "Close" button to main window
- Toolbar button won't appear in editors

## 0.11.0
- New part, created by [Enceos](http://forum.kerbalspaceprogram.com/index.php?/profile/110725-enceos/)
- Icon is now colorized, made by [Madebyoliver](http://www.flaticon.com/authors/madebyoliver) and licensed under [Creative Commons BY 3.0](http://creativecommons.org/licenses/by/3.0/)
- Moved BV part to Space Exploration science node, where RoveMax Model S2 is
- Parts that can contain BV module are not hardcoded anymore
- Duplicate parts on the same vessel are ignored
- Code cleanup
- KSP skin for UI available
- Dewarp done in two steps: instant to 50x and then gradual to 1x
- Solar powered rovers will idle when Sun is 0 degrees above the horizon, no more stucking at poles
- Serious average speed penalties at twighlight and at night time for manned rovers
- Some colors in arrival report
- Added toolbar support, fixed wrapper, no Contract Configurator conflict :D
- Switching to vessel with interface button will go without scene reload if vessel is already loaded
- 80% speed penalty for unmanned rovers
- UI and label are hidden if game is paused
- Label is hidden when all hidden (F2)
- Fixed crazy torpedoes nuking active vessel, rover simply won't move closer than 2400 meters to active vessel
- Fixed argument out of range caused sometimes and rover voyage end
- Route visualized with red line
- Route always visualized for active rover if exists
- Target can be set to active navigation point
- Added support for USI nuclear reactors
- Added support for NFE fission reactors

## 0.10.2
- Recompile for KSP 1.2
- Fixed last waypoint being last step of voyage

## 0.10.1
- Fixed control lock being applied to next switched vessel
- Moved "Bon Voyage control lock active" message higher on screen
- SAS is blocked by control lock too
- Path markers displayed at correct positions
- Fixed trying to build path to target closer than 1000 meters
- Fixed "yet to travel" distance for rovers awaiting sunlight being incorrect after scene switch
- Current rover changes in GUI list on vessel switch
- Fixed distance to target being incorrect if path is not staright
- Fixed errors raised at rover journey end when no/low time acceleration
- Fixed error switching to rover from Space Center
- Added ARES and Puma support

## 0.10.0
- Fixed BV controller part being not in Control tab
- Shut down wheels are not treated as power consumers
- At least two wheels must be on to start BV
- Fixed utilites not being shown
- Added "Calculate average speed" and "Calculate power requirement" utilities
- Power production requirement diminished to 35% of powered wheels max
- Average speed now varies according to number of wheels on: 2 wheels - 50% of wheels' max speed, 4 wheels - 60%, 6 and more - 70%
- Rovers driven away from KSC by BV are not treated as landed at runway or launchpad anymore

## 0.9.9.10
- Fixed errors in editor
- Fixed rover altitude being incorrect
- Added Malemute and Karibou compatibility
- Code optimization
- Pathfinding fully functional
- Interface revamp
- Module Manager patch to add BonVoyage to Malemute, Karibou and Buffalo cabs
- Fixed version file

## 0.9.9.9
- Initial public release
