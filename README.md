# PockyBum522's NetDaemon Apps
These are the current, used way more than daily home automations for my home. 

I do all of my automations in Home Assistant with NetDaemon. It's extremely nice to do complex automations with as I was already familiar with C#. Being able to work in something I'm used to for home automation has been quite enjoyable.

## Apps

* DoorsLockAfterTimePeriod 
  * Allows automatic locking of my August locks beyond what I can do in the August app itself. 
  * Adding time every time the door opens so that the lock will take even longer to lock again if it looks like you're using the door frequently
  * Textboxes in the HA frontend that allow you to disable either the front or back door locks for X amount of hours before they start auto-locking again
  * Disabling a lock if it isn't locking within a few minutes of being told to do so, and sending a notification so that someone can fix it (Usually caused if a door is ajar slightly)


* FrontDoorCameraMotion
  * Takes a picture from a camera and sends it as a notification to users
  * Watches MQTT for a message from a Blue Iris instance so it knows when motion is happening


* FrontDoorNfcReader
  * Watches for a UDP packet sent from a PoE Arduino with a NFC reader attached and verifies UID for authentication
  * Notifies on tag found/door unlocked as well as unauthorized UID scanned so that adding new tags is simple
  * Yes, UID match is insecure for this, but if someone wanted to break into my house there's a window next to the front door they could put a brick through and reach through to unlock the front door


* Kitchen Lights Controller
  * Allows to set various scenes for the kitchen lights using a z-wave switch with 5 buttons on it
  * Two of the buttons will modify brightness up/down for any scene on the lights, even if it was not set by this code


* Routines
  * Allows for special events in my house, like when I want to exercise, or leaving the house/coming home
  * Very unfinished

* Scheduled Lights
  * Handles turning some lights on just before sunset and back off in the morning
    

* ThermostatInitializer
  * Handles setting the thermostat after a HA reboot to something helpful based on the outside temperature


Developer Tools: Useful tools such as an event logger similar to what's in HA dev tools


Utilities
  * Notification helpers
  * Extension methods
    * Mapping a number from one range to another, like Arduino's map()
  * Weather helpers
  * HA Group helpers


## Issues

  * DoorsLockAfterTimePeriod:
    * Auto-locking code was just reworked and I need to add in handling so that when a lock is disabled via HA front end textbox it doesn't think the lock is unable to lock for real-world reasons.


  * KitchenLightsController
    * Brightness modification is slow. Considering updating light state locally so that it can be modified with rapid button presses and then sent out to all lights until it can be verified they match the correct new state.


  * Scheduled Lights
    * Wasn't handling DST. I believe I fixed that, but haven't tested it through a new DST change.
