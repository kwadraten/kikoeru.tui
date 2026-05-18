module kikoeru.tui.Components.Placeholder

open Hex1b
open Hex1b.Widgets

let render (tip: Hex1bWidget) : Hex1bWidget =
    let centeredText text =
        AlignWidget(TextBlockWidget text, Alignment.Center).Fill() :> Hex1bWidget

    AlignWidget(
        VStackWidget
            [| centeredText "A Terminal Player for Kikoeru/Neokikoeru Backend."
               FigletTextWidget("kikoeru.tui").Font(FigletFonts.Slant).Layout(FigletLayoutMode.Smushed)
               AlignWidget(tip, Alignment.Center).Fill() :> Hex1bWidget |],
        Alignment.Center
    )
        .Fill()
    :> Hex1bWidget
