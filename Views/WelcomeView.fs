module kikoeru.tui.Views.WelcomeView

open Hex1b
open Hex1b.Widgets
open kikoeru.tui.ViewState

let render () =
    let centeredText text =
        AlignWidget(TextBlockWidget text, Alignment.Center).Fill() :> Hex1bWidget

    seq {
        AlignWidget(
            VStackWidget
                [| centeredText "A Terminal Player for Kikoeru/Neokikoeru Backend."
                   FigletTextWidget("kikoeru.tui").Font(FigletFonts.Slant).Layout(FigletLayoutMode.Smushed)
                   centeredText (sprintf "当前服务器：%s" (serverDisplayName ()))
                   playbackStatusText globalState.PlaybackStatus |> centeredText |],
            Alignment.Center
        )
            .Fill()
        :> Hex1bWidget
    }
