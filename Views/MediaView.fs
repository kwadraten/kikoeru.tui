module kikoeru.tui.Views.MediaView

open Hex1b
open Hex1b.Widgets

let render () : Hex1bWidget seq =
    // TODO
    seq {
        TextBlockWidget "yes!" :> Hex1bWidget
        TextBlockWidget "时间过的很快喵" :> Hex1bWidget
    }
