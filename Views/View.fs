module kikoeru.tui.View

open System.Threading
open System.Threading.Tasks
open Hex1b
open Hex1b.Input
open Hex1b.Widgets
open kikoeru.tui.ViewState
open kikoeru.tui.Views

let InitializeFromArgs (argv: string array) (ct: CancellationToken) = ViewState.InitializeFromArgs argv ct

let WidgetsTree () =

    let selectedTab index (tab: TabItemWidget) =
        tab.Selected(globalState.ActiveTabIndex = index)

    let closeIcon workId =
        IconWidget("x").OnClick(fun _ -> CloseDetailView workId)

    let detailTab index workId =
        TabItemWidget(sprintf "详情: %d" workId, fun _ -> DetailView.render workId)
            .RightActions(fun _ -> [| closeIcon workId |])
        |> selectedTab index

    let tabs =
        [| yield TabItemWidget("播放", fun _ -> PlayingView.render ()) |> selectedTab 0
           yield TabItemWidget("媒体库", fun _ -> MediaView.render ()) |> selectedTab 1
           yield TabItemWidget("搜索", fun _ -> SearchView.render ()) |> selectedTab 2

           for index, workId in globalState.DetailTabWorkIds |> List.indexed do
               yield detailTab (3 + index) workId |]

    (VStackWidget
        [| TabPanelWidget(tabs)
               .OnSelectionChanged(fun e ->
                   globalState.ActiveTabIndex <- e.SelectedIndex
                   Task.CompletedTask)
               .Selector()
               .Fill()
           PlayerView.render () |])
        .RedrawAfter(250)
        .InputBindings(fun bindings ->
            bindings.Key(Hex1bKey.Tab).Global().Action((fun () -> SelectNextTab()), "下一个页面")
            bindings.Shift().Key(Hex1bKey.Tab).Global().Action((fun () -> SelectPreviousTab()), "上一个页面")
            bindings.Key(Hex1bKey.LeftArrow).Global().Action((fun () -> SeekPlaybackBy -5.0), "后退 5 秒")
            bindings.Key(Hex1bKey.RightArrow).Global().Action((fun () -> SeekPlaybackBy 5.0), "前进 5 秒"))
