module kikoeru.tui.Views.MediaView

open System
open Hex1b
open Hex1b.Widgets
open kikoeru.tui.Components
open kikoeru.tui.ViewState
open kikoeru.tui.WebApi
open kikoeru.tui.WebApiType

type private PickerOption = { Label: string; Value: string option }

let private pageSize = 20
let private random = Random()
let private stateLock = obj ()
let private mediaResults = ResizeArray<PlayingWorkType>()
let mutable private order = "release"
let mutable private sort: string option = Some "desc"
let mutable private sortPickerStateKey = obj ()
let mutable private randomSeed: int option = None
let mutable private subtitle = false
let mutable private nextPage = 1
let mutable private hasMore = true
let mutable private isLoading = false
let mutable private initialized = false
let mutable private statusText = "正在加载媒体库..."
let mutable private requestGeneration = 0

let private orderOptions =
    [| { Label = "发售日期"
         Value = Some "release" }
       { Label = "收录时间"
         Value = Some "create_date" }
       { Label = "RJ号码"; Value = Some "id" }
       { Label = "全年龄"; Value = Some "nsfw" }
       { Label = "下载数量"
         Value = Some "dl_count" }
       { Label = "评论数量"
         Value = Some "review_count" }
       { Label = "价格"; Value = Some "price" }
       { Label = "评价"
         Value = Some "rate_average_2dp" }
       { Label = "随机"; Value = Some "random" } |]

let private sortOptions =
    [| { Label = "降序"; Value = Some "desc" }
       { Label = "升序"; Value = Some "asc" } |]

let private subtitleOptions =
    [| { Label = "不限"; Value = Some "0" }; { Label = "有字幕"; Value = Some "1" } |]

let private copyResults () =
    lock stateLock (fun () -> mediaResults |> Seq.toArray)

let private updateResults reset works =
    lock stateLock (fun () ->
        if reset then
            mediaResults.Clear()

        mediaResults.AddRange works)

let private randomApiSeed () = random.Next(1, 100)

let private currentSeed reset =
    if order = "random" then
        if reset || randomSeed.IsNone then
            randomSeed <- Some(randomApiSeed ())

        randomSeed
    else
        randomSeed <- None
        None

let private startLoad reset =
    if reset || (not isLoading && hasMore) then
        initialized <- true
        isLoading <- true
        statusText <- if reset then "正在加载媒体库..." else "正在加载下一页..."

        if reset then
            nextPage <- 1
            hasMore <- false
            requestGeneration <- requestGeneration + 1
            lock stateLock (fun () -> mediaResults.Clear())

        let page = nextPage
        let generation = requestGeneration
        let seed = currentSeed reset

        async {
            let payload =
                { order = order
                  sort = sort
                  page = Some page
                  seed = seed
                  pageSize = pageSize
                  subtitle = subtitle }

            match GetWorks globalState.BaseUrl payload with
            | Ok dto when generation = requestGeneration ->
                let works = dto.works |> Array.map workToPlayingWork
                updateResults reset works

                let loadedCount = lock stateLock (fun () -> mediaResults.Count)

                nextPage <- dto.pagination.currentPage + 1
                hasMore <- loadedCount < dto.pagination.totalCount
                isLoading <- false

                statusText <-
                    if loadedCount = 0 then
                        "没有媒体库结果"
                    elif hasMore then
                        sprintf "已加载 %d / %d，滚到底继续加载" loadedCount dto.pagination.totalCount
                    else
                        sprintf "已加载全部 %d 条结果" loadedCount
            | Error message when generation = requestGeneration ->
                isLoading <- false
                statusText <- sprintf "媒体库加载失败：%s" message
            | _ -> ()
        }
        |> Async.Start

let private optionLabels options = options |> Array.map _.Label

let private picker label options currentIndex onChanged =
    HStackWidget
        [| TextBlockWidget(label) :> Hex1bWidget
           PickerWidget(optionLabels options, InitialSelectedIndex = currentIndex)
               .OnSelectionChanged(fun e ->
                   onChanged e.SelectedIndex
                   startLoad true)
               .FixedWidth(14)
           :> Hex1bWidget |]
    :> Hex1bWidget

let private currentIndex options value =
    options
    |> Array.tryFindIndex (fun item -> item.Value = value)
    |> Option.defaultValue 0

// 与asmr.one的行为对齐
// nsfw其实是非全年龄
// 后端会把非全年龄排在前，全年龄排在后
// 但使用升序倒过来就变成全年龄了
let private setOrder nextOrder =
    order <- nextOrder

    if nextOrder = "nsfw" then
        sort <- Some "asc"
        sortPickerStateKey <- obj ()

    if nextOrder = "random" then
        randomSeed <- Some(randomApiSeed ())
    else
        randomSeed <- None

let private controlsView () =
    let gap = TextBlockWidget("").FixedWidth(2) :> Hex1bWidget
    let spacer = TextBlockWidget("").Fill() :> Hex1bWidget

    let refreshButton =
        ButtonWidget("刷新").OnClick(fun _ -> startLoad true).FixedWidth(8) :> Hex1bWidget

    HStackWidget(
        [| picker "排序:" orderOptions (currentIndex orderOptions (Some order)) (fun index ->
               orderOptions[index].Value |> Option.defaultValue "release" |> setOrder)
           gap
           StatePanelWidget(
               sortPickerStateKey,
               fun _ ->
                   picker "顺序:" sortOptions (currentIndex sortOptions sort) (fun index ->
                       sort <- sortOptions[index].Value)
           )
           :> Hex1bWidget
           gap
           picker "字幕:" subtitleOptions (if subtitle then 1 else 0) (fun index -> subtitle <- index = 1)
           spacer
           refreshButton |]
    )
        .FillWidth()
    :> Hex1bWidget

let private resultsView () =
    let items =
        copyResults ()
        |> Array.map (WorkInfo.render (WorkInfo.Button(fun work -> OpenDetailView work.id)))

    match items.Length with
    | 0 -> Placeholder.render (TextBlockWidget(statusText) :> Hex1bWidget)
    | _ ->
        let children =
            Array.concat
                [ items
                  [| AlignWidget(TextBlockWidget(statusText), Alignment.Center) :> Hex1bWidget |] ]

        ScrollPanelWidget(VStackWidget children, ScrollOrientation.Vertical, true)
            .OnScroll(fun e ->
                if e.IsAtEnd then
                    startLoad false)
        :> Hex1bWidget

let render () : Hex1bWidget seq =
    if not initialized then
        startLoad true

    seq {
        controlsView ()
        resultsView ()
    }
