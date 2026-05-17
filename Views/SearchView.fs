module kikoeru.tui.Views.SearchView

open System
open Hex1b
open Hex1b.Widgets
open kikoeru.tui.Components
open kikoeru.tui.ViewState
open kikoeru.tui.WebApi
open kikoeru.tui.WebApiType

let private pageSize = 20
let private stateLock = obj ()
let private searchResults = ResizeArray<PlayingWorkType>()
let mutable private keyword = ""
let mutable private nextPage = 1
let mutable private hasMore = false
let mutable private isLoading = false
let mutable private statusText = "请输入关键词后点击搜索"
let mutable private requestGeneration = 0

let private workToPlayingWork (work: WorkDto) =
    { id = work.id
      title = work.title
      duration = work.duration
      circle = work.circle.name
      vas = work.vas |> Array.map _.name
      tags = work.tags |> Array.choose _.name }

let private copyResults () =
    lock stateLock (fun () -> searchResults |> Seq.toArray)

let private updateResults reset (works: PlayingWorkType array) =
    lock stateLock (fun () ->
        if reset then
            searchResults.Clear()

        searchResults.AddRange works)

let private startLoad reset =
    let targetKeyword = keyword.Trim()

    if String.IsNullOrWhiteSpace targetKeyword then
        lock stateLock (fun () -> searchResults.Clear())
        nextPage <- 1
        hasMore <- false
        statusText <- "请输入关键词后点击搜索"
    elif reset || (not isLoading && hasMore) then
        isLoading <- true
        statusText <- if reset then "搜索中..." else "正在加载下一页..."

        if reset then
            nextPage <- 1
            hasMore <- false
            requestGeneration <- requestGeneration + 1

        let page = nextPage
        let generation = requestGeneration

        async {
            let payload =
                { keyword = targetKeyword
                  order = "nsfw"
                  sort = None
                  page = Some page
                  pageSize = pageSize
                  subtitle = false }

            match GetSearch globalState.BaseUrl payload with
            | Ok dto when generation = requestGeneration ->
                let works = dto.works |> Array.map workToPlayingWork
                updateResults reset works

                let loadedCount = lock stateLock (fun () -> searchResults.Count)

                nextPage <- dto.pagination.currentPage + 1
                hasMore <- loadedCount < dto.pagination.totalCount
                isLoading <- false

                statusText <-
                    if loadedCount = 0 then
                        "没有搜索结果"
                    elif hasMore then
                        sprintf "已加载 %d / %d，滚到底继续加载" loadedCount dto.pagination.totalCount
                    else
                        sprintf "已加载全部 %d 条结果" loadedCount
            | Error message when generation = requestGeneration ->
                isLoading <- false
                statusText <- sprintf "搜索失败：%s" message
            | _ -> ()
        }
        |> Async.Start

let private openDetail (_work: PlayingWorkType) = ()

let private resultsView () =
    let items =
        copyResults ()
        |> Array.map (WorkInfo.render (WorkInfo.Button openDetail))

    match items.Length with
    | 0 -> Placeholder.render statusText
    | _ ->
        let children =
            Array.append items [| AlignWidget(TextBlockWidget(statusText), Alignment.Center) :> Hex1bWidget |]

        ScrollPanelWidget(VStackWidget children, ScrollOrientation.Vertical, true) :> Hex1bWidget

let render () : Hex1bWidget seq =
    let searchBox =
        TextBoxWidget(keyword)
            .OnTextChanged(fun e -> keyword <- e.NewText)
            .OnSubmit(fun e ->
                keyword <- e.Text
                startLoad true)
            .FillWidth()
        :> Hex1bWidget

    let searchButton =
        ButtonWidget("搜索").FixedWidth(8).OnClick(fun _ -> startLoad true) :> Hex1bWidget

    seq {
        HStackWidget([| TextBlockWidget("关键词：") :> Hex1bWidget; searchBox; searchButton |]).FillWidth() :> Hex1bWidget
        resultsView ()
    }
