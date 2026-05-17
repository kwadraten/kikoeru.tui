module kikoeru.tui.ViewState

open System
open System.Threading
open kikoeru.tui.Audio
open kikoeru.tui.Utils
open kikoeru.tui.WebApi
open kikoeru.tui.WebApiType

let private defaultBaseUrl = "https://api.asmr-200.com/api"
let private healthCheckIntervalMs = 3000

type PlayingWorkType =
    { id: int
      title: string
      duration: int
      circle: string
      vas: string array
      tags: string array }

type PlayingListItemType =
    { title: string
      duration: float
      mediaStreamUrl: string
      mediaDownloadUrl: string
      streamLowQualityUrl: string
      size: int }

type PlayingListType = PlayingListItemType array

type PlaybackMode =
    | ListPlay
    | ListLoop
    | SingleLoop
    | Shuffle

type GlobalModel() =
    member val BaseUrl = defaultBaseUrl with get, set
    member val IsOnline = false with get, set
    member val CurrentWork: PlayingWorkType option = None with get, set
    member val PlayingList: PlayingListType = Array.empty with get, set
    member val PlayingIndex = -1 with get, set
    member val PlaybackMode = ListPlay with get, set
    member val WorkId = 0 with get, set
    member val ActiveTabIndex = 0 with get, set
    member val IsPlaying = false with get, set
    member val PlaybackStatus = PlaybackStatus.Idle "空闲" with get, set
    member val PlayingFile: PlayingListItemType option = None with get, set
    member val PlayingDuration = 0.0 with get, set
    member val PlayingTime = 0.0 with get, set
    member val Spectrum = Array.zeroCreate<float> 64 with get, set

    member this.HasPlaying() = this.PlayingList.Length > 0

let globalState = new GlobalModel()

let private healthMonitorLock = obj ()
let mutable private healthMonitorStarted = false
let mutable private playbackToken = CancellationToken.None
let mutable private playbackGeneration = 0
let mutable private trackCts: CancellationTokenSource option = None
let private random = Random()

let private nextPlaybackGeneration () =
    playbackGeneration <- playbackGeneration + 1
    playbackGeneration

let playbackStatusText status =
    match status with
    | PlaybackStatus.Idle message when String.IsNullOrWhiteSpace message -> "空闲"
    | PlaybackStatus.Idle message -> message
    | PlaybackStatus.Loading message -> message
    | PlaybackStatus.Playing message when String.IsNullOrWhiteSpace message -> "播放中"
    | PlaybackStatus.Playing message -> message
    | PlaybackStatus.Paused -> "已暂停"
    | PlaybackStatus.Stopped -> "已停止"
    | PlaybackStatus.Finished -> "播放结束"
    | PlaybackStatus.Failed message -> sprintf "出错了：%s" message

let serverStatusBarText isOnline =
    if isOnline then "● 服务器在线" else "× 服务器离线"

let playbackModeText mode =
    match mode with
    | ListPlay -> "列表播放"
    | ListLoop -> "列表循环"
    | SingleLoop -> "单曲循环"
    | Shuffle -> "随机播放"

let serverDisplayName () =
    try
        Uri(globalState.BaseUrl).Host
    with _ ->
        globalState.BaseUrl

let private checkServerAlive () =
    match GetHealth globalState.BaseUrl with
    | Ok message -> message.Trim().Equals("OK", StringComparison.OrdinalIgnoreCase)
    | Error _ -> false

let private updateServerStatus () =
    globalState.IsOnline <- checkServerAlive ()

let private startServerHealthMonitor (ct: CancellationToken) =
    let shouldStart =
        lock healthMonitorLock (fun () ->
            if healthMonitorStarted then
                false
            else
                healthMonitorStarted <- true
                true)

    if shouldStart then
        async {
            while not ct.IsCancellationRequested do
                updateServerStatus ()
                do! Async.Sleep healthCheckIntervalMs
        }
        |> fun monitor -> Async.Start(monitor, ct)

let private audioToPlayingItem (file: File) =
    { title = file.title
      duration = file.duration |> Option.defaultValue 0.0
      mediaStreamUrl = file.mediaStreamUrl
      mediaDownloadUrl = file.mediaDownloadUrl
      streamLowQualityUrl = file.streamLowQualityUrl |> Option.defaultValue ""
      size = file.size }

let private folderToPlayingList (folder: Folder) =
    folder.children
    |> Array.choose (function
        | Audio file when not (String.IsNullOrWhiteSpace file.mediaStreamUrl) -> Some(audioToPlayingItem file)
        | _ -> None)

let private inferWorkFromList workId (playingList: PlayingListType) =
    { id = workId
      title =
        playingList
        |> Array.tryHead
        |> Option.map _.title
        |> Option.defaultValue (sprintf "Work %d" workId)
      duration = playingList |> Array.sumBy (fun item -> int (Math.Ceiling item.duration))
      circle = ""
      vas = Array.empty
      tags = Array.empty }

let private workToPlayingWork (work: WorkDto) =
    { id = work.id
      title = work.title
      duration = work.duration
      circle = work.circle.name
      vas = work.vas |> Array.map _.name
      tags = work.tags |> Array.choose (fun tag -> tag.name) }

let private setCurrentPlayingItem index =
    let item = globalState.PlayingList[index]
    globalState.PlayingIndex <- index
    globalState.PlayingFile <- Some item
    globalState.PlayingDuration <- item.duration
    globalState.PlayingTime <- 0.0
    globalState.Spectrum <- Array.zeroCreate<float> 64

let rec private startTrackAt index =
    if index >= 0 && index < globalState.PlayingList.Length then
        let generation = nextPlaybackGeneration ()
        trackCts |> Option.iter (fun cts -> cts.Cancel())
        StopPlayback()
        setCurrentPlayingItem index

        let item = globalState.PlayingList[index]
        globalState.PlaybackStatus <- PlaybackStatus.Loading(sprintf "已找到音频：%s" item.title)
        let cts = CancellationTokenSource.CreateLinkedTokenSource playbackToken
        trackCts <- Some cts

        PlayFromURLWithStatusAsync
            item.mediaStreamUrl
            (Some item.duration)
            (fun status current duration -> setPlaybackStatus generation status current duration)
            pushSpectrumFrame
        |> fun playback -> Async.Start(playback, cts.Token)

and private nextIndexFromMode automatic =
    let count = globalState.PlayingList.Length

    if count = 0 then
        None
    else
        match globalState.PlaybackMode with
        | SingleLoop when automatic -> Some globalState.PlayingIndex
        | Shuffle when count = 1 -> Some 0
        | Shuffle ->
            let mutable next = random.Next(count)

            while next = globalState.PlayingIndex do
                next <- random.Next(count)

            Some next
        | ListLoop -> Some((globalState.PlayingIndex + 1) % count)
        | ListPlay ->
            let next = globalState.PlayingIndex + 1
            if next < count then Some next else None
        | SingleLoop ->
            let next = globalState.PlayingIndex + 1

            if next < count then
                Some next
            else
                Some globalState.PlayingIndex

and private handlePlaybackFinished generation =
    if generation = playbackGeneration then
        match nextIndexFromMode true with
        | Some index -> startTrackAt index
        | None ->
            globalState.IsPlaying <- false
            globalState.PlaybackStatus <- PlaybackStatus.Finished

and setPlaybackStatus generation status current duration =
    if generation = playbackGeneration then
        let displayStatus =
            match status with
            | PlaybackStatus.Playing _ ->
                let filename =
                    globalState.PlayingFile |> Option.map _.title |> Option.defaultValue "未知音频"

                let currentTrack = globalState.PlayingIndex + 1
                let totalTracks = globalState.PlayingList.Length

                PlaybackStatus.Playing($"播放中: {filename} ({currentTrack}/{totalTracks})")
            | _ -> status

        globalState.PlaybackStatus <- displayStatus

        globalState.IsPlaying <-
            match displayStatus with
            | PlaybackStatus.Playing _ -> true
            | _ -> false

        globalState.PlayingTime <- current
        globalState.PlayingDuration <- duration

        match status with
        | PlaybackStatus.Finished -> handlePlaybackFinished generation
        | _ -> ()

and pushSpectrumFrame spectrum = globalState.Spectrum <- spectrum

let PlayPreviousTrack () =
    let count = globalState.PlayingList.Length

    if count > 0 then
        let index =
            if globalState.PlayingIndex <= 0 then
                count - 1
            else
                globalState.PlayingIndex - 1

        startTrackAt index

let PlayNextTrack () =
    let count = globalState.PlayingList.Length

    match count with
    | 0 -> startTrackAt 0
    | _ -> ((globalState.PlayingIndex + 1) % count) |> startTrackAt


let TogglePlayPause () =
    match globalState.PlaybackStatus with
    | PlaybackStatus.Playing _
    | PlaybackStatus.Paused -> TogglePlayback()
    | _ when globalState.PlayingList.Length > 0 ->
        let index =
            if
                globalState.PlayingIndex >= 0
                && globalState.PlayingIndex < globalState.PlayingList.Length
            then
                globalState.PlayingIndex
            else
                0

        startTrackAt index
    | _ -> ()

let SeekPlaybackBy seconds = SeekRelative seconds

let TogglePlaybackMode () =
    globalState.PlaybackMode <-
        match globalState.PlaybackMode with
        | ListPlay -> ListLoop
        | ListLoop -> SingleLoop
        | SingleLoop -> Shuffle
        | Shuffle -> ListPlay

let private loadWorkAndPlay (workId: int) (ct: CancellationToken) =
    async {
        globalState.WorkId <- workId
        globalState.PlaybackStatus <- PlaybackStatus.Loading(sprintf "正在读取 work %d 的媒体文件..." workId)

        match GetTracks globalState.BaseUrl { id = workId } with
        | Ok tracks ->
            match tracks |> List.ofArray |> dfs [] with
            | Some folder ->
                let playingList = folderToPlayingList folder

                if playingList.Length = 0 then
                    globalState.PlaybackStatus <- PlaybackStatus.Failed(sprintf "work %d 的媒体文件夹中没有可播放音频" workId)
                else
                    globalState.PlayingList <- playingList

                    globalState.CurrentWork <-
                        match GetWork globalState.BaseUrl { id = workId } with
                        | Ok work -> Some(workToPlayingWork work)
                        | Error _ -> Some(inferWorkFromList workId playingList)

                    startTrackAt 0
            | None -> globalState.PlaybackStatus <- PlaybackStatus.Failed(sprintf "work %d 中没有找到包含 mp3 音频的文件夹" workId)
        | Error message ->
            globalState.PlaybackStatus <- PlaybackStatus.Failed(sprintf "读取 work %d 失败：%s" workId message)
    }
    |> fun load -> Async.Start(load, ct)

let InitializeFromArgs (argv: string array) (ct: CancellationToken) =
    playbackToken <- ct

    match parseCliArguments argv with
    | Ok options ->
        match options.BaseUrl with
        | Some baseUrl -> globalState.BaseUrl <- baseUrl
        | None -> ()

        startServerHealthMonitor ct

        match options.WorkId with
        | Some workId -> loadWorkAndPlay workId ct
        | None -> globalState.PlaybackStatus <- PlaybackStatus.Idle "空闲"
    | Error message -> globalState.PlaybackStatus <- PlaybackStatus.Failed message
