module kikoeru.tui.Views.PlayingView

open System
open Hex1b
open Hex1b.Surfaces
open Hex1b.Widgets
open kikoeru.tui.Components
open kikoeru.tui.ViewState

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
            globalState.CurrentWork
            |> Option.map (WorkInfo.render WorkInfo.Text)
            |> Option.defaultValue (Placeholder.render (TextBlockWidget("未选择作品") :> Hex1bWidget))
            TextBlockWidget "" :> Hex1bWidget
            spectrumView ()
        }
    else
        let randomButton =
            ButtonWidget("随便听听").OnClick(fun _ -> PlayRandomWork()) :> Hex1bWidget

        let tip =
            HStackWidget
                [| TextBlockWidget("按Tab键发现更多内容或") :> Hex1bWidget
                   randomButton |]
            :> Hex1bWidget

        seq { Placeholder.render tip }
