using System.IO;
using System.Windows.Forms;
using GUI.Controls;
using GUI.Utils;
using NAudio.Wave;
using NLayer.NAudioSupport;

namespace GUI.Types.Viewers
{
    class Audio : IViewer
    {
        public static bool IsAccepted(uint magic, string fileName)
        {
            return (magic == 0x46464952 /* RIFF */ && fileName.EndsWith(".wav", StringComparison.InvariantCultureIgnoreCase)) ||
                    fileName.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase);
        }

        public TabPage Create(VrfGuiContext vrfGuiContext, byte[] input)
        {
            WaveStream waveStream;

            if (input == null)
            {
                waveStream = new AudioFileReader(vrfGuiContext.FileName);
            }
            else if (vrfGuiContext.FileName.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase))
            {
                waveStream = new Mp3FileReaderBase(new MemoryStream(input), wf => new Mp3FrameDecompressor(wf));
            }
            else
            {
                waveStream = new WaveFileReader(new MemoryStream(input));
            }

            var tab = new TabPage();
            var audio = new AudioPlaybackPanel(waveStream);
            tab.Controls.Add(audio);
            return tab;
        }
    }
}
