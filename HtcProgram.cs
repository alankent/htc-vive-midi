using System;
using Sanford.Multimedia.Midi;
using System.Threading;
using Valve.VR;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace HtcMidi
{
    // Simplified MIDI API (notes on/off events, and controller value change events).
    // Supports supressing particular events for binding and debugging purposes.
    internal class Midi
    {
        private OutputDevice outDevice;
        private int channelNumber;
        private Dictionary<int, int> lastControllerValue = new Dictionary<int, int>();
        private bool notesEnabled = true;
        private HashSet<int> enabledControllers = null;

        // Constructor.
        public Midi(int midiDeviceID, int channelNumber)
        {
            Console.WriteLine("Connecting to " + OutputDevice.GetDeviceCapabilities(midiDeviceID).name);
            this.outDevice = new OutputDevice(midiDeviceID);
            this.channelNumber = channelNumber;
        }

        // Send a NoteOn MIDI event.
        public void NoteOn(int noteID)
        {
            if (notesEnabled)
            {
                int fullVelocity = 127;
                outDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, channelNumber, noteID, fullVelocity));

                string msg = "NoteOn  " + noteID + " " + HtcMidiNotes.NoteIDToString(noteID);
                if (HtcMidiNotes.LookupNoteMetadata(noteID, out HtcMidiNotes.NoteMetadata n))
                {
                    msg += "  " + n.Name + "  " + n.Description;
                }
                Console.WriteLine(msg);
            }
        }

        // Send a NoteOff MIDI event.
        public void NoteOff(int noteID)
        {
            if (notesEnabled)
            {
                int zeroVelocity = 0;
                outDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, channelNumber, noteID, zeroVelocity));

                string msg = "NoteOff " + noteID + " " + HtcMidiNotes.NoteIDToString(noteID);
                if (HtcMidiNotes.LookupNoteMetadata(noteID, out HtcMidiNotes.NoteMetadata n))
                {
                    msg += "  " + n.Name + "  " + n.Description;
                }
                Console.WriteLine(msg);
            }
        }

        // Send a Controller MIDI event (controller values must be in the range 0 to 127)
        public void Controller(int controllerNumber, int controllerValue)
        {
            if (enabledControllers == null || enabledControllers.Contains(controllerNumber))
            {
                // Avoid sending the same value again, to reduce the number of controller value update messages sent.
                // TODO: Could optimize further by only sending values that are more than say 2 different to the previous value.
                if (!lastControllerValue.TryGetValue(controllerNumber, out int prevValue) || controllerValue != prevValue)
                {
                    outDevice.Send(new ChannelMessage(ChannelCommand.Controller, channelNumber, controllerNumber, controllerValue));
                    lastControllerValue[controllerNumber] = controllerValue;
                    // Uncomment this if you want noisy debugging information.
                    //Console.WriteLine("Controller " + controllerNumber + " = " + controllerValue);
                }
            }
        }

        // Enable all notes and controller messages (the default)
        public void EnableAll()
        {
            this.notesEnabled = true;
            this.enabledControllers = null;
        }

        // Disable all notes and controller messages
        public void DisableAll()
        {
            this.notesEnabled = false;
            this.enabledControllers = new HashSet<int>();
        }

        // Enable notes, disable all controllers.
        public void EnableNotes()
        {
            this.notesEnabled = true;
        }

        // Enable only specified controller event (discard notes as well)
        public void EnableController(int controllerNumber)
        {
            this.enabledControllers.Add(controllerNumber);
        }

        // Return true if notes are enabled.
        public bool NotesAreEnabled()
        {
            return this.notesEnabled;
        }

        // Return true if controller is enabled.
        public bool ControllerIsEnabled(int controllerNumber)
        {
            return enabledControllers == null || enabledControllers.Contains(controllerNumber);
        }
    }

    // Useful MIDI note and controller constants.
    internal class HtcMidiNotes
    {
        // MIDI Controller numbers to use for X, Y, Rotation events.
        internal class ControllerID
        {
            internal const int LeftX = 0;
            internal const int LeftY = 1;
            internal const int LeftRotation = 2;
            internal const int RightX = 50;
            internal const int RightY = 51;
            internal const int RightRotation = 52;
            internal const int HeadX = 90; // 100 got swallowed by something
            internal const int HeadY = 91;
            internal const int HeadRotation = 92;
        };

        // Note IDs to use for various trigger events.
        internal class NoteID
        {
            // Using typical keyboard ranges so CH can display them nicely.
            internal const int LeftBase = 24; // C1
            internal const int RightBase = 60; // C4

            // The following are added to the Left/Right Base values.
            internal const int ApplicationMenuButtonOffset = 0;
            internal const int TriggerButtonOffset = 1;
            internal const int GripButtonOffset = 2;

            // Palm direction offsets
            internal const int PalmForwardOffset = 5;
            internal const int PalmDownOffset = 6;
            internal const int PalmBackwardOffset = 7;
            internal const int PalmUpOffset = 8;

            // Touchpad 1-9 (like phone touchpad cells)
            internal const int TouchpadTouchOffset = 11;
            internal const int TouchpadPressOffset = 11 + 12;

        };

        // Used for table of note information for better diagnostics etc.
        internal class NoteMetadata
        {
            public string Name { get; set; }
            public int NoteID { get; set; }
            public string Description { get; set; }
        };

        // Big table of note metadata for all notes triggers we use.
        internal static NoteMetadata[] noteMetadata = new NoteMetadata[]
        {
            new NoteMetadata { Name = "lamb", NoteID = NoteID.LeftBase + NoteID.ApplicationMenuButtonOffset, Description = "Left Application Menu Button" },
            new NoteMetadata { Name = "ltrb", NoteID = NoteID.LeftBase + NoteID.TriggerButtonOffset, Description = "Left Trigger Button" },
            new NoteMetadata { Name = "lgrb", NoteID = NoteID.LeftBase + NoteID.GripButtonOffset, Description = "Left Grip Button" },

            new NoteMetadata { Name = "lnpf", NoteID = NoteID.LeftBase + NoteID.PalmForwardOffset, Description = "Left Palm Forwards" },
            new NoteMetadata { Name = "lnpd", NoteID = NoteID.LeftBase + NoteID.PalmDownOffset, Description = "Left Palm Down" },
            new NoteMetadata { Name = "lnpb", NoteID = NoteID.LeftBase + NoteID.PalmBackwardOffset, Description = "Left Palm Backwards" },
            new NoteMetadata { Name = "lnpu", NoteID = NoteID.LeftBase + NoteID.PalmUpOffset, Description = "Left Palm Up" },

            new NoteMetadata { Name = "ltt1", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 1, Description = "Left Touchpad Touch 1 NW" },
            new NoteMetadata { Name = "ltt2", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 2, Description = "Left Touchpad Touch 2 N" },
            new NoteMetadata { Name = "ltt3", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 3, Description = "Left Touchpad Touch 3 NE" },
            new NoteMetadata { Name = "ltt4", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 4, Description = "Left Touchpad Touch 4 W" },
            new NoteMetadata { Name = "ltt5", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 5, Description = "Left Touchpad Touch 5 0" },
            new NoteMetadata { Name = "ltt6", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 6, Description = "Left Touchpad Touch 6 E" },
            new NoteMetadata { Name = "ltt7", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 7, Description = "Left Touchpad Touch 7 SW" },
            new NoteMetadata { Name = "ltt8", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 8, Description = "Left Touchpad Touch 8 S" },
            new NoteMetadata { Name = "ltt9", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset + 9, Description = "Left Touchpad Touch 9 SE" },

            new NoteMetadata { Name = "ltp1", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 1, Description = "Left Touchpad Press 1 NW" },
            new NoteMetadata { Name = "ltp2", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 2, Description = "Left Touchpad Press 2 N" },
            new NoteMetadata { Name = "ltp3", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 3, Description = "Left Touchpad Press 3 NE" },
            new NoteMetadata { Name = "ltp4", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 4, Description = "Left Touchpad Press 4 W" },
            new NoteMetadata { Name = "ltp5", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 5, Description = "Left Touchpad Press 5 0" },
            new NoteMetadata { Name = "ltp6", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 6, Description = "Left Touchpad Press 6 E" },
            new NoteMetadata { Name = "ltp7", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 7, Description = "Left Touchpad Press 7 SW" },
            new NoteMetadata { Name = "ltp8", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 8, Description = "Left Touchpad Press 8 S" },
            new NoteMetadata { Name = "ltp9", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset + 9, Description = "Left Touchpad Press 9 SE" },

            new NoteMetadata { Name = "ramb", NoteID = NoteID.RightBase + NoteID.ApplicationMenuButtonOffset, Description = "Right Application Menu Button" },
            new NoteMetadata { Name = "rtrb", NoteID = NoteID.RightBase + NoteID.TriggerButtonOffset, Description = "Right Trigger Button" },
            new NoteMetadata { Name = "rgrb", NoteID = NoteID.RightBase + NoteID.GripButtonOffset, Description = "Right Grip Button" },

            new NoteMetadata { Name = "rnpf", NoteID = NoteID.RightBase + NoteID.PalmForwardOffset, Description = "Right Palm Forwards" },
            new NoteMetadata { Name = "rnpd", NoteID = NoteID.RightBase + NoteID.PalmDownOffset, Description = "Right Palm Down" },
            new NoteMetadata { Name = "rnpb", NoteID = NoteID.RightBase + NoteID.PalmBackwardOffset, Description = "Right Palm Backwards" },
            new NoteMetadata { Name = "rnpu", NoteID = NoteID.RightBase + NoteID.PalmUpOffset, Description = "Right Palm Up" },

            new NoteMetadata { Name = "rtt1", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 1, Description = "Right Touchpad Touch 1 NW" },
            new NoteMetadata { Name = "rtt2", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 2, Description = "Right Touchpad Touch 2 N" },
            new NoteMetadata { Name = "rtt3", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 3, Description = "Right Touchpad Touch 3 NE" },
            new NoteMetadata { Name = "rtt4", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 4, Description = "Right Touchpad Touch 4 W" },
            new NoteMetadata { Name = "rtt5", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 5, Description = "Right Touchpad Touch 5 0" },
            new NoteMetadata { Name = "rtt6", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 6, Description = "Right Touchpad Touch 6 E" },
            new NoteMetadata { Name = "rtt7", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 7, Description = "Right Touchpad Touch 7 SW" },
            new NoteMetadata { Name = "rtt8", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 8, Description = "Right Touchpad Touch 8 S" },
            new NoteMetadata { Name = "rtt9", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset + 9, Description = "Right Touchpad Touch 9 SE" },

            new NoteMetadata { Name = "rtp1", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 1, Description = "Right Touchpad Press 1 NW" },
            new NoteMetadata { Name = "rtp2", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 2, Description = "Right Touchpad Press 2 N" },
            new NoteMetadata { Name = "rtp3", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 3, Description = "Right Touchpad Press 3 NE" },
            new NoteMetadata { Name = "rtp4", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 4, Description = "Right Touchpad Press 4 W" },
            new NoteMetadata { Name = "rtp5", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 5, Description = "Right Touchpad Press 5 0" },
            new NoteMetadata { Name = "rtp6", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 6, Description = "Right Touchpad Press 6 E" },
            new NoteMetadata { Name = "rtp7", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 7, Description = "Right Touchpad Press 7 SW" },
            new NoteMetadata { Name = "rtp8", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 8, Description = "Right Touchpad Press 8 S" },
            new NoteMetadata { Name = "rtp9", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset + 9, Description = "Right Touchpad Press 9 SE" },
        };

        // Human readable names for MIDI notes. MIDI starts octaves from C (not A).
        internal static string[] noteNames =
        {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
        };

        // Convert a note ID (like 24) to a note name (like "C2")
        internal static string NoteIDToString(int noteID)
        {
            return noteNames[noteID % 12] + (noteID / 12).ToString();
        }

        // Find note metadata given the noteID.
        internal static bool LookupNoteMetadata(int noteID, out NoteMetadata dest)
        {
            foreach (NoteMetadata n in noteMetadata)
            {
                if (noteID == n.NoteID)
                {
                    dest = n;
                    return true;
                }
            }
            dest = null;
            return false;
        }
    }

    internal struct SceneDimensions
    {
        public float minX;
        public float minY;
        public float maxX;
        public float maxY;

        public SceneDimensions(float minX, float minY, float maxX, float maxY)
        {
            this.minX = minX;
            this.minY = minY;
            this.maxX = maxX;
            this.maxY = maxY;
        }
    }

    // Code for processing events of a hand controller (left and right).
    internal class Visor
    {
        private Midi midi;
        private int headAngle;
        private int baseNoteID;
        private SceneDimensions sceneDimensions;
        private bool verbose;

        // Constructor.
        public Visor(Midi midi, int headAngle, int baseNoteID, SceneDimensions sceneDimensions, bool verbose)
        {
            this.midi = midi;
            this.headAngle = headAngle;
            this.baseNoteID = baseNoteID;
            this.sceneDimensions = sceneDimensions;
            this.verbose = verbose;
        }

        // Given controller data, work out which way the palm is facing to fire the required trigger.
        internal void UpdateHeadPositionAndAngle(TrackedDevicePose_t trackedDevicePose)
        {
            // Process position data of controllers
            if (trackedDevicePose.bDeviceIsConnected && trackedDevicePose.bPoseIsValid)
            {
                // Get position of device.
                HmdMatrix34_t vector = trackedDevicePose.mDeviceToAbsoluteTracking;
                int x = 127 - NormalizeControllerValue(vector.m3, sceneDimensions.minX, sceneDimensions.maxX);
                int y = 127 - NormalizeControllerValue(vector.m7, sceneDimensions.minY, sceneDimensions.maxY);

                // Work out the head tilt.
                int deg = MatrixToDegrees(vector, headAngle);
                
                // Debugging output.
                if (verbose)
                {
                    // The README.md file references this output syntax.
                    Console.WriteLine("HEAD" + "  x: " + vector.m3 + " (" + x + ")  y: " + vector.m7 + " (" + y + ")");

                    // Don't get too verbose with both controllers.
                    // The (X,Y) position of the controller is (m3,m7).
                    // The direction the controller is pointing in is Zvec, so atan2(m6,m2) gives is the angle for rotating hands etc.
                    Console.WriteLine("HEAD MATRIX\n"
                        + "   Xvec     Yvec     Zvec     Transpose\n"
                        + "x  m0=" + r(vector.m0) + "  m1=" + r(vector.m1) + "  m2=" + r(vector.m2) + "  m3=" + r(vector.m3) + "\n"
                        + "y  m4=" + r(vector.m4) + "  m5=" + r(vector.m5) + "  m6=" + r(vector.m6) + "  m7=" + r(vector.m7) + "\n"
                        + "z  m8=" + r(vector.m8) + "  m9=" + r(vector.m9) + " m10=" + r(vector.m10) + " m11=" + r(vector.m11));
                    Console.WriteLine(" Rotation = " + MatrixToDegrees(vector, 0));
                }

                // Head position and rotation MIDI events.
                midi.Controller(HtcMidiNotes.ControllerID.HeadX, x);
                midi.Controller(HtcMidiNotes.ControllerID.HeadY, y);
                // MIDI values are 0 to 127, so convert degrees to 0..127 range.
                midi.Controller(HtcMidiNotes.ControllerID.HeadRotation, deg * 127 / 360);
            }
        }

        // Round the float off to percentages, for display in debugging messages.
        private string r(float n)
        {
            return ((int)(n * 100.0)).ToString().PadLeft(4, ' ');
        }

        // Convert controller vector data to Adobe Character Animator degrees for rotations.
        private int MatrixToDegrees(HmdMatrix34_t vector, int deltaAngle)
        {
            float deltaX = vector.m1;
            float deltaY = vector.m5;
            double rad = Math.Atan2(deltaY, deltaX); // In radians
            int deg = (int)(rad * (180.0 / Math.PI));
            deg = deg - 90;
            deg = deg - deltaAngle;
            while (deg < 0) deg += 360;
            while (deg >= 360) deg -= 360;
            return deg;
        }

        // Normalize controller value from 0 to 127 based on min/max values for that controller.
        private int NormalizeControllerValue(float n, double min, double max)
        {
            int num = (int)(((n - min) / (max - min)) * 127);
            if (num < 0) num = 0;
            if (num > 127) num = 127;
            return num;
        }
    }

    // Code for processing events of a hand controller (left and right).
    internal class HandController
    {
        private Midi midi;
        private int handAngle;
        private int baseNoteID;
        private SceneDimensions sceneDimensions;
        private int currentPalmDir = -1;
        private bool touchingTouchpad = false;
        private bool pressingTouchpad = false;
        private int touchNoteID = -1;
        private bool isPuppetLeftHand;
        private bool verbose;

        // Constructor.
        public HandController(Midi midi, int handAngle, int baseNoteID, SceneDimensions sceneDimensions, bool isPuppetLeftHand, bool verbose)
        {
            this.midi = midi;
            this.handAngle = handAngle;
            this.baseNoteID = baseNoteID;
            this.sceneDimensions = sceneDimensions;
            this.isPuppetLeftHand = isPuppetLeftHand;
            this.verbose = verbose;
        }

        // Once main loop determines if an event is for the left or right hand, it dispatches to the hand controller to process.
        public void ProcessEvent(VREvent_t pEvent)
        {
            switch ((EVRButtonId)pEvent.data.controller.button)
            {
                case EVRButtonId.k_EButton_ApplicationMenu:
                    {
                        switch ((EVREventType)pEvent.eventType)
                        {
                            case EVREventType.VREvent_ButtonPress:
                                {
                                    midi.NoteOn(baseNoteID + HtcMidiNotes.NoteID.ApplicationMenuButtonOffset);
                                    break;
                                }
                            case EVREventType.VREvent_ButtonUnpress:
                                {
                                    midi.NoteOff(baseNoteID + HtcMidiNotes.NoteID.ApplicationMenuButtonOffset);
                                    break;
                                }
                        }
                        break;
                    }
                case EVRButtonId.k_EButton_SteamVR_Trigger:
                    {
                        // TODO: The trigger can actually be a controller as well (tracks how far trigger is squeezed)

                        switch ((EVREventType)pEvent.eventType)
                        {
                            case EVREventType.VREvent_ButtonPress:
                                {
                                    midi.NoteOn(baseNoteID + HtcMidiNotes.NoteID.TriggerButtonOffset);
                                    break;
                                }
                            case EVREventType.VREvent_ButtonUnpress:
                                {
                                    midi.NoteOff(baseNoteID + HtcMidiNotes.NoteID.TriggerButtonOffset);
                                    break;
                                }
                        }
                        break;
                    }
                case EVRButtonId.k_EButton_SteamVR_Touchpad:
                    {
                        switch ((EVREventType)pEvent.eventType)
                        {
                            case EVREventType.VREvent_ButtonTouch:
                                {
                                    touchingTouchpad = true;
                                    break;
                                }
                            case EVREventType.VREvent_ButtonUntouch:
                                {
                                    touchingTouchpad = false;
                                    break;
                                }
                            case EVREventType.VREvent_ButtonPress:
                                {
                                    pressingTouchpad = true;
                                    break;
                                }
                            case EVREventType.VREvent_ButtonUnpress:
                                {
                                    pressingTouchpad = false;
                                    break;
                                }
                        }
                        break;
                    }
                case EVRButtonId.k_EButton_Grip:
                    {
                        switch ((EVREventType)pEvent.eventType)
                        {
                            case EVREventType.VREvent_ButtonPress:
                                {
                                    midi.NoteOn(baseNoteID + HtcMidiNotes.NoteID.GripButtonOffset);
                                    break;
                                }
                            case EVREventType.VREvent_ButtonUnpress:
                                {
                                    midi.NoteOff(baseNoteID + HtcMidiNotes.NoteID.GripButtonOffset);
                                    break;
                                }
                        }
                        break;
                    }
            }
        }

        // Given controller data, work out which way the palm is facing to fire the required trigger.
        internal void UpdateHandPositionsAndAngle(TrackedDevicePose_t trackedDevicePose)
        {
            // Process position data of controllers
            if (trackedDevicePose.bDeviceIsConnected && trackedDevicePose.bPoseIsValid)
            {
                // Get position of device.
                HmdMatrix34_t vector = trackedDevicePose.mDeviceToAbsoluteTracking;
                int x = 127 - NormalizeControllerValue(vector.m3, sceneDimensions.minX, sceneDimensions.maxX);
                int y = 127 - NormalizeControllerValue(vector.m7, sceneDimensions.minY, sceneDimensions.maxY);

                // Left controller = puppet right hand.
                int deg = MatrixToDegrees(vector, handAngle);

                // Work out hand twist position.
                // We want to normalize to 0 = palm towards screen, 1 = palm down, 2 = palm away from screen, 3 = palm up.
                // m9 being negative means palm is facing screen, positive is back of hand facing screen.
                // m8 (for puppet left hand) is negative means palm down (thumb to screen), positive palm up (no thumb)
                int palmDir = baseNoteID;

                // Decide if m8 or m9 is more significant
                if (Math.Abs(vector.m8) > Math.Abs(vector.m9))
                {
                    if (vector.m8 > 0.0)
                    {
                        // The left vs right hands face opposite directions, so we need to know which hand the controller is to get it right.
                        if (isPuppetLeftHand)
                        {
                            palmDir += HtcMidiNotes.NoteID.PalmUpOffset;
                        }
                        else
                        {
                            palmDir += HtcMidiNotes.NoteID.PalmDownOffset;
                        }
                    }
                    else
                    {
                        if (isPuppetLeftHand)
                        {
                            palmDir += HtcMidiNotes.NoteID.PalmDownOffset;
                        }
                        else
                        {
                            palmDir += HtcMidiNotes.NoteID.PalmUpOffset;
                        }
                    }
                }
                else
                {
                    if (vector.m9 > 0.0)
                    {
                        palmDir += HtcMidiNotes.NoteID.PalmBackwardOffset;
                    }
                    else
                    {
                        palmDir += HtcMidiNotes.NoteID.PalmForwardOffset;
                    }
                }

                // Change the palm rotation, if it changed.
                if (palmDir != currentPalmDir)
                {
                    if (currentPalmDir >= 0)
                    {
                        midi.NoteOff(currentPalmDir);
                    }
                    midi.NoteOn(palmDir);
                    currentPalmDir = palmDir;
                }

                // Debugging output.
                if (verbose && false)
                {
                    // The README.md file references this output syntax.
                    Console.WriteLine((isPuppetLeftHand ? "PUPPET LEFT " : "PUPPET RIGHT") + "  x: " + vector.m3 + " (" + x + ")  y: " + vector.m7 + " (" + y + ")");

                    // Don't get too verbose with both controllers.
                    if (isPuppetLeftHand)
                    {
                        // The (X,Y) position of the controller is (m3,m7).
                        // The direction the controller is pointing in is Zvec, so atan2(m6,m2) gives is the angle for rotating hands etc.
                        Console.WriteLine("LEFT MATRIX\n"
                            + "   Xvec     Yvec     Zvec     Transpose\n"
                            + "x  m0=" + r(vector.m0) + "  m1=" + r(vector.m1) + "  m2=" + r(vector.m2) + "  m3=" + r(vector.m3) + "\n"
                            + "y  m4=" + r(vector.m4) + "  m5=" + r(vector.m5) + "  m6=" + r(vector.m6) + "  m7=" + r(vector.m7) + "\n"
                            + "z  m8=" + r(vector.m8) + "  m9=" + r(vector.m9) + " m10=" + r(vector.m10) + " m11=" + r(vector.m11));
                        Console.WriteLine(" Rotation = " + MatrixToDegrees(vector, 0));
                    }
                }

                // Send events for enabled controllers. In calibration mode we restrict to just one controller, which
                // makes rigging in Character Animator easier too.
                // Note: The right controller controls the puppets left hand and vice versa.
                if (isPuppetLeftHand)
                {
                    midi.Controller(HtcMidiNotes.ControllerID.LeftX, x);
                    midi.Controller(HtcMidiNotes.ControllerID.LeftY, y);
                    // MIDI values are 0 to 127, so convert degrees to 0..127 range.
                    midi.Controller(HtcMidiNotes.ControllerID.LeftRotation, deg * 127 / 360);
                }
                else
                {
                    midi.Controller(HtcMidiNotes.ControllerID.RightX, x);
                    midi.Controller(HtcMidiNotes.ControllerID.RightY, y);
                    // MIDI values are 0 to 127, so convert degrees to 0..127 range.
                    midi.Controller(HtcMidiNotes.ControllerID.RightRotation, deg * 127 / 360);
                }
            }
        }


        // Work out the touchpad X/Y and convert to triggers (9 zones, touched or pressed).
        internal void UpdateTrackpadNotes(VRControllerState_t controllerState)
        {
            // Work out touchpad touch positions, turn notes on/off if anything has changed.
            // Work out phone keypad position based on x & y (1..9)
            // (Trigger button is Axis1)
            float x = controllerState.rAxis0.x;
            float y = controllerState.rAxis0.y;

            // Get angle and distance from center, since trackpad is circular
            int keyNum;
            double distance = Math.Sqrt(x * x + y * y);
            if (distance < 0.5f)
            {
                keyNum = 5; // Centroid
            }
            else
            {
                double radians = Math.Atan2(y, x);
                double deg = radians * (180.0 / Math.PI);
                if (deg < 0.0) deg += 360;
                // deg is 0..360 for left, 90 up, 270 down, 0 right.
                // We have 8 sectors N, NW, W, SW, etc, each is 45 degrees. So -22.5 to 22.5 degress is East.
                int sector = (int)((deg + 22.5) / 45.0);
                switch (sector)
                {
                    default: keyNum = 6; break; // E (can be 0 or 8)
                    case 1: keyNum = 3; break; // NE
                    case 2: keyNum = 2; break; // N
                    case 3: keyNum = 1; break; // NW
                    case 4: keyNum = 4; break; // W
                    case 5: keyNum = 7; break; // SW
                    case 6: keyNum = 8; break; // S
                    case 7: keyNum = 9; break; // SE
                }
            }

            // Left controller == puppet right hand
            int newNoteID = pressingTouchpad ? (baseNoteID + HtcMidiNotes.NoteID.TouchpadPressOffset + keyNum)
                : touchingTouchpad ? (baseNoteID + HtcMidiNotes.NoteID.TouchpadTouchOffset + keyNum)
                : -1;
            if (newNoteID != touchNoteID)
            {
                if (touchNoteID >= 0)
                {
                    midi.NoteOff(touchNoteID);
                }
                touchNoteID = newNoteID;
                if (touchNoteID >= 0)
                {
                    midi.NoteOn(touchNoteID);
                }
            }
        }

        // Round the float off to percentages, for display in debugging messages.
        private string r(float n)
        {
            return ((int)(n * 100.0)).ToString().PadLeft(4, ' ');
        }

        // Convert controller vector data to Adobe Character Animator degrees for rotations.
        private int MatrixToDegrees(HmdMatrix34_t vector, int deltaAngle)
        {
            float deltaX = vector.m2;
            float deltaY = vector.m6;
            double rad = Math.Atan2(deltaY, deltaX); // In radians
            int deg = (int)(rad * (180.0 / Math.PI));
            deg = deg + 90;
            deg = deg - deltaAngle;
            while (deg < 0) deg += 360;
            while (deg >= 360) deg -= 360;
            return deg;
        }

        // Normalize controller value from 0 to 127 based on min/max values for that controller.
        private int NormalizeControllerValue(float n, double min, double max)
        {
            int num = (int)(((n - min) / (max - min)) * 127);
            if (num < 0) num = 0;
            if (num > 127) num = 127;
            return num;
        }
    }

    // HTC Controller main processing loop.
    internal class HtcToMidi
    {
        private Midi midi;
        private int fps;
        private SceneDimensions sceneDimensions;
        uint leftControllerDeviceIndex;
        uint rightControllerDeviceIndex;
        private CVRSystem vrPointer;
        private HandController leftHand;
        private HandController rightHand;
        private Visor visor;

        public HtcToMidi(Midi midi, int fps, SceneDimensions sceneDimensions, int leftHandAngle, int rightHandAngle)
        {
            this.midi = midi;
            this.fps = fps;
            this.sceneDimensions = sceneDimensions;
            this.leftHand = new HandController(midi, leftHandAngle, HtcMidiNotes.NoteID.LeftBase, sceneDimensions, true, fps == 1);
            this.rightHand = new HandController(midi, rightHandAngle, HtcMidiNotes.NoteID.RightBase, sceneDimensions, false, fps == 1);
            this.visor = new Visor(midi, 0, HtcMidiNotes.NoteID.RightBase, sceneDimensions, fps == 1);
        }

        // Initialize the HTC controller.
        private void InitHtc()
        {
            // https://github.com/ValveSoftware/openvr/issues/316

            // Initialize VR subsystem.
            EVRInitError eError = EVRInitError.None;
            vrPointer = OpenVR.Init(ref eError, EVRApplicationType.VRApplication_Background);
            if (eError != EVRInitError.None)
            {
                vrPointer = null;

                // TODO: Yeah, should throw an exception...
                Console.WriteLine("Unable to init VR runtime: " + OpenVR.GetStringForHmdError(eError));
                Thread.Sleep(5000);
                System.Environment.Exit(1);
            }

            // Find the left and right controllers.
            leftControllerDeviceIndex = vrPointer.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            rightControllerDeviceIndex = vrPointer.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
        }

        // Main processing loop.
        public void ProcessHtcEvents()
        {
            InitHtc();

            // Loop polling for VR events.
            bool exitLoop = false;
            while (!exitLoop)
            {
                VREvent_t pEvent = new VREvent_t();

                // Process all events the have built up (returns false when all events done).
                while (vrPointer.PollNextEvent(ref pEvent, (uint)Marshal.SizeOf(pEvent)))
                {
                    switch ((EVREventType)pEvent.eventType)
                    {
                        case EVREventType.VREvent_Quit:
                        case EVREventType.VREvent_ProcessQuit:
                        case EVREventType.VREvent_QuitAborted_UserPrompt:
                        case EVREventType.VREvent_QuitAcknowledged:
                            {
                                exitLoop = true;
                                break;
                            }

                        case EVREventType.VREvent_ButtonPress:
                        case EVREventType.VREvent_ButtonUnpress:
                        case EVREventType.VREvent_ButtonTouch:
                        case EVREventType.VREvent_ButtonUntouch:
                            {
                                ETrackedDeviceClass trackedDeviceClass = vrPointer.GetTrackedDeviceClass(pEvent.trackedDeviceIndex);
                                if (trackedDeviceClass == ETrackedDeviceClass.Controller)
                                {
                                    ETrackedControllerRole role = vrPointer.GetControllerRoleForTrackedDeviceIndex(pEvent.trackedDeviceIndex);
                                    if (role == ETrackedControllerRole.LeftHand)
                                    {
                                        rightHand.ProcessEvent(pEvent);
                                    }
                                    else if (role == ETrackedControllerRole.RightHand)
                                    {
                                        leftHand.ProcessEvent(pEvent);
                                    }


                                }
                                break;
                            }
                    }
                }

                // Get HMD (the visor) position.
                if (vrPointer.IsTrackedDeviceConnected(0))
                {
                    TrackedDevicePose_t[] trackedDevicePoses = new TrackedDevicePose_t[1];
                    trackedDevicePoses[0] = new TrackedDevicePose_t();
                    vrPointer.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0.0f, trackedDevicePoses);
                    TrackedDevicePose_t trackedDevicePose = trackedDevicePoses[0];
                    visor.UpdateHeadPositionAndAngle(trackedDevicePose);
                }

                // Get tracking information for left and right controllers.
                if (leftControllerDeviceIndex >= 0)
                {
                    if (vrPointer.IsTrackedDeviceConnected(leftControllerDeviceIndex))
                    {
                        // Get positional data of controllers to see if should send new Controller events.
                        TrackedDevicePose_t trackedDevicePose = new TrackedDevicePose_t();
                        VRControllerState_t controllerState = new VRControllerState_t();

                        if (vrPointer.GetControllerStateWithPose(ETrackingUniverseOrigin.TrackingUniverseStanding, leftControllerDeviceIndex, ref controllerState, (uint)Marshal.SizeOf(controllerState), ref trackedDevicePose))
                        {
                            // The left controller is the puppets right hand.
                            rightHand.UpdateTrackpadNotes(controllerState);
                            rightHand.UpdateHandPositionsAndAngle(trackedDevicePose);
                        }
                    }
                }
                if (rightControllerDeviceIndex >= 0)
                {
                    if (vrPointer.IsTrackedDeviceConnected(rightControllerDeviceIndex))
                    {
                        // Get positional data of controllers to see if should send new Controller events.
                        TrackedDevicePose_t trackedDevicePose = new TrackedDevicePose_t();
                        VRControllerState_t controllerState = new VRControllerState_t();

                        if (vrPointer.GetControllerStateWithPose(ETrackingUniverseOrigin.TrackingUniverseStanding, rightControllerDeviceIndex, ref controllerState, (uint)Marshal.SizeOf(controllerState), ref trackedDevicePose))
                        {
                            // The left controller is the puppets right hand.
                            leftHand.UpdateTrackpadNotes(controllerState);
                            leftHand.UpdateHandPositionsAndAngle(trackedDevicePose);
                        }
                    }
                }

                // Send events no faster than requested frames per second
                Thread.Sleep(1000 / fps);
            }

            CloseHtc();
        }

        // Shut down the HTC controller.
        private void CloseHtc()
        {
            // Shutdown VR subsystem cleanly.
            if (vrPointer != null)
            {
                OpenVR.Shutdown();
                vrPointer = null;
            }
        }
    }

    // Parse command line options for the main program.
    internal class ParseOptions
    {
        private int argNum = 0;
        private string[] args;

        // Constructor
        public ParseOptions(string[] args)
        {
            this.args = args;
        }

        // Returns true if there are more options to parse.
        public bool MoreArgs()
        {
            return argNum < args.Length;
        }

        // Always call IsArg() followed by ArgParam() to get the value.
        public bool IsArg(string shortForm, string longForm)
        {
            bool isArg = (argNum + 1) < args.Length && (args[argNum] == shortForm || args[argNum] == longForm);
            if (isArg)
            {
                argNum++;
            }
            return isArg;
        }

        // Get next argument from command line.
        public string ArgParam()
        {
            return args[argNum++];
        }

        // Argument without a parameter value.
        public bool IsFlag(string shortForm, string longForm)
        {
            bool isArg = argNum < args.Length && (args[argNum] == shortForm || args[argNum] == longForm);
            if (isArg)
            {
                argNum++;
            }
            return isArg;
        }
    }

    // The main programm class.
    class HtcProgram
    {
        // Main program.
        static void Main(string[] args)
        {
            // Defer connecting to MIDI device until we have parsed command line options.
            Midi midi = null;

            Console.WriteLine("MIDI Device Count = " + OutputDevice.DeviceCount);

            // Command line options.
            int outDeviceID = OutputDevice.DeviceCount - 1;
            int channelNumber = 0;
            int fps = 8;
            float minX = 0.0F;
            float maxX = 1.8F;
            float minY = 0.8F;
            float maxY = 1.8F;
            int rha = 220;
            int lha = 140;
            bool testMode = false;
            bool noteSent = false;
            bool cFlagSpecified = false;

            ParseOptions parser = new ParseOptions(args);
            while (parser.MoreArgs())
            {
                if (parser.IsArg("-d", "--device-id"))
                {
                    // Get the MIDI device number to send messages to
                    if (!Int32.TryParse(parser.ArgParam(), out outDeviceID) || outDeviceID < 0 || outDeviceID >= OutputDevice.DeviceCount)
                    {
                        Usage("MIDI <device-id> must be an integer in the range 0 to " + (OutputDevice.DeviceCount - 1));
                    }
                }
                else if (parser.IsArg("-c", "--channel"))
                {
                    // Get the MIDI channel number for messages.
                    if (!Int32.TryParse(parser.ArgParam(), out channelNumber) || channelNumber < 1 || channelNumber > 16)
                    {
                        Usage("MIDI <channel> must be an integer in the range 1 to 16");
                    }

                    // Humans are told MIDI channel number is 1 to 16, but internally its 0 to 15.
                    channelNumber--;
                }
                else if (parser.IsArg("-f", "--fps"))
                {
                    // Get the frames per second rate (so we send controller events at this speed)
                    if (!Int32.TryParse(parser.ArgParam(), out fps) || fps < 1 || fps > 100)
                    {
                        Usage("<fps> must be an integer in the range 1 to 100");
                    }
                }
                else if (parser.IsArg("-x", "--min-x"))
                {
                    if (!float.TryParse(parser.ArgParam(), out minX))
                    {
                        Usage("<min-x> must be a float.");
                    }
                }
                else if (parser.IsArg("-X", "--max-x"))
                {
                    if (!float.TryParse(parser.ArgParam(), out maxX))
                    {
                        Usage("<max-x> must be a float.");
                    }
                }
                else if (parser.IsArg("-y", "--min-y"))
                {
                    if (!float.TryParse(parser.ArgParam(), out minY))
                    {
                        Usage("<min-y> must be a float.");
                    }
                }
                else if (parser.IsArg("-Y", "--max-y"))
                {
                    if (!float.TryParse(parser.ArgParam(), out maxY))
                    {
                        Usage("<max-y> must be a float.");
                    }
                }
                else if (parser.IsArg("-rha", "--right-hand-angle"))
                {
                    // Natural left/right hand angles.
                    if (!Int32.TryParse(parser.ArgParam(), out rha) || rha < 0 || rha >= 360)
                    {
                        Usage("Puppet right hand angle must be an integer in the range 0 to 359");
                    }
                }
                else if (parser.IsArg("-lha", "--left-hand-angle"))
                {
                    // Natural left/right hand angles.
                    if (!Int32.TryParse(parser.ArgParam(), out lha) || lha < 0 || lha >= 360)
                    {
                        Usage("Puppet left hand angle must be an integer in the range 0 to 359");
                    }
                }
                else if (parser.IsFlag("-t", "--test"))
                {
                    testMode = true;
                }
                else if (parser.IsArg("-n", "--note"))
                {
                    string arg = parser.ArgParam();
                    int noteID = ParseNoteID(arg);

                    // Great! Send the note!
                    if (midi == null)
                    {
                        midi = new Midi(outDeviceID, channelNumber);
                    }
                    midi.NoteOn(noteID);
                    Thread.Sleep(2000);
                    midi.NoteOff(noteID);
                    noteSent = true;
                }
                else if (parser.IsArg("-e", "--enable"))
                {
                    // See which MIDI events to enable. (It is useful to restrict controllers when doing rigging in Character Animator.)
                    if (!cFlagSpecified)
                    {
                        if (midi == null)
                        {
                            midi = new Midi(outDeviceID, channelNumber);
                        }
                        midi.DisableAll();
                        cFlagSpecified = true;
                    }

                    switch (parser.ArgParam())
                    {
                        case "all": midi.EnableAll(); break;
                        case "notes": midi.EnableNotes(); break;
                        case "hx": midi.EnableController(HtcMidiNotes.ControllerID.HeadX); break;
                        case "hy": midi.EnableController(HtcMidiNotes.ControllerID.HeadY); break;
                        case "ha": midi.EnableController(HtcMidiNotes.ControllerID.HeadRotation); break;
                        case "lx": midi.EnableController(HtcMidiNotes.ControllerID.LeftX); break;
                        case "ly": midi.EnableController(HtcMidiNotes.ControllerID.LeftY); break;
                        case "la": midi.EnableController(HtcMidiNotes.ControllerID.LeftRotation); break;
                        case "rx": midi.EnableController(HtcMidiNotes.ControllerID.RightX); break;
                        case "ry": midi.EnableController(HtcMidiNotes.ControllerID.RightY); break;
                        case "ra": midi.EnableController(HtcMidiNotes.ControllerID.RightRotation); break;
                        default: Usage("Unknown enable flag"); break;
                    }
                }
                else
                {
                    Usage("");
                }
            }

            // If we sent a note, then our job is done.
            if (!noteSent)
            {
                if (midi == null)
                {
                    midi = new Midi(outDeviceID, channelNumber);
                }
                HtcToMidi h2m = new HtcToMidi(midi, fps, new SceneDimensions(minX, minY, maxX, maxY), rha, lha);

                if (testMode)
                {
                    // Generate events showing the limits of all the controls, so can calibrate the settings without having a HTC Vive handy.
                    TestLimits(midi, lha, rha);
                }
                else
                {
                    // Start processing events.
                    h2m.ProcessHtcEvents();
                }
            }
        }

        // Parse a note name (e.g. "C3", or "63", or "lamb").
        private static int ParseNoteID(string arg)
        {
            int noteID = -1;

            // Look up button names.
            foreach (HtcMidiNotes.NoteMetadata n in HtcMidiNotes.noteMetadata)
            {
                if (n.Name == arg)
                {
                    noteID = n.NoteID;
                }
            }

            // See if a raw note number
            if (noteID < 0)
            {
                if (!Int32.TryParse(arg, out noteID))
                {
                    noteID = -1;
                }
            }

            // Try to parse "G#3" etc.
            if (noteID < 0 && arg.Length > 1)
            {
                string name;
                string octave;
                if (arg.ToCharArray()[1] == '#')
                {
                    name = arg.Substring(0, 2);
                    octave = arg.Substring(2);
                }
                else
                {
                    name = arg.Substring(0, 1);
                    octave = arg.Substring(1);
                }

                if (!Int32.TryParse(octave, out int octaveNum))
                {
                    Usage("Failed to parse note name.");
                }

                int noteNum = -1;
                for (int i = 0; i < 12; i++)
                {
                    if (HtcMidiNotes.noteNames[i] == name)
                    {
                        noteNum = i;
                        break;
                    }
                }
                if (noteNum >= 0)
                {
                    noteID = octaveNum * 12 + noteNum;
                }
            }

            if (noteID < 0 || noteID > 127)
            {
                Usage("Unknown note or button name.");
            }

            return noteID;
        }

        // Common reporting of usage and command line options.
        private static void Usage(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("Usage: HtcMidi.exe");
            Console.WriteLine("-d|--device <int>     MIDI device number (max device)");
            Console.WriteLine("-c|--channel <int>    MIDI channel number (1)");
            Console.WriteLine("-f|--fps <int>        Frames per sec (8)");
            Console.WriteLine("-x|--min-x <float>    Min X value (0.0)");
            Console.WriteLine("-X|--max-x <float>    Max X value (1.8)");
            Console.WriteLine("-y|--min-y <float>    Min Y value (0.8)");
            Console.WriteLine("-Y|--max-y <float>    Max Y value (1.8)");
            Console.WriteLine("-rha|--right-hand-angle <int>   Default angle of puppets right hand (220)");
            Console.WriteLine("-lha|--left-hand-angle <int>    Default angle of puppets left hand (140)");
            Console.WriteLine("-t|--test             Generate a selection of synthentic test data (off)");
            Console.WriteLine("-n|--note <string>    Send a single note for the given note/button (off)");
            Console.WriteLine("-e|--enable all|notes|hx|hy|ha|lx|ly|la|rx|ry|ra   Limit what MIDI events are sent (all)");
            Console.WriteLine("\ne.g. HtcMidi -lha 90 -rha 90 --test");
            Console.WriteLine("\ne.g. HtcMidi --note ltop   (send left touchpad press)");
            Console.WriteLine("\nAvailable MIDI devices:");
            for (int i = 0; i < OutputDevice.DeviceCount; i++)
            {
                MidiOutCaps cap = OutputDevice.GetDeviceCapabilities(i);
                Console.WriteLine("" + i + ": " + cap.name);
            }
            Console.WriteLine("\nNote names can be note numbers (60 = C4), 'G#3', or button names.");
            Console.WriteLine("Buttons:");
            foreach (HtcMidiNotes.NoteMetadata n in HtcMidiNotes.noteMetadata)
            {
                Console.WriteLine(n.Name + " (" + HtcMidiNotes.NoteIDToString(n.NoteID) + " " + n.NoteID + ") - " + n.Description);
            }
            Thread.Sleep(5000);
            System.Environment.Exit(1);
        }

        // Generate lots of test MIDI data for testing purposes, without needing a HTC Vive.
        public static void TestLimits(Midi midi, int leftHandAngle, int rightHandAngle)
        {
            while (true)
            {
                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.HeadX)
                    || midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.HeadY)
                    || midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.HeadRotation))
                {
                    // Tilt head from left to right
                    Console.WriteLine("Head tilt");
                    for (int i = 0; i <= 180; i++)
                    {
                        // We want to start and end in middle with head swaying backwards and forwards
                        // i=0..45 maps to deg=360..315; 45..90->315..360; 90..135->0..45; 135..180->45..0
                        int deg = (i < 45) ? (360 - i) :
                            (i < 90) ? (270 + i) :
                            (i < 135) ? (i - 90) :
                            (180 - i);

                        // Head moves for moving the body around
                        int x = (int)((Math.Cos((deg - 90) * (Math.PI / 180.0)) + 1.0) * 32.0) + 32;
                        int y = (int)((Math.Sin((deg - 90) * (Math.PI / 180.0)) + 1.0) * 16.0) + 80;
                        midi.Controller(HtcMidiNotes.ControllerID.HeadX, x);
                        midi.Controller(HtcMidiNotes.ControllerID.HeadY, y);
                        int headAngle = 0; // Might need later in case heads don't always point upwards!
                        midi.Controller(HtcMidiNotes.ControllerID.HeadRotation, ((deg - headAngle + 360) * 127 / 360) % 127);
                        Thread.Sleep(10);
                    }
                }

                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.RightX)
                    || midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.RightY)
                    || midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.RightRotation))
                {
                    // Move hands around in a circle, adjusting the angle as well.
                    Console.WriteLine("Right Hand");
                    for (int deg = 200; deg < 360 + 200; deg++)
                    {
                        int x = (int)((Math.Cos((deg - 90) * (Math.PI / 180.0)) + 1.0) * 32.0);
                        int y = (int)((Math.Sin((deg - 90) * (Math.PI / 180.0)) + 1.0) * 63.0);
                        midi.Controller(HtcMidiNotes.ControllerID.RightX, x);
                        midi.Controller(HtcMidiNotes.ControllerID.RightY, y);
                        midi.Controller(HtcMidiNotes.ControllerID.RightRotation, ((deg - rightHandAngle + 360) * 127 / 360) % 127);
                        Thread.Sleep(5);
                    }
                }

                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.LeftX)
                    || midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.LeftY)
                    || midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.LeftRotation))
                {
                    // Move hands around in a circle, adjusting the angle as well.
                    Console.WriteLine("Left Hand");
                    for (int deg = 160; deg < 360 + 160; deg++)
                    {
                        int x = (int)((Math.Cos((deg - 90) * (Math.PI / 180.0)) + 1.0) * 48.0) + 28;
                        int y = (int)((Math.Sin((deg - 90) * (Math.PI / 180.0)) + 1.0) * 63.0);
                        midi.Controller(HtcMidiNotes.ControllerID.LeftX, x);
                        midi.Controller(HtcMidiNotes.ControllerID.LeftY, y);
                        midi.Controller(HtcMidiNotes.ControllerID.LeftRotation, ((deg - leftHandAngle + 360) * 127 / 360) % 127);
                        Thread.Sleep(5);
                    }
                }

                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.RightX))
                {
                    Thread.Sleep(1000);
                    Console.WriteLine("RightX Controller");
                    for (int i = 127; i >= 0; i--)
                    {
                        midi.Controller(HtcMidiNotes.ControllerID.RightX, i);
                        Thread.Sleep(10);
                    }
                }
                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.RightY))
                {
                    Thread.Sleep(1000);
                    Console.WriteLine("RightY Controller");
                    for (int i = 0; i <= 127; i++)
                    {
                        midi.Controller(HtcMidiNotes.ControllerID.RightY, i);
                        Thread.Sleep(10);
                    }
                }
                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.RightRotation))
                {
                    Thread.Sleep(1000);
                    Console.WriteLine("RightRotation Controller");
                    for (int i = 0; i <= 127; i++)
                    {
                        midi.Controller(HtcMidiNotes.ControllerID.RightRotation, i);
                        Thread.Sleep(10);
                    }
                }

                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.LeftX))
                {
                    Thread.Sleep(1000);
                    Console.WriteLine("LeftX Controller");
                    for (int i = 0; i <= 127; i++)
                    {
                        midi.Controller(HtcMidiNotes.ControllerID.LeftX, i);
                        Thread.Sleep(10);
                    }
                }
                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.LeftY))
                {
                    Thread.Sleep(1000);
                    Console.WriteLine("LeftY Controller");
                    for (int i = 0; i <= 127; i++)
                    {
                        midi.Controller(HtcMidiNotes.ControllerID.LeftY, i);
                        Thread.Sleep(10);
                    }
                }
                if (midi.ControllerIsEnabled(HtcMidiNotes.ControllerID.LeftRotation))
                {
                    Thread.Sleep(1000);
                    Console.WriteLine("LeftRotation Controller");
                    for (int i = 0; i <= 127; i++)
                    {
                        midi.Controller(HtcMidiNotes.ControllerID.LeftRotation, i);
                        Thread.Sleep(10);
                    }
                }

                if (midi.NotesAreEnabled())
                {
                    foreach (HtcMidiNotes.NoteMetadata n in HtcMidiNotes.noteMetadata)
                    {
                        Console.WriteLine(n.Description);
                        midi.NoteOn(n.NoteID);
                        Thread.Sleep(1000);
                        midi.NoteOff(n.NoteID);
                    }

                    // Test hand rotations with trigger on/off.
                    Thread.Sleep(1000);
                    Console.WriteLine("Palm forward + trigger");
                    midi.NoteOn(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.PalmForwardOffset);
                    Thread.Sleep(1000);
                    midi.NoteOn(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    Thread.Sleep(1000);
                    midi.NoteOff(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    midi.NoteOff(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.PalmForwardOffset);

                    Thread.Sleep(1000);
                    Console.WriteLine("Palm down + trigger");
                    midi.NoteOn(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.PalmDownOffset);
                    Thread.Sleep(1000);
                    midi.NoteOn(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    Thread.Sleep(1000);
                    midi.NoteOff(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    midi.NoteOff(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.PalmDownOffset);

                    Thread.Sleep(1000);
                    Console.WriteLine("Palm back + trigger");
                    midi.NoteOn(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.PalmBackwardOffset);
                    Thread.Sleep(1000);
                    midi.NoteOn(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    Thread.Sleep(1000);
                    midi.NoteOff(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    midi.NoteOff(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.PalmBackwardOffset);

                    Thread.Sleep(1000);
                    Console.WriteLine("Palm up + trigger");
                    midi.NoteOn(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.PalmUpOffset);
                    Thread.Sleep(1000);
                    midi.NoteOn(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    Thread.Sleep(1000);
                    midi.NoteOff(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    midi.NoteOff(HtcMidiNotes.NoteID.LeftBase + HtcMidiNotes.NoteID.PalmUpOffset);

                    // Test hand rotations with trigger on/off.
                    Thread.Sleep(1000);
                    Console.WriteLine("Palm forward + trigger");
                    midi.NoteOn(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.PalmForwardOffset);
                    Thread.Sleep(1000);
                    midi.NoteOn(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    Thread.Sleep(1000);
                    midi.NoteOff(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    midi.NoteOff(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.PalmForwardOffset);

                    Thread.Sleep(1000);
                    Console.WriteLine("Palm down + trigger");
                    midi.NoteOn(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.PalmDownOffset);
                    Thread.Sleep(1000);
                    midi.NoteOn(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    Thread.Sleep(1000);
                    midi.NoteOff(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    midi.NoteOff(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.PalmDownOffset);

                    Thread.Sleep(1000);
                    Console.WriteLine("Palm back + trigger");
                    midi.NoteOn(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.PalmBackwardOffset);
                    Thread.Sleep(1000);
                    midi.NoteOn(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    Thread.Sleep(1000);
                    midi.NoteOff(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    midi.NoteOff(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.PalmBackwardOffset);

                    Thread.Sleep(1000);
                    Console.WriteLine("Palm up + trigger");
                    midi.NoteOn(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.PalmUpOffset);
                    Thread.Sleep(1000);
                    midi.NoteOn(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    Thread.Sleep(1000);
                    midi.NoteOff(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.TriggerButtonOffset);
                    midi.NoteOff(HtcMidiNotes.NoteID.RightBase + HtcMidiNotes.NoteID.PalmUpOffset);

                }
            }
        }
    }
}
