HTC Vive Controller to MIDI
===========================

This Windows command line utility reads from a pair of HTC Vive controllers and
sends MIDI events when buttons are clicked, plus positional data (as MIDI
controller values).  This is designed to work with Adobe Character Animator so
you can use the HTC Vive controllers to control the hands of puppets, plus use
some of the HTC Vive controller buttons as triggers.

Currently the headset is not used - just the hand controllers. There is also
no support for additional Vive trackers, which would be nice to attach to
legs etc for controlling more of the body.

## Compliing the Code 

I don't know the right way to set up a C# project in Visual Studio. Advice
appreciated! You do need two other projects to compile up the code I provide.

 - Sanford.Multimedia.Midi for generating MIDI events (https://github.com/tebjan/Sanford.Multimedia.Midi.git)
 - OpenVR for connecting to the HTC Vive and getting position and button data (https://github.com/ValveSoftware/openvr.git)

The only trick was the OpenVR instructions talked about adding a DLL (I used
the 32 bit version). The C# code calls into the DLL.

## MIDI Set Up 

Normally Windows has a single MIDI device, which is a built in device to
play some simple noises. If you plug in external MIDI devices, additional
devices may be available on your machine. If you run this utility with no
arguments it will print out the availble devices on your machine and their
device-ids.

This project works by sending MIDI events to a MIDI device. It does not create
a MIDI device. Adobe Character Animator however only picks up events generated
by a MIDI device. To get around this, use one of the following to programs:

 * loopMIDI - creates a loopback device on the local machine. It receives MIDI
   events and writes them directly back out again, which Characte Animator will
   then see and process.

 * rptMIDI - sends MIDI events to another machine over the network. The
   protocol used is compatible with Macs. This allows the HTC Vive to be
   connected to one machine with Character Animator running on a second
   machine.

Once you run one of the above, you will have a local device to point this
utility at.

To run this utility, you must supply a number of command line arguments:

      HtcMidi.exe <device-id> <midi-channel> <fps> <min-x> <max-x> <min-y> <max-y> <puppet-right-hand-angle> <pupplet-left-hand-angle> htc-vive|test all|none|notes|lx|ly|la|rx|ry|ra

The arguments are as follows:

* `<device-id>` is the integer number of the MIDI device id to send the MIDI messages to.
* `<midi-channel>` the midi channel number to use. 1 should be fine for most cases.
* `<fps>` is the target frames per second for the animation, which is the rate positional events will be sent to Character Animator. 24 is high accuracy, but it it overloads your system back it off to something like 8.
* `<min/max-x/y>` is the min and max raw values for actual values you want to use.
* `<puppet-left/righ-hand-angle>` is the starting angle of the puppets left and right hand. Rotation of zero will result in the hand at this angle. In order to compute the rotation value to use, the default hand angle in the artwork must be known.
* `htc-vive|test` specifies whether to use the HTC Vive drivers, or some test code that synthetically creates events. This is useful for testing if you don't have a HTC Vive handy.
* `all|none|notes|lx|ly|la|rx|ry|ra` specifies which controller events to emit (more below).

## How to calibrate min/max X/Y 

There first is the question of how to work out the min/max-x/y values.
The tool uses the min/max X/Y values to scale positional values into the range
0 (representing the min values) to 127 (representing the max value).

I suggest starting up the program with "all" and min/max values of 0 and 1.
(It does not really matter what they are.) Set "fps" to 1. Then start the
program up. With a low frame rate it will print the left and right controller
X/Y values to the screen in the original coordinate system (which is measured
in meters).

Move the controllers around and note the minimum and maximum values you
encounter for X/Y (the floating point numbers printed to the screen).
(Ignore the integer numbers displayed after the floating point numbers.)

Quit the program and restart using the min and max X and Y values you saw on
the command line. As you move the controllers, the integer numbers should now
move between 0 and 127, which is the maximum range of values MIDI supports for
controller messages. These are the actual numbers sent to Character Animator.

Character Animator applies an additional scale factor to the positional data.

## Binding Controllers Positional Data to Character Animator 

Be aware the work "control" has multiple meanings - make sure you have the
right one in mind. Specifically

 - The HTC Vive also has two hand controllers that we want to track.
 - There are MIDI "control" events which allow numeric values in the range 0 to 127 to be sent. For example, a volume control, an reverb control, etc.
 - Inside Character Animator there is a "control" panel that allows you to bind MIDI events to triggers and behavior properties.

It is just a co-incidence that all three, which need to be linked up, have all
used the word "control" or "controller".

To bind MIDI triggers and control values through to Character Animator
controls, enter "rig" mode for your puppet, open up the "Control" panel,
click on "Layout" so you can edit the controls visible, and drag the behavior
properties (like Position X/Y and Rotation for a Transform behavior) over to
the Control panel. This will give you sliders or knobs for numeric behavior
properties. Triggers will also be present.

To bind a HTC Vive controller button to a trigger, I suggest restarting the
utility with "none" rather than "all" so no numeric control events are output.
(Otherwise controller will generate positional events causing incorrect
bindings to be made. Click on the panel for the desired trigger in Character
Animator then click the button on the HTC Vive controller. This will send a
MIDI event and bind that event to the trigger. Click on the next trigger in
Character Animator and clicke the next controller button.

To bind numeric values (sliders and knobs), you need to generate MIDI events
for that value only. This is the purpose of the "lx", "ly", "la", "rx", "ry",
and "ra" modes. To bind the Left X value to a behavior, start the utillity with
"lx" so that it only outputs Left Controller X position events. You will need
to restart for each of the 6 bindings.

No, its not very elegant. What would be nicer is a GUI with buttons, but
my C# programming skills are not very good. Volunteers to clean this up
would be welcome!

Once you have the bindings done, you need to adjust the ranges on the controls.
I selected the following values:

 - Right Position X: -500 to 3500
 - Right Position Y: -2700 to 300
 - Right Rotation: 0 to 360
 - Left Position X: -3500 to 500
 - Left Position Y: -2700 to 500
 - Left Rotation: 0 to 360

What such strange values? This is because the starting position of the hands
in your artwork matters. The X range for both hands should be the same (4000
in my case), but the default resting position of my puppet hands are around
3000 apart, hence the offsets. Similarly the Y values, the default resting
position of the hands was quite low on the page (and Y increases down the page).

Once you have it all set up, using the 'test' mode moves both hands around
in a circle, computing the rotation values as well. This is where tweaking
the hand angles can be tested.

## Future Work 

I got this code to work, just, then stopped. It works for me, but if anyone
would like to clean it up and improve, feel free! The following were some
thoughts I had.

The touchpads on each controller can be used to generate positional data
as well. This might be useful for controlling head or eye positions.
Unfortuantely there is no MIDI control for eye gaze, so webcam is still
probably best approach for eyes. (Transforms on the eyes are hard to get
right.)

The trigger also has X-axis positional data, meaning instead of on/off,
it could be used (for example) to select on of several hand positions such as
open, closed a bit, pointing, fully closed. That would mean left/right triggers
control left/right hand positions.

Could use rotation of the controller to show palm of hand / back of hand
in artwork, to get the hands to look more natural. At present, same hand
rotates 360 degress, which frankly looks strange (you cannot physically do it).
Need to flip to a different hand position at a certain angle.

This code is currently experimental - use at own risk.

This may be better as a GUI with buttons to control things, but I my
knowledge of C# is pretty minimal, so I stuck with a command line tool.

For my "Sam" puppet, I used the following;

HtcMidi.exe -rha 260 -lha 100 --min-y 0.2 -f 8
