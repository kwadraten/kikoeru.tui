module kikoeru.tui.Audio

open System
open System.Threading
open NAudio.Dsp
open NAudio.Wave

type PlaybackStatus =
    | Idle of string
    | Loading of string
    | Playing of string
    | Paused
    | Stopped
    | Finished
    | Failed of string

let private syncRoot = obj ()
type private PlaybackSession =
    { Id: int
      Output: WaveOutEvent
      Reader: MediaFoundationReader }

let mutable private currentSession: PlaybackSession option = None
let mutable private nextSessionId = 0

let private createCurrentSession output reader =
    lock syncRoot (fun () ->
        nextSessionId <- nextSessionId + 1
        let session = { Id = nextSessionId; Output = output; Reader = reader }
        currentSession <- Some session
        session.Id)

let private clearCurrentSession sessionId =
    lock syncRoot (fun () ->
        match currentSession with
        | Some session when session.Id = sessionId -> currentSession <- None
        | _ -> ())

let StopPlayback () =
    lock syncRoot (fun () ->
        currentSession
        |> Option.iter (fun session -> session.Output.Stop()))

let TogglePlayback () =
    lock syncRoot (fun () ->
        currentSession
        |> Option.iter (fun session ->
            match session.Output.PlaybackState with
            | PlaybackState.Playing -> session.Output.Pause()
            | PlaybackState.Paused -> session.Output.Play()
            | _ -> ()))

let SeekRelative seconds =
    lock syncRoot (fun () ->
        currentSession
        |> Option.iter (fun session ->
            let totalSeconds = session.Reader.TotalTime.TotalSeconds
            let currentSeconds = session.Reader.CurrentTime.TotalSeconds
            let targetSeconds =
                if totalSeconds > 0.0 then
                    Math.Clamp(currentSeconds + seconds, 0.0, totalSeconds)
                else
                    Math.Max(0.0, currentSeconds + seconds)

            session.Reader.CurrentTime <- TimeSpan.FromSeconds targetSeconds))

let private getTotalSeconds (reader: MediaFoundationReader) (fallback: float option) =
    let seconds = reader.TotalTime.TotalSeconds

    if seconds > 0.0 then seconds
    else fallback |> Option.defaultValue 0.0

type private SpectrumSampleProvider(source: ISampleProvider, onSpectrumFrame: float array -> unit) =
    let fftLength = 1024
    let fftM = int (Math.Log(float fftLength, 2.0))
    let fftBuffer = Array.zeroCreate<Complex> fftLength
    let mutable fftPosition = 0

    let publishSpectrum () =
        let fftData = Array.copy fftBuffer
        FastFourierTransform.FFT(false, fftM, fftData)

        let halfLength = fftLength / 2
        let bandCount = 64

        Array.init bandCount (fun band ->
            let startBin = band * halfLength / bandCount
            let endBin = Math.Max(startBin + 1, (band + 1) * halfLength / bandCount)

            let magnitude =
                fftData[startBin .. endBin - 1]
                |> Array.map (fun value ->
                    let x = float value.X
                    let y = float value.Y
                    Math.Sqrt(x * x + y * y))
                |> Array.max

            Math.Clamp(Math.Log10(1.0 + magnitude * 40.0), 0.0, 1.0))
        |> onSpectrumFrame

    let addSample sample =
        let window = FastFourierTransform.HannWindow(fftPosition, fftLength)
        fftBuffer[fftPosition].X <- float32 (float sample * float window)
        fftBuffer[fftPosition].Y <- 0.0f
        fftPosition <- fftPosition + 1

        if fftPosition >= fftLength then
            publishSpectrum ()
            fftPosition <- 0

    interface ISampleProvider with
        member _.WaveFormat = source.WaveFormat

        member _.Read(buffer, offset, count) =
            let samplesRead = source.Read(buffer, offset, count)
            let channels = Math.Max(1, source.WaveFormat.Channels)
            let last = offset + samplesRead - channels

            if last >= offset then
                for frameOffset in offset .. channels .. last do
                    let mutable mixed = 0.0f

                    for channel in 0 .. channels - 1 do
                        mixed <- mixed + buffer[frameOffset + channel]

                    addSample (mixed / float32 channels)

            samplesRead

let PlayFromURLWithStatusAsync
    (url: string)
    (durationHint: float option)
    (onStatus: PlaybackStatus -> float -> float -> unit)
    (onSpectrumFrame: float array -> unit)
    =
    async {
        let! ct = Async.CancellationToken
        let mutable sessionId = None

        try
            try
                onStatus (Loading "正在载入音频...") 0.0 (durationHint |> Option.defaultValue 0.0)

                use reader = new MediaFoundationReader(url)
                use output = new WaveOutEvent()

                let spectrum = SpectrumSampleProvider(reader.ToSampleProvider(), onSpectrumFrame)

                output.Init(spectrum)
                sessionId <- Some(createCurrentSession output reader)
                output.Play()

                while not ct.IsCancellationRequested
                      && output.PlaybackState <> PlaybackState.Stopped do
                    let totalSeconds = getTotalSeconds reader durationHint
                    let currentSeconds = reader.CurrentTime.TotalSeconds

                    let status =
                        match output.PlaybackState with
                        | PlaybackState.Playing -> Playing ""
                        | PlaybackState.Paused -> Paused
                        | PlaybackState.Stopped -> Stopped
                        | _ -> Idle "空闲"

                    onStatus status currentSeconds totalSeconds
                    do! Async.Sleep 250

                if ct.IsCancellationRequested then
                    output.Stop()
                    onStatus Stopped reader.CurrentTime.TotalSeconds (getTotalSeconds reader durationHint)
                else
                    let totalSeconds = getTotalSeconds reader durationHint
                    let currentSeconds = reader.CurrentTime.TotalSeconds

                    if totalSeconds > 0.0 && currentSeconds >= totalSeconds - 0.25 then
                        onStatus Finished totalSeconds totalSeconds
                    else
                        onStatus Stopped currentSeconds totalSeconds

            with ex ->
                onStatus (Failed ex.Message) 0.0 (durationHint |> Option.defaultValue 0.0)
        finally
            sessionId |> Option.iter clearCurrentSession
    }

let PlayFromURLAsync (url: string) =
    async {
        use mf = new MediaFoundationReader(url)
        use wo = new WaveOutEvent()
        wo.Init(mf)
        wo.Play()

        while wo.PlaybackState = PlaybackState.Playing do
            do! Async.Sleep 1000
    }

let PlayFromURL (url: string) =
    // 开发时ssl报错请安装根证书
    // i.e. One or more errors occurred.
    // (The SSL connection could not be established, see inner exception.)
    // 安装方法：
    // dotnet dev-certs https --trust
    use mf = new MediaFoundationReader(url)
    use wo = new WaveOutEvent()
    wo.Init(mf)
    wo.Play()

    while wo.PlaybackState = PlaybackState.Playing do
        Thread.Sleep 1000
