# KSP-BonVoyage
Rovers background processing for KSP.

# Changelog
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
