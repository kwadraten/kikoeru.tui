module kikoeru.tui.View

open System.Threading
open System.Threading.Tasks
open Hex1b
open Hex1b.Widgets
open kikoeru.tui.ViewState
open kikoeru.tui.Views

let InitializeFromArgs (argv: string array) (ct: CancellationToken) = ViewState.InitializeFromArgs argv ct

let WidgetsTree () =

    let selectedTab index (tab: TabItemWidget) =
        tab.Selected(globalState.ActiveTabIndex = index)

    (VStackWidget
        [| TabPanelWidget(
               [| TabItemWidget("播放", fun ctx -> PlayingView.render ()) |> selectedTab 0
                  TabItemWidget("媒体库", fun t -> MediaView.render ()) |> selectedTab 1
                  TabItemWidget("搜索", fun t -> SearchView.render ()) |> selectedTab 2 |]
           )
               .OnSelectionChanged(fun e ->
                   globalState.ActiveTabIndex <- e.SelectedIndex
                   Task.CompletedTask)
               .Selector()
               .Fill()
           PlayerView.render () |])
        .RedrawAfter(250)
