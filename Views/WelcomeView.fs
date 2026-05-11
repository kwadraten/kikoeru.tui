module kikoeru.tui.Views.WelcomeView

open Hex1b
open Hex1b.Widgets
open kikoeru.tui.ViewState

let render () =
    let centeredText text =
        AlignWidget(TextBlockWidget text, Alignment.Center).Fill() :> Hex1bWidget

    let logo =
        [| "   __    _  __                           __         _ "
           "  / /__ (_)/ /__ ___  ___  ____ __ __   / /_ __ __ (_)"
           " /  '_// //  '_// _ \/ -_)/ __// // /_ / __// // // / "
           "/_/\_\/_//_/\_\ \___/\__//_/   \_,_/(_)\__/ \_,_//_/  " |]

    seq {
        AlignWidget(
            VStackWidget
                [| centeredText "A Terminal Player for Kikoeru/Neokikoeru Backend."
                   yield! [| for t in logo -> centeredText t |]
                   centeredText (sprintf "当前服务器：%s" (serverDisplayName ()))
                   playbackStatusText globalState.PlaybackStatus |> centeredText |],
            Alignment.Center
        )
            .Fill()
        :> Hex1bWidget
    }
