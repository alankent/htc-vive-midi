using System;
using Sanford.Multimedia.Midi;
using System.Threading;
using Valve.VR;
using System.Runtime.InteropServices;

namespace HtcMidi
{
    //private static OutputDeviceDialog outDialog = new OutputDeviceDialog();

    class HtcProgram
    {
        private enum ControllerEnablement { All = 0xf, LeftX = 0x1, LeftY = 0x2, RightX = 0x4, RightY = 0x8, None = 0x0 };

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
            private CVRSystem vrPointer;

            public HtcToMidi(OutputDevice outDevice, int channelNumber, int fps, ControllerEnablement controllerEnablement, float minX, float maxX, float minY, float maxY)
            {
                this.outDevice = outDevice;
                this.channelNumber = channelNumber;
                this.fps = fps;
                this.controllerEnablement = controllerEnablement;
                this.minX = minX;
                this.maxX = maxX;
                this.minY = minY;
                this.maxY = maxY;
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
                Console.WriteLine("Controller " + controllerNumber + " = " + controllerValue);
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
                                    Debug("Button event " + pEvent.eventType);

                                    ETrackedDeviceClass trackedDeviceClass = vrPointer.GetTrackedDeviceClass(pEvent.trackedDeviceIndex);
                                    if (trackedDeviceClass == ETrackedDeviceClass.Controller)
                                    {
                                        Debug("  Is controller");
                                        ETrackedControllerRole role = vrPointer.GetControllerRoleForTrackedDeviceIndex(pEvent.trackedDeviceIndex);
                                        if (role == ETrackedControllerRole.LeftHand || role == ETrackedControllerRole.RightHand)
                                        {
                                            // Left hand generates 10,11,12,13,... notes based on which button.
                                            // Right hand generates 20,21,22,23,... notes based on which button.
                                            int baseNoteID = (role == ETrackedControllerRole.LeftHand) ? 10 : 20;

                                            switch ((EVRButtonId)pEvent.data.controller.button)
                                            {
                                                case EVRButtonId.k_EButton_ApplicationMenu:
                                                    {
                                                        switch ((EVREventType)pEvent.eventType)
                                                        {
                                                            case EVREventType.VREvent_ButtonPress:
                                                                {
                                                                    NoteOn(baseNoteID + 0);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUnpress:
                                                                {
                                                                    NoteOff(baseNoteID + 0);
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
                                                                    NoteOn(baseNoteID + 1);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUnpress:
                                                                {
                                                                    NoteOff(baseNoteID + 1);
                                                                    break;
                                                                }
                                                        }
                                                        break;
                                                    }
                                                case EVRButtonId.k_EButton_SteamVR_Touchpad:
                                                    {
                                                        switch ((EVREventType)pEvent.eventType)
                                                        {
                                                            case EVREventType.VREvent_ButtonPress:
                                                                {
                                                                    NoteOn(baseNoteID + 2);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUnpress:
                                                                {
                                                                    NoteOff(baseNoteID + 2);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonTouch:
                                                                {
                                                                    // TODO: get position of finger on touchpad as additional controller values (e.g. for eye movements)
                                                                    NoteOn(baseNoteID + 3);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUntouch:
                                                                {
                                                                    NoteOff(baseNoteID + 3);
                                                                    break;
                                                                }
                                                        }
                                                        break;
                                                    }
                                                case EVRButtonId.k_EButton_SteamVR_Trigger:
                                                    {
                                                        // TODO: I believe the trigger can actually be a controller as well (tracks how far trigger is squeezed)

                                                        switch ((EVREventType)pEvent.eventType)
                                                        {
                                                            case EVREventType.VREvent_ButtonPress:
                                                                {
                                                                    NoteOn(baseNoteID + 4);
                                                                    break;
                                                                }
                                                            case EVREventType.VREvent_ButtonUnpress:
                                                                {
                                                                    NoteOff(baseNoteID + 4);
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
                                        int y = NormalizeControllerValue(vector.m7, minY, maxY);

                                        if (fps < 8)
                                        {
                                            Debug(((role == ETrackedControllerRole.LeftHand) ? "LEFT " : "RIGHT") + "  x: " + vector.m3 + " " + x + "  y: " + vector.m7 + " " + y);
                                        }

                                        // Send events for enabled controllers. In calibration mode we restrict to just one controller, which
                                        // makes rigging in Character Animator easier too.
                                        if (role == ETrackedControllerRole.LeftHand)
                                        {
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.LeftX)
                                            {
                                                Controller(1, x);
                                            }
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.LeftY)
                                            {
                                                Controller(2, y);
                                            }
                                        }
                                        if (role == ETrackedControllerRole.RightHand)
                                        {
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.RightX)
                                            {
                                                Controller(3, x);
                                            }
                                            if (controllerEnablement == ControllerEnablement.All || controllerEnablement == ControllerEnablement.RightY)
                                            {
                                                Controller(4, y);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    Thread.Sleep(1000 / fps); // Send events for 25fps
                }

                CloseHtc();
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
        }

        static void Main(string[] args)
        {
            Console.WriteLine("MIDI Device Count = " + OutputDevice.DeviceCount);

            if (args.Length != 8)
            {
                Usage("Incorrect number of arguments.");
            }

            // Get the MIDI device number to send messages to
            if (!Int32.TryParse(args[0], out int outDeviceID) || outDeviceID < 0 || outDeviceID >= OutputDevice.DeviceCount)
            {
                Usage("First argument (<device-id>) must be an integer in the range 0 to " + (OutputDevice.DeviceCount - 1));
            }

            // Get the MIDI channel number for messages.
            if (!Int32.TryParse(args[1], out int channelNumber) || channelNumber < 0 || channelNumber >= 16)
            {
                Usage("Second argument (<midi-channel>) must be an integer in the range 0 to 15");
            }

            // Get the frames per second rate (so we send controller events at this speed)
            if (!Int32.TryParse(args[2], out int fps) || fps < 1 || fps > 100)
            {
                Usage("Third argument (<fps>) must be an integer in the range 1 to 100");
            }

            // See which controllers to enable. (It is useful to restrict controllers when doing rigging in Character Animator.)
            ControllerEnablement controllerEnablement = ControllerEnablement.All;
            switch (args[3])
            {
                case "all": { controllerEnablement = ControllerEnablement.All; break; }
                case "none": { controllerEnablement = ControllerEnablement.None; break; }
                case "lx": { controllerEnablement = ControllerEnablement.LeftX; break; }
                case "ly": { controllerEnablement = ControllerEnablement.LeftY; break; }
                case "rx": { controllerEnablement = ControllerEnablement.RightX; break; }
                case "ry": { controllerEnablement = ControllerEnablement.RightY; break; }
                default: { Usage("Fourth argument must be one of 'all', 'none', 'lx', 'ly', 'rx', or 'ry'."); break; }
            }

            // Get minX/maxX/minY/maxY.
            if (!float.TryParse(args[4], out float minX))
            {
                Usage("Fifth argument (<min-x>) must be a float.");
            }
            if (!float.TryParse(args[5], out float maxX))
            {
                Usage("Sixth argument (<max-x>) must be a float.");
            }
            if (!float.TryParse(args[6], out float minY))
            {
                Usage("Seventh argument (<min-y>) must be a float.");
            }
            if (!float.TryParse(args[7], out float maxY))
            {
                Usage("Eighth argument (<max-y>) must be a float.");
            }

            // Connect to the MIDI output device.
            Console.WriteLine("Connecting to " + OutputDevice.GetDeviceCapabilities(outDeviceID).name);
            OutputDevice outDevice = new OutputDevice(outDeviceID);
            HtcToMidi h2m = new HtcToMidi(outDevice, channelNumber, fps, controllerEnablement, minX, maxX, minY, maxY);

            // Start processing events.
            h2m.ProcessHtcEvents();
        }

        private static void Usage(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("Usage: HtcMidi.exe <device-id> <midi-channel> <fps> all|none|lx|ly|rx|ry <min-x> <max-x> <min-y> <max-y>");
            Console.WriteLine("e.g. HtcMidi 1 2 25 all 0 1.8 0.8 1.8");
            Console.WriteLine("Available MIDI devices:");
            for (int i = 0; i < OutputDevice.DeviceCount; i++)
            {
                MidiOutCaps cap = OutputDevice.GetDeviceCapabilities(i);
                Console.WriteLine("" + i + ": " + cap.name);
            }
            Thread.Sleep(5000);
            System.Environment.Exit(1);
        }
    }
}
