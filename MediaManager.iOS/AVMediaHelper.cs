using System.Collections.Generic;
using System.Linq;
using System.Text;
using AVFoundation;
using Foundation;


namespace Plugin.MediaManager
{
    public static class AVMediaHelper
    {        
        #region ArePortsPresent
        private static readonly NSString[] Output_Port_Internal =
        {
            AVAudioSession.PortBuiltInSpeaker,
            AVAudioSession.PortBuiltInReceiver
        };

        public static bool ShouldOverrideOutputAudioPort(this AVAudioSessionRouteDescription route)
        {
            bool bFound = route.Outputs.AnyPortsPresentBuiltInSpeaker();
            return bFound;
        }

        public static bool AnyPortsPresentBuiltInSpeaker(this IEnumerable<AVAudioSessionPortDescription> ports)
        {
            List<NSString> portsMissing = null;
            return ports.AnyPortsPresent(Output_Port_Internal, out portsMissing);
        }

        public static bool AnyPortsPresent(this IEnumerable<AVAudioSessionPortDescription> ports, NSString[] portsToSearch, out List<NSString> portsMissing)
        {
            portsMissing = new List<NSString>();
            Dictionary<NSString, List<AVAudioSessionPortDescription>> portsByPortType = ports.ToDictionary();
            bool bFoundAny = false;
            foreach (var portType in portsToSearch)
            {
                List<AVAudioSessionPortDescription> list = null;
                if (portsByPortType.TryGetValue(portType, out list))
                    bFoundAny = true;
                else
                    portsMissing.Add(portType);
            }
            return bFoundAny;
        }
        #endregion

        private static readonly NSString[] Input_Port_Priority =
        {
            AVAudioSession.PortLineIn, //ltang: Highest priority
            AVAudioSession.PortHeadsetMic,
            AVAudioSession.PortBluetoothHfp,
            AVAudioSession.PortUsbAudio,
            AVAudioSession.PortCarAudio,
            AVAudioSession.PortBuiltInMic //ltang: Keep this one at the lowest priority
        };

        /// <summary>
        /// https://stackoverflow.com/questions/31133636/how-to-connect-the-audio-to-bluetooth-when-playing-using-avplayer
        /// </summary>
        /// <param name="audioSession"></param>
        public static AVAudioSessionPortDescription GetPreferredInput(this AVAudioSession audioSession, IEnumerable<NSString> portTypePriorityList)
        {
            AVAudioSessionPortDescription input = null;
            Dictionary<NSString, List<AVAudioSessionPortDescription>> inputsByPortType = GetAvailableInputs(audioSession);
            foreach (var portType in portTypePriorityList)
            {
                List<AVAudioSessionPortDescription> list = null;
                if (inputsByPortType.TryGetValue(portType, out list))
                {
                    //ltang: Found the highest prority
                    input = list.First();
                    break;
                }
            }
            return input;
        }

        public static Dictionary<NSString, List<AVAudioSessionPortDescription>> GetAvailableInputs(this AVAudioSession audioSession)
        {
            var inputsByPortType = new Dictionary<NSString, List<AVAudioSessionPortDescription>>();
            if (audioSession == null || audioSession.AvailableInputs == null)
                return inputsByPortType;
            inputsByPortType = audioSession.AvailableInputs.ToDictionary();
            return inputsByPortType;
        }


        public static void GetInputsOutputs(this AVAudioSessionRouteDescription route
                                            , out Dictionary<NSString, List<AVAudioSessionPortDescription>> inputsByPortType
                                            , out Dictionary<NSString, List<AVAudioSessionPortDescription>> outputByPortType
            )
        {
            inputsByPortType = null;
            outputByPortType = null;
            if (route == null)
                return;
            AVAudioSessionPortDescription[] inputs = route.Inputs;
            AVAudioSessionPortDescription[] outputs = route.Outputs;

            inputsByPortType = inputs.ToDictionary();
            outputByPortType = outputs.ToDictionary();
        }

        public static Dictionary<NSString, List<AVAudioSessionPortDescription>> ToDictionary(this IEnumerable<AVAudioSessionPortDescription> ports)
        {
            var portsByPortType = new Dictionary<NSString, List<AVAudioSessionPortDescription>>();
            if (ports != null)
            {
                foreach (var port in ports)
                {
                    NSString portType = port.PortType;
                    List<AVAudioSessionPortDescription> list = null;
                    if (!portsByPortType.TryGetValue(portType, out list))
                    {
                        list = new List<AVAudioSessionPortDescription>();
                        portsByPortType.Add(portType, list);
                    }
                    list.Add(port);
                }
            }
            return portsByPortType;
        }

        public static string ToText(this AVAudioSession audioSession)
        {
            StringBuilder st = new StringBuilder();
            st.AppendLine($"Mode: {audioSession.Mode}, Category: {audioSession.Category}");
            st.AppendLine(audioSession.AvailableInputs.ToText("Available Inputs"));
            st.AppendLine(audioSession.CurrentRoute.ToText());
            return st.ToString().Trim();
        }

        public static string ToText(this AVAudioSessionRouteDescription route)
        {
            StringBuilder st = new StringBuilder();
            AVAudioSessionPortDescription[] inputs = route.Inputs;
            AVAudioSessionPortDescription[] outputs = route.Outputs;
            st.AppendLine($"{ToText(inputs, "Inputs")}");
            st.AppendLine($"{ToText(outputs, "Outputs")}");
            return st.ToString().Trim();
        }

        public static string ToText(this IEnumerable<AVAudioSessionPortDescription> ports, string descriptions = null)
        {
            var st = new StringBuilder();            
            int count = 0;
            if (ports != null)
            {
                foreach (var port in ports)
                {
                    st.AppendLine($"{++count}: {port.PortName}, {port.PortType}");
                }
            }
            st.Insert(0, $"Ports ({descriptions}): {count}\n");
            return st.ToString().Trim();
        }
    }
}