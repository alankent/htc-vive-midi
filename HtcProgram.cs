using System;
using Sanford.Multimedia.Midi;
using System.Threading;
using Valve.VR;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace HtcMidi
{
    //private static OutputDeviceDialog outDialog = new OutputDeviceDialog();

    class HtcProgram
    {
        // Processing modes (which MIDI events to generate).
        private enum ControllerEnablement { None, All, Notes, LeftX, LeftY, RightX, RightY, LeftHandAngle, RightHandAngle };

        // MIDI Controller numbers to use for X, Y, Rotation events.
        internal class ControllerID
        {
            internal const int LeftX = 0;
            internal const int LeftY = 1;
            internal const int LeftRotation = 2;
            internal const int RightX = 50;
            internal const int RightY = 51;
            internal const int RightRotation = 52;
        };

        internal class NoteID
        {
            // Using Controller range + 10 just to make it less confusing.
            internal const int LeftBase = 10;
            internal const int RightBase = 60;

            // The following are added to the Left/Right Base values.
            internal const int ApplicationMenuButtonOffset = 0;
            internal const int TriggerButtonOffset = 1;
            internal const int TouchpadTouchOffset = 2;
            internal const int TouchpadPressOffset = 3;
            internal const int GripButtonOffset = 4;

            // Palm direction offsets
            internal const int PalmForwardOffset = 10;
            internal const int PalmDownOffset = 11;
            internal const int PalmBackwardOffset = 12;
            internal const int PalmUpOffset = 13;
        };

        internal class NoteStruct
        {
            public string Name { get; set; }
            public int NoteID { get; set; }
            public string Description { get; set; }
        };
        private static NoteStruct[] notes = new NoteStruct[] 
        {
            new NoteStruct { Name = "lamb", NoteID = NoteID.LeftBase + NoteID.ApplicationMenuButtonOffset, Description = "Left Application Menu Button" },
            new NoteStruct { Name = "ltrb", NoteID = NoteID.LeftBase + NoteID.TriggerButtonOffset, Description = "Left Trigger Button" },
            new NoteStruct { Name = "ltot", NoteID = NoteID.LeftBase + NoteID.TouchpadTouchOffset, Description = "Left Touchpad Touch" },
            new NoteStruct { Name = "ltop", NoteID = NoteID.LeftBase + NoteID.TouchpadPressOffset, Description = "Left Touchpad Press" },
            new NoteStruct { Name = "lgrb", NoteID = NoteID.LeftBase + NoteID.GripButtonOffset, Description = "Left Grip Button" },

            new NoteStruct { Name = "lnpf", NoteID = NoteID.LeftBase + NoteID.PalmForwardOffset, Description = "Left Palm Forwards" },
            new NoteStruct { Name = "lnpd", NoteID = NoteID.LeftBase + NoteID.PalmDownOffset, Description = "Left Palm Down" },
            new NoteStruct { Name = "lnpb", NoteID = NoteID.LeftBase + NoteID.PalmBackwardOffset, Description = "Left Palm Backwards" },
            new NoteStruct { Name = "lnpu", NoteID = NoteID.LeftBase + NoteID.PalmUpOffset, Description = "Left Palm Up" },

            new NoteStruct { Name = "ramb", NoteID = NoteID.RightBase + NoteID.ApplicationMenuButtonOffset, Description = "Right Application Menu Button" },
            new NoteStruct { Name = "rtrb", NoteID = NoteID.RightBase + NoteID.TriggerButtonOffset, Description = "Right Trigger Button" },
            new NoteStruct { Name = "rtot", NoteID = NoteID.RightBase + NoteID.TouchpadTouchOffset, Description = "Right Touchpad Touch" },
            new NoteStruct { Name = "rtop", NoteID = NoteID.RightBase + NoteID.TouchpadPressOffset, Description = "Right Touchpad Press" },
            new NoteStruct { Name = "rgrb", NoteID = NoteID.RightBase + NoteID.GripButtonOffset, Description = "Right Grip Button" },

            new NoteStruct { Name = "rnpf", NoteID = NoteID.RightBase + NoteID.PalmForwardOffset, Description = "Right Palm Forwards" },
            new NoteStruct { Name = "rnpd", NoteID = NoteID.RightBase + NoteID.PalmDownOffset, Description = "Right Palm Down" },
            new NoteStruct { Name = "rnpb", NoteID = NoteID.RightBase + NoteID.PalmBackwardOffset, Description = "Right Palm Backwards" },
            new NoteStruct { Name = "rnpu", NoteID = NoteID.RightBase + NoteID.PalmUpOffset, Description = "Right Palm Up" },
        };

        private class HtcToMidi
        {
            private OutputDevice outDevice;
            private int channelNumber;
            private int fps;
            private ControllerEnablement controllerEnablement;
            private float minX;
            private float maxX;
            private float minY;
            private float maxY;
            private int leftHandAngle;
            private int rightHandAngle;
            private CVRSystem vrPointer;

            public HtcToMidi(OutputDevice outDevice, int channelNumber, int fps, ControllerEnablement controllerEnablement, float minX, float maxX, float minY, float maxY, int leftHandAngle, int rightHandAngle)
            {
                this.outDevice = outDevice;
                this.channelNumber = channelNumber;
                this.fps = fps;
                this.controllerEnablement = controllerEnablement;
                this.minX = minX;
                this.maxX = maxX;
                this.minY = minY;
                this.maxY = maxY;
                this.leftHandAngle = leftHandAngle;
                this.rightHandAngle = rightHandAngle;
            }

            // Send a NoteOn MIDI event.
            public void NoteOn(int noteID)
            {
                int fullVelocity = 127;
                outDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, channelNumber, noteID, fullVelocity));
                Console.WriteLine("NoteOn " + noteID);
            }

            // Send a NoteOff MIDI event.
            public void NoteOff(int noteID)
            {
                int zeroVelocity = 0;
                outDevice.Send(new ChannelMessage(ChannelCommand.NoteOn, channelNumber, noteID, zeroVelocity));
                Console.WriteLine("NoteOff " + noteID);
            }

            // Send a Controller MIDI event (controller values must be in the range 0 to 127)
            public void Controller(int controllerNumber, int controllerValue)
            {
                outDevice.Send(new ChannelMessage(ChannelCommand.Controller, channelNumber, controllerNumber, controllerValue));
                //Console.WriteLine("Controller " + controllerNumber + " = " + controllerValue);
            }

            // Main processing loop.
            public void ProcessHtcEvents()
            {
                InitHtc();

                // Remember previous direction of palms so we only output events if things change.
                int currentLeftHandPalmDir = -1;
                int currentRightHandPalmDir = -1;

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
                                        if (role == ETrackedControllerRole.LeftHand || role == ETrackedControllerRole.RightHand)
                                        {
                                            // Left controller generates puppet right hand triggers.
                                            int baseNoteID = (role == ETrackedControllerRole.LeftHand) ? NoteID.RightBase : NoteID.LeftBase;

                                            switch ((EVRButtonId)pEvent.data.controller.button)
                                            {
                                                case EVRButtonId.k_EButton_ApplicationMenu:
                                                    {
                                                        switch ((EVREventType)pEvent.eventType)
                                                        {
                                                            case EVREventType.VREvent_ButtonPress:
                                                                {
                                                                    NoteOn(baseNoteID + NoteID.ApplicationMenuButtonOffset);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUnpress:
                                                                {
                                                                    NoteOff(baseNoteID + NoteID.ApplicationMenuButtonOffset);
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
                                                                    NoteOn(baseNoteID + NoteID.TriggerButtonOffset);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUnpress:
                                                                {
                                                                    NoteOff(baseNoteID + NoteID.TriggerButtonOffset);
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
                                                                    // TODO: get position of finger on touchpad as additional controller values (e.g. for eye movements)
                                                                    NoteOn(baseNoteID + NoteID.TouchpadTouchOffset);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUntouch:
                                                                {
                                                                    NoteOff(baseNoteID + NoteID.TouchpadTouchOffset);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonPress:
                                                                {
                                                                    NoteOn(baseNoteID + NoteID.TouchpadPressOffset);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUnpress:
                                                                {
                                                                    NoteOff(baseNoteID + NoteID.TouchpadPressOffset);
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
                                                                    NoteOn(baseNoteID + NoteID.GripButtonOffset);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUnpress:
                                                                {
                                                                    NoteOff(baseNoteID + NoteID.GripButtonOffset);
                                                                    break;
                                                                }
                                                        }
                                                        break;
                                                    }
                                            }
                                        }
                                    }
                                    break;
                                }
                        }
                    }
                 
                    // Get tracking information for left and right controllers.
                    for (uint id = 0; id < OpenVR.k_unMaxTrackedDeviceCount; id++)
                    {
                        ETrackedDeviceClass trackedDeviceClass = vrPointer.GetTrackedDeviceClass(id);
                        if (trackedDeviceClass == ETrackedDeviceClass.Controller && vrPointer.IsTrackedDeviceConnected(id))
                        {
                            // TODO: There is also "GenericTracker" instead of "Controller" for the Vive tracking devices.

                            // Get positional data of controllers to see if should send new Controller events.
                            TrackedDevicePose_t trackedDevicePose = new TrackedDevicePose_t();
                            VRControllerState_t controllerState = new VRControllerState_t();

                            if (vrPointer.GetControllerStateWithPose(ETrackingUniverseOrigin.TrackingUniverseStanding, id, ref controllerState, (uint)Marshal.SizeOf(controllerState), ref trackedDevicePose))
                            {
                                // TODO: For now we only do left and right controllers - not headset, not additional trackers.
                                ETrackedControllerRole role = vrPointer.GetControllerRoleForTrackedDeviceIndex(id);
                                if (role == ETrackedControllerRole.LeftHand || role == ETrackedControllerRole.RightHand)
                                {
                                    if (trackedDevicePose.bDeviceIsConnected && trackedDevicePose.bPoseIsValid)
                                    {
                                        // Get position of device.
                                        // TODO: There is lots of rotational data also available that could be used e.g. to rotate hands.
                                        HmdMatrix34_t vector = trackedDevicePose.mDeviceToAbsoluteTracking;
                                        int x = 127 - NormalizeControllerValue(vector.m3, minX, maxX);
                                        int y = 127 - NormalizeControllerValue(vector.m7, minY, maxY);

                                        // Left controller = puppet right hand.
                                        int deg = MatrixToDegrees(vector, (role == ETrackedControllerRole.LeftHand) ? rightHandAngle : leftHandAngle);

                                        // Work out hand twist position.
                                        // m9 being negative means palm is facing screen, positive is back of hand facing screen.
                                        // If m9 is around zero, then m2 postive/negative indicates rotation of hand.
                                        // We want to normalize to 0 = palm towards screen, 1 = palm down, 2 = palm away from screen, 3 = palm up.
                                        int palmDir = (role == ETrackedControllerRole.LeftHand) ? NoteID.LeftBase : NoteID.RightBase;
                                        if (vector.m9 < -0.6)
                                        {
                                            palmDir += NoteID.PalmForwardOffset;
                                        }
                                        else if (vector.m9 > 0.6)
                                        {
                                            palmDir += NoteID.PalmBackwardOffset;
                                        }
                                        else if (vector.m2 < 0.0)
                                        {
                                            palmDir += NoteID.PalmDownOffset;
                                        }
                                        else
                                        {
                                            palmDir += NoteID.PalmUpOffset;
                                        }
                                        if (role == ETrackedControllerRole.LeftHand)
                                        {
                                            if (palmDir != currentLeftHandPalmDir)
                                            {
                                                if (currentLeftHandPalmDir >= 0)
                                                {
                                                    NoteOff(currentLeftHandPalmDir);
                                                }
                                                NoteOn(palmDir);
                                                currentLeftHandPalmDir = palmDir;
                                            }
                                        }
                                        else
                                        {
                                            if (palmDir != currentRightHandPalmDir)
                                            {
                                                if (currentRightHandPalmDir >= 0)
                                                {
                                                    NoteOff(currentRightHandPalmDir);
                                                }
                                                NoteOn(palmDir);
                                                currentRightHandPalmDir = palmDir;
                                            }

                                        }

                                        if (fps == 1)
                                        {
                                            // The README.md file references this output syntax.
                                            Debug(((role == ETrackedControllerRole.LeftHand) ? "LEFT " : "RIGHT") + "  x: " + vector.m3 + " (" + x + ")  y: " + vector.m7 + " (" + y + ")");

                                            // Don't get too verbose with both controllers.
                                            if (role == ETrackedControllerRole.LeftHand)
                                            {
                                                // The (X,Y) position of the controller is (m3,m7).
                                                // The direction the controller is pointing in is Zvec, so atan2(m6,m2) gives is the angle for rotating hands etc.
                                                Debug("LEFT MATRIX\n"
                                                    + "   Xvec     Yvec     Zvec     Transpose\n"
                                                    + "x  m0=" + r(vector.m0) + "  m1=" + r(vector.m1) + "  m2=" + r(vector.m2) + "  m3=" + r(vector.m3) + "\n"
                                                    + "y  m4=" + r(vector.m4) + "  m5=" + r(vector.m5) + "  m6=" + r(vector.m6) + "  m7=" + r(vector.m7) + "\n"
                                                    + "z  m8=" + r(vector.m8) + "  m9=" + r(vector.m9) + " m10=" + r(vector.m10) + " m11=" + r(vector.m11));
                                                Debug(" Rotation = " + MatrixToDegrees(vector, 0));
                                            }
                                        }

                                        // Send events for enabled controllers. In calibration mode we restrict to just one controller, which
                                        // makes rigging in Character Animator easier too.
                                        // Note: The right controller controls the puppets left hand and vice versa.
                                        if (role == ETrackedControllerRole.RightHand)
                                        {
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.LeftX)
                                            {
                                                Controller(ControllerID.LeftX, x);
                                            }
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.LeftY)
                                            {
                                                Controller(ControllerID.LeftY, y);
                                            }
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.LeftHandAngle)
                                            {
                                                // MIDI values are 0 to 127, so convert degrees to 0..127 range.
                                                Controller(ControllerID.LeftRotation, deg * 127 / 360);
                                            }
                                        }
                                        if (role == ETrackedControllerRole.LeftHand)
                                        {
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.RightX)
                                            {
                                                Controller(ControllerID.RightX, x);
                                            }
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.RightY)
                                            {
                                                Controller(ControllerID.RightY, y);
                                            }
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.RightHandAngle)
                                            {
                                                // MIDI values are 0 to 127, so convert degrees to 0..127 range.
                                                Controller(ControllerID.RightRotation, deg * 127 / 360);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Send events no faster than requested frames per second
                    Thread.Sleep(1000 / fps);
                }

                CloseHtc();
            }

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
                int num = (int) (((n - min) / (max - min)) * 127);
                if (num < 0) num = 0;
                if (num > 127) num = 127;
                return num;
            }

            private void Debug(string msg)
            {
                Console.WriteLine(msg);
            }

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
            }

            private void CloseHtc()
            {
                // Shutdown VR subsystem cleanly.
                if (vrPointer != null)
                {
                    OpenVR.Shutdown();
                    vrPointer = null;
                }
            }

            public void TestLimits()
            {
                while (true)
                {
                    if (controllerEnablement == ControllerEnablement.All)
                    {
                        // Move hands around in a circle, adjusting the angle as well.
                        for (int deg = 200; deg < 360 + 200; deg++)
                        {
                            int x = (int)((Math.Cos((deg - 90) * (Math.PI / 180.0)) + 1.0) * 32.0);
                            int y = (int)((Math.Sin((deg - 90) * (Math.PI / 180.0)) + 1.0) * 63.0);
                            Controller(ControllerID.RightX, x);
                            Controller(ControllerID.RightY, y);
                            Controller(ControllerID.RightRotation, ((deg - rightHandAngle + 360) * 127 / 360) % 127);
                            Thread.Sleep(5);
                        }
                        for (int deg = 160; deg < 360 + 160; deg++)
                        {
                            int x = (int)((Math.Cos((deg - 90) * (Math.PI / 180.0)) + 1.0) * 48.0) + 28;
                            int y = (int)((Math.Sin((deg - 90) * (Math.PI / 180.0)) + 1.0) * 63.0);
                            Controller(ControllerID.LeftX, x);
                            Controller(ControllerID.LeftY, y);
                            Controller(ControllerID.LeftRotation, ((deg - leftHandAngle + 360) * 127 / 360) % 127);
                            Thread.Sleep(5);
                        }
                    }

                    if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.RightX)
                    {
                        Thread.Sleep(1000);
                        Debug("RightX Controller");
                        Thread.Sleep(1000);
                        for (int i = 127; i >= 0; i--)
                        {
                            Controller(ControllerID.RightX, i);
                            Thread.Sleep(10);
                        }
                    }
                    if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.RightY)
                    {
                        Thread.Sleep(1000);
                        Debug("RightY Controller");
                        Thread.Sleep(1000);
                        for (int i = 0; i <= 127; i++)
                        {
                            Controller(ControllerID.RightY, i);
                            Thread.Sleep(10);
                        }
                    }
                    if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.RightHandAngle)
                    {
                        Thread.Sleep(1000);
                        Debug("RightRotation Controller");
                        Thread.Sleep(1000);
                        for (int i = 0; i <= 127; i++)
                        {
                            Controller(ControllerID.RightRotation, i);
                            Thread.Sleep(10);
                        }
                    }

                    if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.LeftX)
                    {
                        Thread.Sleep(1000);
                        Debug("LeftX Controller");
                        Thread.Sleep(1000);
                        for (int i = 0; i <= 127; i++)
                        {
                            Controller(ControllerID.LeftX, i);
                            Thread.Sleep(10);
                        }
                    }
                    if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.LeftY)
                    {
                        Thread.Sleep(1000);
                        Debug("LeftY Controller");
                        Thread.Sleep(1000);
                        for (int i = 0; i <= 127; i++)
                        {
                            Controller(ControllerID.LeftY, i);
                            Thread.Sleep(10);
                        }
                    }
                    if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.LeftHandAngle)
                    {
                        Thread.Sleep(1000);
                        Debug("LeftRotation Controller");
                        Thread.Sleep(1000);
                        for (int i = 0; i <= 127; i++)
                        {
                            Controller(ControllerID.LeftRotation, i);
                            Thread.Sleep(10);
                        }
                    }

                    if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.Notes)
                    {
                        foreach (NoteStruct n in notes)
                        {
                            Thread.Sleep(1000);
                            Debug(n.Description);
                            Thread.Sleep(1000);
                            NoteOn(n.NoteID);
                            Thread.Sleep(2000);
                            NoteOff(n.NoteID);
                        }

                        // Test hand rotations with trigger on/off.
                        Thread.Sleep(1000);
                        Debug("Palm forward + trigger");
                        NoteOn(NoteID.LeftBase + NoteID.PalmForwardOffset);
                        Thread.Sleep(1000);
                        NoteOn(NoteID.LeftBase + NoteID.TriggerButtonOffset);
                        Thread.Sleep(1000);
                        NoteOff(NoteID.LeftBase + NoteID.TriggerButtonOffset);
                        NoteOff(NoteID.LeftBase + NoteID.PalmForwardOffset);

                        Thread.Sleep(1000);
                        Debug("Palm down + trigger");
                        NoteOn(NoteID.LeftBase + NoteID.PalmDownOffset);
                        Thread.Sleep(1000);
                        NoteOn(NoteID.LeftBase + NoteID.TriggerButtonOffset);
                        Thread.Sleep(1000);
                        NoteOff(NoteID.LeftBase + NoteID.TriggerButtonOffset);
                        NoteOff(NoteID.LeftBase + NoteID.PalmDownOffset);

                        Thread.Sleep(1000);
                        Debug("Palm back + trigger");
                        NoteOn(NoteID.LeftBase + NoteID.PalmBackwardOffset);
                        Thread.Sleep(1000);
                        NoteOn(NoteID.LeftBase + NoteID.TriggerButtonOffset);
                        Thread.Sleep(1000);
                        NoteOff(NoteID.LeftBase + NoteID.TriggerButtonOffset);
                        NoteOff(NoteID.LeftBase + NoteID.PalmBackwardOffset);

                        Thread.Sleep(1000);
                        Debug("Palm up + trigger");
                        NoteOn(NoteID.LeftBase + NoteID.PalmUpOffset);
                        Thread.Sleep(1000);
                        NoteOn(NoteID.LeftBase + NoteID.TriggerButtonOffset);
                        Thread.Sleep(1000);
                        NoteOff(NoteID.LeftBase + NoteID.TriggerButtonOffset);
                        NoteOff(NoteID.LeftBase + NoteID.PalmUpOffset);

                        // Test hand rotations with trigger on/off.
                        Thread.Sleep(1000);
                        Debug("Palm forward + trigger");
                        NoteOn(NoteID.RightBase + NoteID.PalmForwardOffset);
                        Thread.Sleep(1000);
                        NoteOn(NoteID.RightBase + NoteID.TriggerButtonOffset);
                        Thread.Sleep(1000);
                        NoteOff(NoteID.RightBase + NoteID.TriggerButtonOffset);
                        NoteOff(NoteID.RightBase + NoteID.PalmForwardOffset);

                        Thread.Sleep(1000);
                        Debug("Palm down + trigger");
                        NoteOn(NoteID.RightBase + NoteID.PalmDownOffset);
                        Thread.Sleep(1000);
                        NoteOn(NoteID.RightBase + NoteID.TriggerButtonOffset);
                        Thread.Sleep(1000);
                        NoteOff(NoteID.RightBase + NoteID.TriggerButtonOffset);
                        NoteOff(NoteID.RightBase + NoteID.PalmDownOffset);

                        Thread.Sleep(1000);
                        Debug("Palm back + trigger");
                        NoteOn(NoteID.RightBase + NoteID.PalmBackwardOffset);
                        Thread.Sleep(1000);
                        NoteOn(NoteID.RightBase + NoteID.TriggerButtonOffset);
                        Thread.Sleep(1000);
                        NoteOff(NoteID.RightBase + NoteID.TriggerButtonOffset);
                        NoteOff(NoteID.RightBase + NoteID.PalmBackwardOffset);

                        Thread.Sleep(1000);
                        Debug("Palm up + trigger");
                        NoteOn(NoteID.RightBase + NoteID.PalmUpOffset);
                        Thread.Sleep(1000);
                        NoteOn(NoteID.RightBase + NoteID.TriggerButtonOffset);
                        Thread.Sleep(1000);
                        NoteOff(NoteID.RightBase + NoteID.TriggerButtonOffset);
                        NoteOff(NoteID.RightBase + NoteID.PalmUpOffset);

                    }
                }
            }
        }

        // Main program.
        static void Main(string[] args)
        {
            Console.WriteLine("MIDI Device Count = " + OutputDevice.DeviceCount);

            if (args.Length != 11)
            {
                Usage("Incorrect number of arguments.");
            }

            // Get the MIDI device number to send messages to
            if (!Int32.TryParse(args[0], out int outDeviceID) || outDeviceID < 0 || outDeviceID >= OutputDevice.DeviceCount)
            {
                Usage("<device-id> must be an integer in the range 0 to " + (OutputDevice.DeviceCount - 1));
            }

            // Get the MIDI channel number for messages.
            if (!Int32.TryParse(args[1], out int channelNumber) || channelNumber < 0 || channelNumber >= 16)
            {
                Usage("<midi-channel> must be an integer in the range 0 to 15");
            }

            // Get the frames per second rate (so we send controller events at this speed)
            if (!Int32.TryParse(args[2], out int fps) || fps < 1 || fps > 100)
            {
                Usage("<fps> must be an integer in the range 1 to 100");
            }

            // Get minX/maxX/minY/maxY.
            if (!float.TryParse(args[3], out float minX))
            {
                Usage("<min-x> must be a float.");
            }
            if (!float.TryParse(args[4], out float maxX))
            {
                Usage("<max-x> must be a float.");
            }
            if (!float.TryParse(args[5], out float minY))
            {
                Usage("<min-y> must be a float.");
            }
            if (!float.TryParse(args[6], out float maxY))
            {
                Usage("<max-y> must be a float.");
            }

            // Natural left/right hand angles.
            if (!Int32.TryParse(args[7], out int rha) || rha < 0 || rha >= 360)
            {
                Usage("Puppet right hand angle must be an integer in the range 0 to 359");
            }
            if (!Int32.TryParse(args[8], out int lha) || lha < 0 || lha >= 360)
            {
                Usage("Puppet left hand angle must be an integer in the range 0 to 359");
            }

            // See if use HTC Vive or synthesized test data.
            bool testMode = false;
            switch (args[9])
            {
                case "htc-vive": { testMode = false; break; }
                case "test": { testMode = true; break; }
                default:
                    {
                        Usage("<mode> must be one of 'htc-vive' or 'test'.");
                        break;
                    }
            }

            // See which controllers to enable. (It is useful to restrict controllers when doing rigging in Character Animator.)
            ControllerEnablement controllerEnablement = ControllerEnablement.All;
            switch (args[10])
            {
                case "all": { controllerEnablement = ControllerEnablement.All; break; }
                case "none": { controllerEnablement = ControllerEnablement.None; break; }
                case "notes": { controllerEnablement = ControllerEnablement.Notes; break; }
                case "lx": { controllerEnablement = ControllerEnablement.LeftX; break; }
                case "ly": { controllerEnablement = ControllerEnablement.LeftY; break; }
                case "la": { controllerEnablement = ControllerEnablement.LeftHandAngle; break; }
                case "rx": { controllerEnablement = ControllerEnablement.RightX; break; }
                case "ry": { controllerEnablement = ControllerEnablement.RightY; break; }
                case "ra": { controllerEnablement = ControllerEnablement.RightHandAngle; break; }
                default:
                    {
                        foreach (NoteStruct n in notes)
                        {
                            if (n.Name == args[10])
                            {
                                HtcToMidi htom = new HtcToMidi(new OutputDevice(outDeviceID), channelNumber, fps, ControllerEnablement.None, minX, maxX, minY, maxY, lha, rha);
                                Console.WriteLine("Sending " + n.Name + " - " + n.Description);
                                htom.NoteOn(n.NoteID);
                                return;
                            }
                        }
                        Usage("<mode> must be one of 'all', 'none', 'lx', 'ly', 'la', 'rx', 'ry', 'ra', or a button name.");
                        break;
                    }
            }

            // Connect to the MIDI output device.
            Console.WriteLine("Connecting to " + OutputDevice.GetDeviceCapabilities(outDeviceID).name);
            OutputDevice outDevice = new OutputDevice(outDeviceID);
            HtcToMidi h2m = new HtcToMidi(outDevice, channelNumber, fps, controllerEnablement, minX, maxX, minY, maxY, lha, rha);

            if (testMode)
            {
                // Generate events showing the limits of all the controls, so can calibrate the settings without having a HTC Vive handy.
                h2m.TestLimits();
            }
            else
            {
                // Start processing events.
                h2m.ProcessHtcEvents();
            }
        }

        private static void Usage(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("Usage: HtcMidi.exe <device-id> <midi-channel> <fps> <min-x> <max-x> <min-y> <max-y> <puppet-right-hand-angle> <puppet-left-hand-angle> htc-vive|test all|none|lx|ly|la|rx|ry|ra|<button>");
            Console.WriteLine("e.g. HtcMidi 1 2 25 0 1.8 0.8 1.6 220 120 test all");
            Console.WriteLine("\nAvailable MIDI devices:");
            for (int i = 0; i < OutputDevice.DeviceCount; i++)
            {
                MidiOutCaps cap = OutputDevice.GetDeviceCapabilities(i);
                Console.WriteLine("" + i + ": " + cap.name);
            }
            Console.WriteLine("\nButtons:");
            foreach (NoteStruct n in notes)
            {
                Console.WriteLine(n.Name + " (" + n.NoteID + ") - " + n.Description);
            }
            Thread.Sleep(5000);
            System.Environment.Exit(1);
        }
    }
}
