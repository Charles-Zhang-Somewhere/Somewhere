﻿using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.Text;
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
        /// <summary>
        /// Play as MIDI music
        /// </summary>
        public async Task Play()
        {
            using (OutputDevice outDevice = new OutputDevice(0))
            {
                ChannelMessageBuilder builder = new ChannelMessageBuilder();

                async Task PlayNoteName(string name, int volume = 127, int delay = 1000)
                    => await PlayNote((int)Enum.Parse(typeof(Note), name.Replace("-1", "_1").Replace("#", "Sharp")), volume, delay);
                async Task PlayNote(int note, int volume = 127, int delay = 1000)
                {
                    builder.Command = ChannelCommand.NoteOn;
                    builder.MidiChannel = 0;
                    builder.Data1 = note;
                    builder.Data2 = volume;
                    builder.Build();

                    outDevice.Send(builder.Result);
                    await Task.Delay(delay);

                    builder.Command = ChannelCommand.NoteOff;
                    builder.Data2 = 0;
                    builder.Build();
                    outDevice.Send(builder.Result);
                }

                await PlayNoteName("C4", delay: 500);
                await PlayNoteName("C4", delay: 500);
                await PlayNoteName("C4", delay: 500);
                await PlayNoteName("E4", delay: 500);
                await PlayNoteName("F4", delay: 500);
            }
        }
        /// <summary>
        /// Render as HTML
        /// </summary>
        public string Render(string libPath, string cssPath)
        {
            string abc = @"X: 1
T: Cooley's
M: 4/4
L: 1/8
R: reel
K: Emin
|:D2|EB{c}BA B2 EB|~B2 AB dBAG|FDAD BDAD|FDAD dAFD|
EBBA B2 EB|B2 AB defg|afe^c dBAF|DEFD E2:|
|:gf|eB B2 efge|eB B2 gedB|A2 FA DAFA|A2 FA defg|
eB B2 eBgB|eB B2 defg|afe^c dBAF|DEFD E2:|
";

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
<textarea name=""abc"" id=""abc"" cols=""80"" rows=""15"">" + abc + @"
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
    }
}
