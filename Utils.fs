module kikoeru.tui.Utils

open System
open Argu
open kikoeru.tui.WebApiType

type CliArguments =
    | [<MainCommand; Unique>] WorkId of workId: int
    | [<AltCommandLine("-u"); AltCommandLine("--server"); AltCommandLine("--url"); Unique>] Base_Url of baseUrl: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | WorkId _ -> "work id to load and play."
            | Base_Url _ -> "kikoeru/neokikoeru backend API base URL."

type CliOptions =
    { WorkId: int option
      BaseUrl: string option }

let private argumentParser =
    ArgumentParser.Create<CliArguments>(programName = "kikoeru.tui")

let private normalizeBaseUrl (text: string) =
    let value = text.Trim().TrimEnd('/')

    if String.IsNullOrWhiteSpace value then None else Some value

let parseCliArguments (argv: string array) =
    try
        let results = argumentParser.ParseCommandLine(inputs = argv, raiseOnUsage = false)

        match results.TryGetResult <@ WorkId @> with
        | Some workId when workId <= 0 -> Error "work id 必须是正整数"
        | workId ->
            Ok
                { WorkId = workId
                  BaseUrl = results.TryGetResult <@ Base_Url @> |> Option.bind normalizeBaseUrl }
    with :? ArguParseException as ex ->
        Error ex.Message

let private hasAudioSuffix (text: string) =
    let value =
        try
            Uri(text).AbsolutePath
        with _ ->
            text.Split('?')[0]

    value.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
    || value.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)

let private isAudio item =
    match item with
    | Audio file ->
        hasAudioSuffix file.title
        || hasAudioSuffix file.mediaStreamUrl
        || hasAudioSuffix file.mediaDownloadUrl
        || (file.streamLowQualityUrl |> Option.exists hasAudioSuffix)
    | _ -> false

let rec dfs (visitedNode: ItemDto list) (fileTree: ItemDto list) : Folder option =
    match fileTree with
    | [] -> None
    | head :: tail ->
        match head with
        | Audio _
        | Image _
        | Text _
        | Other _ -> dfs (head :: visitedNode) tail
        | Folder f ->
            let children = f.children |> List.ofArray

            if children |> List.exists isAudio then
                Some f
            else
                match dfs (head :: visitedNode) children with
                | Some f -> Some f
                | None -> dfs visitedNode tail

let formatDuration (seconds: float) =
    if Double.IsNaN seconds || Double.IsInfinity seconds || seconds < 0.0 then
        "--:--"
    else
        let total = int (Math.Floor seconds)
        let minutes = total / 60
        let seconds = total % 60
        sprintf "%02d:%02d" minutes seconds

let renderSpectrum (width: int) (height: int) (bands: float array) =
    if width <= 0 || height <= 0 then
        Array.empty
    else
        let sampleAt x =
            if bands.Length = 0 then
                0.0
            elif width <= 1 || bands.Length = 1 then
                bands[0]
            else
                let position =
                    float x / float (width - 1) * float (bands.Length - 1)

                let left = int (Math.Floor position)
                let right = Math.Min(left + 1, bands.Length - 1)
                let weight = position - float left
                bands[left] * (1.0 - weight) + bands[right] * weight

        let visible = Array.init width sampleAt

        [| for row in height .. -1 .. 1 ->
               visible
               |> Array.map (fun sample ->
                   let clamped = Math.Clamp(sample, 0.0, 1.0)
                   if int (Math.Ceiling(clamped * float height)) >= row then '█' else ' ')
               |> String |]
