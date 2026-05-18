module kikoeru.tui.Views.DetailView

open System
open System.Collections.Generic
open Hex1b
open Hex1b.Widgets
open kikoeru.tui.Components
open kikoeru.tui.Utils
open kikoeru.tui.ViewState
open kikoeru.tui.WebApi
open kikoeru.tui.WebApiType

type private DetailLoadState =
    { mutable Work: PlayingWorkType option
      mutable Tracks: TrackDto option
      mutable TreeItems: TreeItemWidget array option
      mutable IsLoading: bool
      mutable StatusText: string }

let private states = Dictionary<int, DetailLoadState>()
let private stateLock = obj ()

let private rootFolder tracks =
    { ``type`` = "folder"
      title = "根目录"
      children = tracks }

let private isPlayableAudio (file: File) =
    not (String.IsNullOrWhiteSpace file.mediaStreamUrl)

let private fileIcon item =
    match item with
    | Audio _ -> "A"
    | Image _ -> "I"
    | Text _ -> "T"
    | Other _ -> "O"
    | Folder _ -> "D"

let private getState workId =
    lock stateLock (fun () ->
        match states.TryGetValue workId with
        | true, state -> state
        | false, _ ->
            let state =
                { Work = None
                  Tracks = None
                  TreeItems = None
                  IsLoading = false
                  StatusText = "正在准备详情..." }

            states[workId] <- state
            state)

let private ensureLoaded workId =
    let state = getState workId

    if state.Work.IsNone && not state.IsLoading then
        state.IsLoading <- true
        state.StatusText <- sprintf "正在加载 work %d..." workId

        async {
            let workResult = GetWork globalState.BaseUrl { id = workId }
            let trackResult = GetTracks globalState.BaseUrl { id = workId }

            match workResult, trackResult with
            | Ok work, Ok tracks ->
                state.Work <- Some(workToPlayingWork work)
                state.Tracks <- Some tracks
                state.StatusText <- "选择音频文件开始播放"
            | Error message, _ -> state.StatusText <- sprintf "详情加载失败：%s" message
            | _, Error message -> state.StatusText <- sprintf "文件列表加载失败：%s" message

            state.IsLoading <- false
        }
        |> Async.Start

let rec private treeItemFor (work: PlayingWorkType) (parent: Folder) (targetFolder: Folder option) (item: ItemDto) =
    match item with
    | Folder folder ->
        let childItems = folder.children |> Array.map (treeItemFor work folder targetFolder)

        let isTarget = targetFolder |> Option.exists ((=) folder)

        let shouldExpand = isTarget || (childItems |> Array.exists snd)

        TreeItemWidget(folder.title).Icon(fileIcon item).Children(childItems |> Array.map fst).Expanded(shouldExpand),
        shouldExpand
    | Audio file ->
        let label =
            if isPlayableAudio file then
                file.title
            else
                sprintf "%s (不可播放)" file.title

        let play () =
            if isPlayableAudio file then
                PlayWorkFolderFile work parent file

        TreeItemWidget(label).Icon(fileIcon item).OnActivated(fun _ -> play ()).OnClicked(fun _ -> play ()), false
    | Image file
    | Text file
    | Other file -> TreeItemWidget(file.title).Icon(fileIcon item), false

let private ensureTreeItems (state: DetailLoadState) work tracks =
    match state.TreeItems with
    | Some items -> items
    | None ->
        let root = rootFolder tracks
        let targetFolder = tracks |> List.ofArray |> dfs []

        let items = tracks |> Array.map (treeItemFor work root targetFolder >> fst)

        state.TreeItems <- Some items
        items

let private treeView (items: TreeItemWidget array) =
    BorderWidget(TreeWidget(items).Fill()).Title(" 文件 ").Fill() :> Hex1bWidget

let render workId : Hex1bWidget seq =
    ensureLoaded workId

    let state = getState workId

    seq {
        match state.Work with
        | Some work -> WorkInfo.render WorkInfo.Text work
        | None -> TextBlockWidget(sprintf "work %d" workId) :> Hex1bWidget

        match state.Work, state.Tracks with
        | Some work, Some tracks when tracks.Length > 0 -> treeView (ensureTreeItems state work tracks)
        | Some _, Some _ -> Placeholder.render (TextBlockWidget("该作品没有文件") :> Hex1bWidget)
        | _ -> Placeholder.render (TextBlockWidget(state.StatusText) :> Hex1bWidget)
    }
