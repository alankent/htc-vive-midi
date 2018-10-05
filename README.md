HTC Vive Controller to MIDI
===========================

This Windows command line utility (written in C#) reads from a pair of [HTC Vive](https://www.vive.com/us/) hand controllers and sends [Adobe Character Animator](https://www.adobe.com/products/character-animator.html) MIDI events for buttons clicks (as MIDI notes) and positional data (as MIDI controller values). This allows you to use body movements to control both arms in parallel during recordings (instead of using draggers and mouse). See this [quick demo video](https://youtu.be/ovXXki5kWu4) for more information.

Currently the headset is not used - just the hand controllers. There is also no support for additional Vive trackers, which would be nice to attach to legs etc for controlling more of the body. These could be added, but are not supported at this stage.

This utility is currently provided as source code only. Its really intended for developers who want to take the tool further. It is released under MIT license for this purpose.

I believe other VR headsets (e.g. Oculus) also have APIs, so it should be possible to adapt this code for those devices as well, but warning: this code is not very elegant. I built it as a quick proof of concept, not production code.

## Puppet Setup

This section is to give a feel for the effort required to get an existing puppet set up to use this new control approach.

### Facial Expressions

I normally create triggers for different eye expressions (angry, sad, surprised) and mouth expressions. I create these independently, because different combinations give you more expressions (angry eyes plus smiling face = evil grin).

I then associate the left and right touchpads (more below) so one controls the mouth, the other controls the eyes. I currently divide the touchpad into 9 zones (N, NE, E, SE etc plus center), numbered like a phone keypad (1 to 9). Using the touchpad you can only make one of the 9 selections at a time, so putting all mouth positions on one touchpad and all eye expressions on the other works well for me.

Note: you can touch or press the touchpad, so you can have two forms of angry (e.g. press harder for extra angry). I have used "N" (up) for raised eyebrows (surprised) and "S" (down) for angry (lowered eye brows). I have not used all 9 positions yet. Having reminders like these help when doing a recording since you have to do both hands at once.

### Hands

For the hands, the controller detects rotation of the controllers you are holding. It assumes you have 4 views of the hands. The palm of the hand, the back of the hand, then the side of the hands (front and back). Imagine the default character pose with arms out sideways, like you were pretending to be an aeroplane. I call that "palm down". If you lift up your hand to wave at someone, then that is the palm facing forwards. If you were doing a fist pump, you probably will have the backs of your hands outwards. Then the final position is a side view from the back of your hand (e.g. if you cross your arms). You don't have to draw all 4 views, but if you do, you can hook triggers up to them. Rotating the hand controllers automatically picks the right trigger.

In addition to the 4 palm rotations (down, towards screen, up, away from screen), I use the hand controller main trigger button as an additional trigger. I combine this with the rotation triggers and draw 8 hand positions - the 4 palm views with hands open and 4 palm views with hands closed. I then set the hand hierarchy in the puppet up as follows:

    Left Hand Positions (I add a "Transform" behavior here)
        Left Palm Down
            Left Palm Down Open
            Left Palm Down Closed (fist)
        Left Palm Forwards
            Left Palm Forwards Open
            Left Palm Forwards Closed
        Left Palm Up
            Left Palm Up Open
            Left Palm Up Closed
        Left Palm Backwards
            Left Palm Backwards Open
            Left Palm Backwards Closed

I then put the 4 children of "Left Hand Positions" into a swapset and assign triggers. I then create a second swapset for the children of "Left Palm Down" for the open and closed hand positions. I then add the children of the other 3 palm rotations to the same triggers. That is, you end up with a single "Open/Close" trigger swap set with two children ("open" and "closed") but "open" is connected to "Left Palm Down Open", "Left Palm Forwards Open", "Left Palm Up Open", and "Left Palm Backwards Open". The palm rotation trigger then picks the right palm to use, and the trigger picks the open/closed within the palm view.

TODO: The trigger can detect how far the trigger is squeezed, so theoretically it should be possible to have 3 or 4 open to closed hand positions (e.g. open, pointing and closed) that depend on how far you squeeze the trigger.

Finally, for the hand position controls to work, you need to add a Transform behavior to each of the two the hands. Doing so will disable draggers (they stop working when there is a Transform behavior at present). The HTC Vive controllers send out a continuous stream of X/Y position events that this utility converts to MIDI controller events. These X and Y position events get bound to the the hand Transform behaviors (describe later). You also need to bind the Rotation property of the Transform up as well so the angle you hold the controllers affects the angle of the puppet hands. This allows you to wave by flexing your wrist.

### Other Triggers

The controller also has an application menu button and a grip hand button that you can hook up to other special triggers.

So all up, the only unusal set up structure is the hands to make it work with the palm rotation triggers and the transform behavior on the hands. I describe how to bind the puppet to MIDI events etc later.

## Compliing the Code 

To compile the code, you need to download two other projects as well.

 - [Sanford.Multimedia.Midi](https://github.com/tebjan/Sanford.Multimedia.Midi) for generating MIDI events 
 - [OpenVR](https://github.com/ValveSoftware/openvr) for connecting to the HTC Vive to get position and button data

I just added the above two directories to the project and compiled it all up together. The only trick was the OpenVR instructions explain how to include a provided in the distribution that the C# code calls (I used the 32 bit version).

## MIDI Set Up 

Normally Windows comes with a single built in MIDI device to play simple sounds. If you plug in external MIDI devices (like an electronic music keyboard), additional devices may be available on your machine. If you run this utility with no arguments it will print out the availble devices on your machine and their device-ids. The Sanford.Multimedia.Midi project above sends messages to MIDI devices.

To get Adobe Character Animator to see MIDI events, you need a MIDI device to send a message. The Sanford.Multimedia.Midi library does not support this - registering new MIDI devices is a low level operating system API for device drivers to use. To get around this, use one of the following to programs:

 * [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html) - creates a loopback device on the local machine. It receives MIDI events and writes them directly back out again, which Character Animator will then see and process.

 * [rptMIDI](http://www.tobias-erichsen.de/software/rtpmidi.html) - sends MIDI events to another machine over the network. The protocol used is compatible with Macs. This allows the HTC Vive to be connected to one machine with Character Animator running on a second machine. Theoretically you should be able to have a Windows machine running this utility and the HTC Vive controllers, then send MIDI events to a Mac running Character Animator.

Once you run one of the above, you will have a local device to point this utility at.

## Command Line Arguments

There are a number of different command line arguments. (Please check out the utility directly - I may make changes and forget to keep the following up to date.)

        > HtcMidi.exe --help
        MIDI Device Count = 3
        
        Usage: HtcMidi.exe [options]

        Options:
        -d|--device <int>     MIDI device number (defaults to largest device id)
        -c|--channel <int>    MIDI channel number (1)
        -f|--fps <int>        Frames per sec (8)
        -x|--min-x <float>    Min X value (0.0)
        -X|--max-x <float>    Max X value (1.8)
        -y|--min-y <float>    Min Y value (0.8)
        -Y|--max-y <float>    Max Y value (1.8)
        -rha|--right-hand-angle <int>   Default angle of puppets right hand (220)
        -lha|--left-hand-angle <int>    Default angle of puppets left hand (140)
        -t|--test             Generate a selection of synthentic test data (off)
        -n|--note <string>    Send a single note for the given note/button (off)
        -C|--controller all|none|lx|ly|la|rx|ry|ra   Only send specifed controller data (all)
        
        e.g. HtcMidi -lha 90 -rha 90 --test
             HtcMidi --note lamb   (send left application menu press)
        
        Available MIDI devices:
        0: Microsoft GS Wavetable Synth
        1: loopMIDI Port
        
        Note names can be note numbers '60' (C4), 'G#3', or button names
        from the following list.
        
        lamb (C2 24) - Left Application Menu Button
        ltrb (C#2 25) - Left Trigger Button
        lgrb (D2 26) - Left Grip Button
        lnpf (F2 29) - Left Palm Forwards
        lnpd (F#2 30) - Left Palm Down
        lnpb (G2 31) - Left Palm Backwards
        lnpu (G#2 32) - Left Palm Up
        ltt1 (C3 36) - Left Touchpad Touch 1 NW
        ltt2 (C#3 37) - Left Touchpad Touch 2 N
        ltt3 (D3 38) - Left Touchpad Touch 3 NE
        ltt4 (D#3 39) - Left Touchpad Touch 4 W
        ltt5 (E3 40) - Left Touchpad Touch 5 0
        ltt6 (F3 41) - Left Touchpad Touch 6 E
        ltt7 (F#3 42) - Left Touchpad Touch 7 SW
        ltt8 (G3 43) - Left Touchpad Touch 8 S
        ltt9 (G#3 44) - Left Touchpad Touch 9 SE
        ltp1 (C4 48) - Left Touchpad Press 1 NW
        ltp2 (C#4 49) - Left Touchpad Press 2 N
        ltp3 (D4 50) - Left Touchpad Press 3 NE
        ltp4 (D#4 51) - Left Touchpad Press 4 W
        ltp5 (E4 52) - Left Touchpad Press 5 0
        ltp6 (F4 53) - Left Touchpad Press 6 E
        ltp7 (F#4 54) - Left Touchpad Press 7 SW
        ltp8 (G4 55) - Left Touchpad Press 8 S
        ltp9 (G#4 56) - Left Touchpad Press 9 SE
        ramb (C5 60) - Right Application Menu Button
        rtrb (C#5 61) - Right Trigger Button
        rgrb (D5 62) - Right Grip Button
        rnpf (F5 65) - Right Palm Forwards
        rnpd (F#5 66) - Right Palm Down
        rnpb (G5 67) - Right Palm Backwards
        rnpu (G#5 68) - Right Palm Up
        rtt1 (C6 72) - Right Touchpad Touch 1 NW
        rtt2 (C#6 73) - Right Touchpad Touch 2 N
        rtt3 (D6 74) - Right Touchpad Touch 3 NE
        rtt4 (D#6 75) - Right Touchpad Touch 4 W
        rtt5 (E6 76) - Right Touchpad Touch 5 0
        rtt6 (F6 77) - Right Touchpad Touch 6 E
        rtt7 (F#6 78) - Right Touchpad Touch 7 SW
        rtt8 (G6 79) - Right Touchpad Touch 8 S
        rtt9 (G#6 80) - Right Touchpad Touch 9 SE
        rtp1 (C7 84) - Right Touchpad Press 1 NW
        rtp2 (C#7 85) - Right Touchpad Press 2 N
        rtp3 (D7 86) - Right Touchpad Press 3 NE
        rtp4 (D#7 87) - Right Touchpad Press 4 W
        rtp5 (E7 88) - Right Touchpad Press 5 0
        rtp6 (F7 89) - Right Touchpad Press 6 E
        rtp7 (F#7 90) - Right Touchpad Press 7 SW
        rtp8 (G7 91) - Right Touchpad Press 8 S
        rtp9 (G#7 92) - Right Touchpad Press 9 SE


The most likely arguments you will use are as follows

### Device ID

`--device` is used to specify which MIDI device to send events to. This defaults to the largest device id available on the assumption the last device you installed was loopMIDI or rptMIDI.

### Update Rate

`--fps` is the target frames per second for the animation, which is the rate positional events will be sent to Character Animator. 24 is high accuracy, but my laptop could not keep up so the default is set to 8. With the webcam on in Character Animator I had to set this to a value like 4. Higher numbers will result in smoother motions.

### Movement Bounding Box

`--min/max-x/y` is the min and max raw values generated by the HTC Vive position values. The default values worked well for the desk I was sitting at - you will almost certainly have to adjust these values for your setup based on where you plan to stand. The values are in meters. Values in this range are scalled to MIDI values in the range 0 to 127.

To work out what the min and max values are, start the utility up with `--fps 1`. At one frame per second it outputs more detailed debugging information so you can see the X and Y values. Point your arms up, down, left, and right. Write down the minimum and maximum X and Y values you see. These are the values you then provide to `--min-x` etc. Use ^C to abort the utility and start it up again with new X/Y bounding box values. You *cannot* go beyond these values, so feel free to use values slight beyond what you observe, so you can stretch further if needed. You could just use 0 and 2, but it reduces the accuracy of position data as you end up only using a subset of the available 0 to 127 integer coordinate space.

TODO: It might be better if min/max were autocalibrated - e.g. have the code work out the max X/Y values it sees you use, and adjust the values on the fly. Then you just wave your arms around at start up to recalibrate the software. But the problem with this is if you walk over to your computer, it will change the adjustments. So I have stuck with manual calibration for now.

### Hand Rotation Angles

`-lha` and `-rha` tell the utility the starting angle of the puppet's left and right hands in degress in the artwork. (Zero is up, 180 is down.) In order to compute the rotation value to use, the default hand angle in the artwork must be known. If you get it wrong, the hands will always look slight bent.

The right hand of the puppet defaults to 220 (40 degress from down) and the left 140 (40 degress the other direction from down). If your puppet default position has hands directly out sideways, you should use 270 and 90 instead.

If you use the `--test` option, it generates a series of test data where the two arms are moved around in a circle, with the angles set appropriately. Just use ^C to stop the command when done (it loops forever). I find this useful to test the hand hands. You don't need the HTC Vive controllers in test mode.

* `all|none|notes|lx|ly|la|rx|ry|ra` specifies which controller events to emit (more below).

## Binding Controllers Positional Data to Character Animator 

First, be aware the word "control" has multiple meanings - make sure you have the right one in mind. Specifically

 - The HTC Vive also has two hand controllers that we want to track.
 - There are MIDI "control" events which allow numeric values in the range 0 to 127 to be sent. For example, a volume control, an reverb control, etc.
 - Inside Character Animator there is a "control" panel that allows you to bind MIDI events to triggers and behavior properties.

It is just a co-incidence that all three, which need to be linked up, have all used the word "control" or "controller". Sorry if the following gets confusing!

To bind MIDI note (triggers) and control (position and rotation) values through to Character Animator controls, enter "rig" mode for your puppet, open up the "Control" panel, click on "Layout" so you can edit the controls visible. Drag the behavior properties for Position X, Position Y and Rotation of the left and right hand behaviors to the Control panel. This will give you sliders for X and Y and a knob for rotation. Also add all the triggers. The triggers do not need keyboard shortcuts, but they can be handy for debugging.

### Binding Triggers

This utility sends all "triggers" as MIDI notes. The 4 palm rotations and touchpad touches are also sent as triggers (in addition to the trigger button, application menu button, and grip button).

If you are using MIDI channel 1 (the recommended default), then in the "Trigger" panel in Character Animator, there is a "MIDI Note" field you can type the note number directly into. (Currently there is not a channel number field for some reason.) That means you can bind triggers up by using the table printed out in the help message at startup. For example,

        ltt1 (C3 36) - Left Touchpad Touch 1 NW

This says that MIDI note 36 (which is "C3" on a MIDI keyboard) is the note sent when you touch the puppet left hand touchpad (the controller you hold in your right hand) in the top left (NW) corner. If you have a MIDI keyboard, you can press the C3 note. So you can bind up these triggers by typing in "36" into the MIDI Note field. I found this the quickest approach myself.

But you can instead do bindings in the documented way, which is to enter "Layout" mode in Character Animator, click on the trigger you want to bind, then perform the action to generate the desired MIDI event. BUT!!! The problem is in the default mode of operation, the utility is sending a continuous stream of X/Y/rotation data for both hands. So these will get bound to the button instead. To overcome this you can do one of two things.

 - Run the utility with `--controller none` which will tell the utility to output no control event data.
 - Run the utility using `--note ltt1` (or `--note C3` or `--note 36`) which will make the utility send a single note and then exit.

Once you have bound the trigger up, you can flip back to "Performance" mode in the "Control" panel, or click outside the trigger to deselect it.

### Binding Position and Rotation Data

Binding the Position X, Position Y, and Rotation properties of the hand Transform behaviors is a little tricky. There is no "Note ID" field you can type MIDI note numbers into, plus I am using "MIDI Control" events (not note events) as well. Control events in MIDI are for numeric data like volume levels.

Because the utility sends a constant stream of Position X, Position Y, and Rotation data (even when putting the controllers on my desk, the slightest movement would register as a change in position), you have to use the `--controller` command line option to only enable one control event at a time.

Go into the Character Animator control panel. If not done already, drag the Position X, Position Y, and Rotation behavior properties for the two hand Transforms into the panel. Go into "Layout" mode. Click on the first control to bind, e.g. the puppet left hand Position X control to select it. Then start up the utility with `--controller lx`. You must have the HTC Vive controllers turned on and available to get this data. When an event arrives, the color of the control will change and the MIDI information will be displayed on the control as well. Quit the utility (using ^C) to stop sending event data and deselect the control.

Repeat for all of the 6 controls to be bound (`lx`, `ly`, `la`, `rx`, `ry`, `ra`). You need to exit and restart the utility for each.

TODO: Maybe add a new command line option to send dummy control events so you can bind these controls more easily, like triggers and notes.

### Calibrating Position and Rotation Data

Once you have the bindings done, you need to adjust the ranges on the controls in the Character Animator control panel. These are the two numbers in the bottom left and right corners (they are min and max values you can adjust). The MIDI 0 to 127 numbers are scaled to these ranges.

I selected the following values for one of my puppets.

 - Right Position X: -500 to 3500
 - Right Position Y: -2700 to 300
 - Right Rotation: 0 to 360
 - Left Position X: -3500 to 500
 - Left Position Y: -2700 to 500
 - Left Rotation: 0 to 360

These numbers change per puppet because the starting position of the hands in the artwork matters. The X range for both hands should be the same (4000 in the above case), but the default resting position of my puppet hands are around 3000 apart, hence the offsets. Similarly the Y values, the default resting position of the hands was quite low on the page (and Y increases down the page).

Once you have it all set up, run the utility with the `--test` option to generate synthetic data that moves both hands around in a circle, computing the rotation values as well.

Getting this calibration right I found was the most time consuming part of the whole experience.

TODO: It would be better to have some calibration mode where you could click certain reference points and have the system work out the numbers for you.

## Future Work 

Some ideas for future work are as follows.

 - This is proof of concept code - its not very clean or elegant.
 - It is currently a command line utility. It would probably be better to have a GUI allowing buttons instead of different command line switches.
 - The touchpads could be used to generate positional data for eye gaze. Unfortuantely there is no MIDI control for eye gaze in Character Animator, so webcam is still probably best approach for eyes. 
 - The trigger also has X-axis positional data, meaning instead of on/off, it could be used to select on of several hand positions such as open, closed a bit, pointing, fully closed. 
 - The number of events could be reduced by sending positional/rotation events only if values change. I doubt it would help much in practice as it is very hard to stand complete still. This mode would help if small movements were supressed.

## Final Remarks

This code is currently experimental - use at own risk. It is released under MIT license so you can use it on your own projects.

For my "Sam" puppet, I used the following;

      HtcMidi.exe -rha 260 -lha 100 --min-y 0.2

Want to discuss? Submit a ticket to this GitHub or find me in the [Adobe Character Animator forums](https://forums.adobe.com/community/character-animator).
