module kikoeru.tui.Views.SearchView

open Hex1b
open Hex1b.Widgets

let render () : Hex1bWidget seq =
    // TODO
    seq {
        TextBlockWidget "yes!" :> Hex1bWidget
        TextBlockWidget "这也行？" :> Hex1bWidget
    }
