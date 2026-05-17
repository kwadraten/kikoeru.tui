module kikoeru.tui.View

open System.Threading
open Hex1b
open Hex1b.Widgets
open kikoeru.tui.ViewState
open kikoeru.tui.Views

let InitializeFromArgs (argv: string array) (ct: CancellationToken) = ViewState.InitializeFromArgs argv ct

let WidgetsTree () =
    let firstTabPara b =
        match b with
        | true -> TabItemWidget("播放", fun ctx -> PlayingView.render ())
        | false -> TabItemWidget("欢迎", fun ctx -> WelcomeView.render ())

    (VStackWidget
        [| TabPanelWidget(
               [| firstTabPara (globalState.HasPlaying())
                  TabItemWidget("媒体库", fun t -> MediaView.render ())
                  TabItemWidget("搜索", fun t -> SearchView.render ()) |]
           )
               .Selector()
               .Fill()
           PlayerView.render () |])
        .RedrawAfter(250)
