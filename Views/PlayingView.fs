module kikoeru.tui.Views.PlayingView

open System
open Hex1b
open Hex1b.Surfaces
open Hex1b.Widgets
open kikoeru.tui.Components
open kikoeru.tui.ViewState

let private nonEmptyItems (items: string array) =
    items |> Array.filter (fun item -> not (System.String.IsNullOrWhiteSpace item))

let private chipButton text = ButtonWidget(text) :> Hex1bWidget

let private chipsRow label (items: string array) =
    let chips =
        items
        |> nonEmptyItems
        |> Array.mapi (fun index item ->
            if index = 0 then
                [| chipButton item |]
            else
                [| TextBlockWidget " " :> Hex1bWidget; chipButton item |])
        |> Array.concat

    HStackWidget(Array.concat [ [| TextBlockWidget(label) :> Hex1bWidget |]; chips ]) :> Hex1bWidget

let private workTitle () =
    globalState.CurrentWork |> Option.map _.title |> Option.defaultValue "未选择作品"

let private circleButton () =
    globalState.CurrentWork
    |> Option.bind (fun work ->
        if String.IsNullOrWhiteSpace work.circle then
            None
        else
            Some(chipButton $"社团: {work.circle}"))

let private vasItems () =
    globalState.CurrentWork |> Option.map _.vas |> Option.defaultValue Array.empty

let private tagsItems () =
    globalState.CurrentWork |> Option.map _.tags |> Option.defaultValue Array.empty

let private titleRow () =
    let right =
        circleButton () |> Option.defaultValue (TextBlockWidget "" :> Hex1bWidget)

    HStackWidget(
        [| TextBlockWidget(workTitle ()) :> Hex1bWidget
           AlignWidget(right, Alignment.Right).Fill() :> Hex1bWidget |]
    )
    :> Hex1bWidget

let private metadataView () =
    seq {
        titleRow ()
        chipsRow "声优: " (vasItems ())
        chipsRow "Tag: " (tagsItems ())
    }

let private sampleSpectrum (bands: float array) width x =
    if bands.Length = 0 then
        0.0
    elif width <= 1 || bands.Length = 1 then
        bands[0]
    else
        let position = float x / float (width - 1) * float (bands.Length - 1)

        let left = int (Math.Floor position)
        let right = Math.Min(left + 1, bands.Length - 1)
        let weight = position - float left
        bands[left] * (1.0 - weight) + bands[right] * weight

let private drawSpectrum (surface: Surface) (bands: float array) =
    let width = surface.Width
    let height = surface.Height
    let foreground = Nullable()
    let background = Nullable()

    if width > 0 && height > 0 then
        for x in 0 .. width - 1 do
            let sample = Math.Clamp(sampleSpectrum bands width x, 0.0, 1.0)
            let barHeight = int (Math.Ceiling(sample * float height))
            let firstRow = Math.Max(0, height - barHeight)

            for y in firstRow .. height - 1 do
                surface.WriteChar(x, y, '█', foreground, background, CellAttributes.None)
                |> ignore

let private spectrumView () =
    let surface =
        SurfaceWidget(fun ctx ->
            seq { yield ctx.Layer((fun surface -> drawSpectrum surface globalState.Spectrum), 0, 0) })
            .Fill()

    BorderWidget(surface).Title(" 频谱 ").Fill() :> Hex1bWidget

let render () =
    if globalState.HasPlaying() then
        seq {
            yield! metadataView ()
            TextBlockWidget "" :> Hex1bWidget
            spectrumView ()
        }
    else
        seq { Placeholder.render "请选择一个作品开始播放" }
