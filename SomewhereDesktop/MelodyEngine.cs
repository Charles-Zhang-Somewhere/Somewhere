using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SomewhereDesktop
{
    /// <summary>
    /// A class responsible for parsing "Melody" syntax
    /// </summary>
    internal class MelodyEngine
    {
        #region Constructor
        public MelodyEngine(string script)
            => Script = script;
        private string Script { get; }
        #endregion

        #region Shared Single Resource
        private static OutputDevice OutDevice = null;
        private static Object Locker = new object();
        private static int UserCount = 0;
        public static void RequireDispose()
        {
            lock(Locker)
            {
                if (OutDevice != null)
                {
                    OutDevice.Dispose();
                    OutDevice = null;
                }
            }
        }
        #endregion

        #region General Interface
        public enum Note
        {
            // -1
            C_1 = 0,
            CSharp_1,
            D_1,
            DSharp_1,
            E_1,
            F_1,
            FSharp_1,
            G_1,
            GSharp_1,
            A_1,
            ASharp_1,
            B_1,
            // 0
            C0,
            CSharp0,
            D0,
            DSharp0,
            E0,
            F0,
            FSharp0,
            G0,
            GSharp0,
            A0,
            ASharp0,
            B0,
            // 1
            C1,
            CSharp,
            D1,
            DSharp1,
            E1,
            F1,
            FSharp1,
            G1,
            GSharp1,
            A1,
            ASharp1,
            B1,
            // 2
            C2,
            CSharp2,
            D2,
            DSharp2,
            E2,
            F2,
            FSharp2,
            G2,
            GSharp2,
            A2,
            ASharp2,
            B2,
            // 3
            C3,
            CSharp3,
            D3,
            DSharp3,
            E3,
            F3,
            FSharp3,
            G3,
            GSharp3,
            A3,
            ASharp3,
            B3,
            // 4
            C4,
            CSharp4,
            D4,
            DSharp4,
            E4,
            F4,
            FSharp4,
            G4,
            GSharp4,
            A4,
            ASharp4,
            B4,
            // 5
            C5,
            CSharp5,
            D5,
            DSharp5,
            E5,
            F5,
            FSharp5,
            G5,
            GSharp5,
            A5,
            ASharp5,
            B5,
            // 6
            C6,
            CSharp6,
            D6,
            DSharp6,
            E6,
            F6,
            FSharp6,
            G6,
            GSharp6,
            A6,
            ASharp6,
            B6,
            // 7
            C7,
            CSharp7,
            D7,
            DSharp7,
            E7,
            F7,
            FSharp7,
            G7,
            GSharp7,
            A7,
            ASharp7,
            B7,
            // 8
            C8,
            CSharp8,
            D8,
            DSharp8,
            E8,
            F8,
            FSharp8,
            G8,
            GSharp8,
            A8,
            ASharp8,
            B8,
            // 9
            C9,
            CSharp9,
            D9,
            DSharp9,
            E9,
            F9,
            FSharp9,
            G9
        }
        // Mapping from drum preset range to drum key
        public static Dictionary<string, int[]> DrumRanges = new Dictionary<string, int[]>()
        {
            // Name, key mapping: 123456789
            { "default", new int[]{ 35, 36, 37, 38, 39, 40, 41, 42, 43 } }
        };
        public static Dictionary<string, int> InstrumentMapping = new Dictionary<string, int>()
        {
            // Notice those are 0-indexed (data1 has max value of 127)
            // General MIDI family name preferred: Family Name, Program Change Preferred #, Program Change # Range
            { "piano", 0}, // Range: 1-8
            { "chromatic percussion", 10}, // Range: 9-16
            { "organ", 19}, // Range: 17-24
            { "guitar", 25}, // Range: 25-32
            { "bass", 38}, // Range: 33-40
            { "strings", 42}, // Range: 41-48
            { "ensemble", 52}, // Range: 49-56
            { "brass", 55}, // Range: 57-64
            { "reed", 64}, // Range: 65-72
            { "pipe", 73}, // Range: 73-80
            { "synth lead", 80}, // Range: 81-88
            { "synth pad", 88}, // Range: 89-96
            { "synth effects", 98}, // Range: 97-104
            { "ethnic", 106}, // Range: 105-112
            { "percussive", 117}, // Range: 113-120
            { "sound effects", 127}, // Range: 121-128
            // General MIDI Specific Instrument: Instrument Name, PC#
            {"acoustic grand piano", 0},
            {"bright acoustic piano", 1},
            {"electric grand piano", 2},
            {"honky-tonk piano", 3},
            {"electric piano 1", 4},
            {"electric piano 2", 5},
            {"harpsichord", 6},
            {"clavi", 7},
            {"celesta", 8},
            {"glockenspiel", 9},
            {"music box", 10},
            {"vibraphone", 11},
            {"marimba", 12},
            {"xylophone", 13},
            {"tubular bells", 14},
            {"dulcimer", 15},
            {"drawbar organ", 16},
            {"percussive organ", 17},
            {"rock organ", 18},
            {"church organ", 19},
            {"reed organ", 20},
            {"accordion", 21},
            {"harmonica", 22},
            {"tango accordion", 23},
            {"acoustic guitar (nylon)", 24},
            {"acoustic guitar (steel)", 25},
            {"electric guitar (jazz)", 26},
            {"electric guitar (clean)", 27},
            {"electric guitar (muted)", 28},
            {"overdriven guitar", 29},
            {"distortion guitar", 30},
            {"guitar harmonics", 31},
            {"acoustic bass", 32},
            {"electric bass (finger)", 33},
            {"electric bass (pick)", 34},
            {"fretless bass", 35},
            {"slap bass 1", 36},
            {"slap bass 2", 37},
            {"synth bass 1", 38},
            {"synth bass 2", 39},
            {"violin", 40},
            {"viola", 41},
            {"cello", 42},
            {"contrabass", 43},
            {"tremolo strings", 44},
            {"pizzicato strings", 45},
            {"orchestral harp", 46},
            {"timpani", 47},
            {"string ensemble 1", 48},
            {"string ensemble 2", 49},
            {"synthstrings 1", 50},
            {"synthstrings 2", 51},
            {"choir aahs", 52},
            {"voice oohs", 53},
            {"synth voice", 54},
            {"orchestra hit", 55},
            {"trumpet", 56},
            {"trombone", 57},
            {"tuba", 58},
            {"muted trumpet", 59},
            {"french horn", 60},
            {"brass section", 61},
            {"synthbrass 1", 62},
            {"synthbrass 2", 63},
            {"soprano sax", 64},
            {"alto sax", 65},
            {"tenor sax", 66},
            {"baritone sax", 67},
            {"oboe", 68},
            {"english horn", 69},
            {"bassoon", 70},
            {"clarinet", 71},
            {"piccolo", 72},
            {"flute", 73},
            {"recorder", 74},
            {"pan flute", 75},
            {"blown bottle", 76},
            {"shakuhachi", 77},
            {"whistle", 78},
            {"ocarina", 79},
            {"lead 1 (square)", 80},
            {"lead 2 (sawtooth)", 81},
            {"lead 3 (calliope)", 82},
            {"lead 4 (chiff)", 83},
            {"lead 5 (charang)", 84},
            {"lead 6 (voice)", 85},
            {"lead 7 (fifths)", 86},
            {"lead 8 (bass + lead)", 87},
            {"pad 1 (new age)", 88},
            {"pad 2 (warm)", 89},
            {"pad 3 (polysynth)", 90},
            {"pad 4 (choir)", 91},
            {"pad 5 (bowed)", 92},
            {"pad 6 (metallic)", 93},
            {"pad 7 (halo)", 94},
            {"pad 8 (sweep)", 95},
            {"fx 1 (rain)", 96},
            {"fx 2 (soundtrack)", 97},
            {"fx 3 (crystal)", 98},
            {"fx 4 (atmosphere)", 99},
            {"fx 5 (brightness)", 100},
            {"fx 6 (goblins)", 101},
            {"fx 7 (echoes)", 102},
            {"fx 8 (sci-fi)", 103},
            {"sitar", 104},
            {"banjo", 105},
            {"shamisen", 106},
            {"koto", 107},
            {"kalimba", 108},
            {"bag pipe", 109},
            {"fiddle", 110},
            {"shanai", 111},
            {"tinkle bell", 112},
            {"agogo", 113},
            {"steel drums", 114},
            {"woodblock", 115},
            {"taiko drum", 116},
            {"melodic tom", 117},
            {"synth drum", 118},
            {"reverse cymbal", 119},
            {"guitar fret noise", 120},
            {"breath noise", 121},
            {"seashore", 122},
            {"bird tweet", 123},
            {"telephone ring", 124},
            {"helicopter", 125},
            {"applause", 126},
            {"gunshot", 127},
            // Spcial mapping
            {"drum", 117 }  // "drum" for "percussive"
        };
        public static string TimeSignaturePattern = "^\\((\\d+/\\d+)(-(\\d+))?\\)";
        public static string InstrumentInstructionPattern = "^{(.*?)(:(.*?))?}";
        /// <summary>
        /// Play as MIDI music
        /// </summary>
        public void Play()
        {
            lock(Locker)
            {
                // Acquire device only when it's not already acquired
                if (OutDevice == null)
                    OutDevice = new OutputDevice(0);
                // Increment user count
                Interlocked.Increment(ref UserCount);
            }
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();

                int NotationToNoteID(string notation)
                    => (int)Enum.Parse(typeof(Note), notation.ToUpper().Replace("-1", "_1").Replace("#", "Sharp"));
                void PlayNote(int note, int volume = 127, int delay = 1000)
                    => PlayNotes(new int[] { note }, volume, delay);
                void PlayNotes(int[] notes, int volume, int delay)
                {
                    // On
                    foreach (var note in notes)
                        HoldNote(note, volume);
                    // Hold
                    Thread.Sleep(delay);
                    // Off
                    foreach (var note in notes)
                        ReleaseNote(note);
                }
                void HoldNote(int note, int volume)
                {
                    lock(Locker)
                    {
                        // Premture closing
                        if (OutDevice == null)
                            return;
                        OutDevice.Send(builder, ChannelCommand.NoteOn, note, volume, 0);
                    }                    
                }
                void ReleaseNote(int note)
                {
                    lock (Locker)
                    {
                        // Premture closing
                        if (OutDevice == null)
                            return;
                        OutDevice.Send(builder, ChannelCommand.NoteOff, note, 0, 0);
                    }
                }
                void SetInstrument(string instrument)
                {
                    lock(Locker)
                    {
                        // Prematur close
                        if (OutDevice == null || 
                            // Ignore invalid instrument instruction
                            !InstrumentMapping.ContainsKey(instrument))
                            return;
                        OutDevice.Send(builder, ChannelCommand.ProgramChange, InstrumentMapping[instrument], 0, 0);
                    }
                }

                // Extract tempo
                int tempoDelay = GetTempoDelay(120);   // From temp to milisec delay; Default 120
                var reg = Regex.Match(Script, TimeSignaturePattern);
                string rawNotes = Script;
                if(reg.Success == true)
                {
                    string tempo = reg.Groups[3].Value;
                    if (!string.IsNullOrEmpty(tempo))
                        tempoDelay = GetTempoDelay(Convert.ToInt32(tempo));
                    // Divide by time step scaling, default at 4th per step
                    string timeSignature = reg.Groups[1].Value;
                    if (!string.IsNullOrEmpty(timeSignature))
                    {
                        int targetStep = Convert.ToInt32(timeSignature.Substring(timeSignature.IndexOf('/') + 1));
                        float scale = 4 / (float)targetStep;
                        tempoDelay = (int)(scale * tempoDelay);
                    }
                    rawNotes = Script.Substring(reg.Value.Length);
                }
                // Very basic implementation for just simple notes
                int currentLevel = 4;
                string currentInstrument = null;
                string drumRangeSet = DrumRanges.Keys.First();
                for (int i = 0; i < rawNotes.Length; i++)
                {
                    var note = rawNotes[i];
                    // Premture closing
                    if (OutDevice == null)
                        return;
                    // Helper methods
                    void PlayNoteKeyName(string name)
                        => PlayNoteContinued(NotationToNoteID(name));
                    void PlayNoteContinued(int id)
                    {
                        // Get continue
                        int count = 1;
                        while (i + count < rawNotes.Length 
                            && rawNotes[i + count] == '-')
                            count++;
                        PlayNote(id, delay: tempoDelay * count);
                    }
                    // Handled or ignore, just continue
                    if (// Ignore white space
                        note == ' ' ||
                        // Continuity handled
                        note == '-')
                        continue;
                    else if (note == '#')
                        currentLevel++;
                    else if (note == '$')
                        currentLevel--;
                    // Keyboard notes
                    else if (MediumNotes.Contains(note))
                        PlayNoteKeyName(note + currentLevel.ToString());
                    else if (HighNotes.Contains(note))
                        PlayNoteKeyName(note + (currentLevel + 1).ToString());
                    // Drump notes
                    else if(DrumNotes.Contains(note))
                    {
                        // First entry and re-entry
                        if (currentInstrument == null || !InstrumentMapping.ContainsKey(currentInstrument)
                            || InstrumentMapping[currentInstrument] < 113
                            || InstrumentMapping[currentInstrument] > 120)
                            SetInstrument("drum");
                        int drumIndex = Convert.ToInt32($"{note}"); // In C# char is interpreted directly as int, so put it in a string
                        if (drumIndex > 9) continue;    // Skip invalid
                        PlayNoteContinued(GetDrumKey(drumRangeSet, drumIndex));
                    }
                    // Instrument
                    else if(note == '{')
                    {
                        var match = Regex.Match(rawNotes.Substring(i), InstrumentInstructionPattern);
                        string instrument = match.Groups[1].Value;
                        string preset = match.Groups[3].Value;
                        SetInstrument(instrument.ToLower());
                        drumRangeSet = !string.IsNullOrEmpty(preset) ? preset.ToLower() : drumRangeSet;
                        i += match.Length - 1;
                    }
                    // Stop
                    else if (// Default step
                        note == '~' ||
                        // Drump stop
                        note == '0')
                        Thread.Sleep(tempoDelay);
                    else
                        // Skip unidentified
                        continue;
                }
            }
            lock(Locker)
            {
                // Decrement user count
                Interlocked.Decrement(ref UserCount);
                // Release device only when it's not being used by anyone else
                if (OutDevice != null && OutDevice.IsDisposed == false && UserCount == 0)
                {
                    OutDevice.Dispose();
                    OutDevice = null;
                }
            }
        }
        /// <summary>
        /// Render as HTML
        /// </summary>
        public string Render(string libPath, string cssPath)
        {
            string BuildABC()
            {
                // Extract tempo
                int tempoDelay = GetTempoDelay(120);   // From temp to milisec delay; Default 120
                var reg = Regex.Match(Script, "^\\(.*-(\\d+)\\)");
                string rawNotes = Script;
                if (reg.Success == true)
                {
                    tempoDelay = GetTempoDelay(Convert.ToInt32(reg.Groups[1].Value));
                    rawNotes = Script.Substring(reg.Value.Length);
                }

                StringBuilder builder = new StringBuilder(
@"X:1
T:Melody
M:4/4
C:Trad.
K:C
");
                builder.Append("|");
                int counter = 0;
                int currentLevel = 4;
                char prevNote = ' ';
                int prevLevel = currentLevel;
                // Tone and temp etc. are not fully implemented
                foreach (var note in rawNotes)
                {
                    if (note == ' ')
                        continue;
                    else if (note == '#')
                        currentLevel++;
                    else if (note == '$')
                        currentLevel--;
                    else if (MediumNotes.Contains(note))
                    {
                        prevNote = char.ToUpper(note);
                        prevLevel = currentLevel;
                        builder.Append(char.ToUpper(note) + 2.ToString());    // 4th note use 2, also ABC's note is inverted per Melody
                        counter++;
                    }
                    else if (HighNotes.Contains(note))
                    {
                        prevNote = char.ToLower(note);
                        prevLevel = currentLevel + 1;
                        builder.Append(char.ToLower(note) + 2.ToString());    // 4th note use 2
                        counter++;
                    }
                    else if (note == '-')    // Continue
                    {
                        builder.Append(prevNote + 2.ToString());    // 4th note use 2
                        counter++;
                    }

                    if(counter == 4)
                    {
                        builder.Append("|");
                        counter = 0;
                    }
                }

                return builder.ToString();
            }

            return @"<!DOCTYPE HTML>
<html>
<head>
	<meta charset=""utf-8"">
	<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
	<title>abcjs editor</title>

	<link rel=""stylesheet"" href=""https://maxcdn.bootstrapcdn.com/font-awesome/4.5.0/css/font-awesome.min.css"">
	<link href=""" + cssPath /* We depend on this locally because no CDN is available */ + @""" rel=""stylesheet""/>
	<script src=""" + libPath + @"""></script>
	<style>
		.abcjs-inline-midi {
			max-width: 740px;
		}
		@media print {
			h1, p, textarea, #selection, #midi, #midi-download, hr {
				display: none;
			}
			.paper {
				position: absolute;
			}
		}
	</style>
</head>
<body>
<h1>Melody Sketch: </h1>

<div id=""paper0"" class=""paper""></div>
<div id=""paper1"" class=""paper""></div>
<div id=""paper2"" class=""paper""></div>
<div id=""paper3"" class=""paper""></div>
<div id=""selection""></div>

<p>Modify the generated abc tune in the area below to make adjustments. Also notice that you can click on the drawn notes and
	see the place in the text where that note is defined.</p>

<p>For more information, see <a href=""https://github.com/paulrosen/abcjs"" >the project page</a>.</p>
<textarea name=""abc"" id=""abc"" cols=""80"" rows=""15"">" + BuildABC() + @"
</textarea>

<hr />
<div id=""midi""></div>
<div id=""midi-download""></div>
<div id=""warnings""></div>

<script type=""text/javascript"">
	function selectionCallback(abcelem) {
		var note = {};
		for (var key in abcelem) {
			if (abcelem.hasOwnProperty(key) && key !== ""abselem"")
				note[key] = abcelem[key];
		}
		console.log(abcelem);
		var el = document.getElementById(""selection"");
		el.innerHTML = ""<b>selectionCallback parameter:</b><br>"" + JSON.stringify(note);
	}

	function initEditor() {
		new ABCJS.Editor(""abc"", { paper_id: ""paper0"",
			generate_midi: true,
			midi_id:""midi"",
			midi_download_id: ""midi-download"",
			generate_warnings: true,
			warnings_id:""warnings"",
			abcjsParams: {
				generateDownload: true,
				clickListener: selectionCallback
			}
		});
	}

	window.addEventListener(""load"", initEditor, false);
</script>
</body>
</html>";
        }
        #endregion

        #region Sub Routines
        private static readonly char[] MediumNotes = "cdefgab".ToCharArray();
        private static readonly char[] HighNotes = "CDEFGAB".ToCharArray();
        private static readonly char[] DrumNotes = "123456789".ToCharArray();
        /// <param name="rangeSet">Lower case</param>
        /// <param name="index">Must be in 1-9, we will map it to 0-8</param>
        private int GetDrumKey(string rangeSet, int index)
        {
            if (DrumRanges.ContainsKey(rangeSet))
                return DrumRanges[rangeSet][index];
            else
                // For invalid drum range just use default
                return DrumRanges.Keys.First()[index];
        }
        private int GetTempoDelay(int tempo)
                => (int)((float)60 /* 60 seconds */ / tempo * 1000);
        #endregion
    }

    public static class OutputDeviceExtension
    {
        public static void Send(this OutputDevice device, ChannelCommand command, int data1, int data2 = 0, int midiChannel = 0)
            => Send(device, new ChannelMessageBuilder(), command, data1, data2, midiChannel);
        public static void Send(this OutputDevice device, ChannelMessageBuilder builder, 
            ChannelCommand command, int data1, int data2 = 0, int midiChannel = 0)
        {
            builder.Command = command;
            builder.Data1 = data1;
            builder.Data2 = data2;
            builder.MidiChannel = midiChannel;
            builder.Build();
            device.Send(builder.Result);
        }
    }
}
