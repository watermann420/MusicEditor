// MusicEngine License (MEL) - Honor-Based Commercial Support
// Description: Inline punchcard wrapper.

using System.Windows.Controls;
using MusicEngine.Core;
using MusicEngineEditor.Editor;

namespace MusicEngineEditor.Controls.InlineVisuals;

public partial class PunchcardInlineControl : UserControl, ISequencerVisual, IAnimatedVisual
{
    private Sequencer? _sequencer;

    public Sequencer? Sequencer
    {
        get => _sequencer;
        set
        {
            _sequencer = value;
            if (value != null)
                Punch.BindToSequencer(value);
            else
                Punch.UnbindSequencer();
        }
    }

    public PunchcardInlineControl()
    {
        InitializeComponent();
    }

    public void OnFrame()
    {
        // Punchcard has its own dispatcher-based refresh; no-op here.
    }
}
