module kikoeru.tui.Components.WorkInfo

open System
open Hex1b
open Hex1b.Widgets
open kikoeru.tui.ViewState

type TitleMode =
    | Text
    | Button of (PlayingWorkType -> unit)

let private ellipsis = "..."
let private buttonFrameWidth = 4
let private borderWidth = 4
let private maxResponsiveWidth = 300

let private joinItems (items: string array) =
    let text =
        items
        |> Array.filter (fun item -> not (String.IsNullOrWhiteSpace item))
        |> String.concat "、"

    if String.IsNullOrWhiteSpace text then "无" else text

let private truncateByDisplayWidth maxWidth text =
    let slice value width =
        let struct (sliced, _, _, _) = DisplayWidth.SliceByDisplayWidth(value, 0, width)
        sliced

    if maxWidth <= 0 then
        ""
    elif DisplayWidth.GetStringWidth text <= maxWidth then
        text
    elif maxWidth <= DisplayWidth.GetStringWidth ellipsis then
        slice ellipsis maxWidth
    else
        let prefixWidth = maxWidth - DisplayWidth.GetStringWidth ellipsis
        slice text prefixWidth + ellipsis

let private centerByDisplayWidth width text =
    let textWidth = DisplayWidth.GetStringWidth text

    if textWidth >= width then
        text
    else
        let padding = width - textWidth
        let left = padding / 2
        let right = padding - left
        String.replicate left " " + text + String.replicate right " "

let private titleForWidth mode width (work: PlayingWorkType) =
    match mode with
    | Text -> truncateByDisplayWidth width work.title
    | Button _ when width <= buttonFrameWidth -> truncateByDisplayWidth width $"» {work.title} «"
    | Button _ ->
        let title = truncateByDisplayWidth (width - buttonFrameWidth) work.title
        centerByDisplayWidth width $"» {title} «"

let private rawTitleWidget mode (work: PlayingWorkType) label =
    match mode with
    | Text -> TextBlockWidget(label).FillWidth() :> Hex1bWidget
    | Button onClick -> ButtonWidget(label).OnClick(fun _ -> onClick work) :> Hex1bWidget

let private titleWidget mode (work: PlayingWorkType) =
    let widthBranch width =
        let usableWidth =
            match mode with
            | Text -> width
            | Button _ -> max 0 (width - borderWidth)

        ConditionalWidget(
            (fun actualWidth _ -> actualWidth = width),
            rawTitleWidget mode work (titleForWidth mode usableWidth work)
        )

    let branches =
        [| yield ConditionalWidget((fun actualWidth _ -> actualWidth <= 0), rawTitleWidget mode work "")

           for width in 1..maxResponsiveWidth do
               yield widthBranch width

           yield
               ConditionalWidget(
                   (fun actualWidth _ -> actualWidth > maxResponsiveWidth),
                   rawTitleWidget mode work (titleForWidth mode maxResponsiveWidth work)
               ) |]

    ResponsiveWidget(branches).FillWidth() :> Hex1bWidget

let render mode (work: PlayingWorkType) =
    BorderWidget(
        VStackWidget(
            [| titleWidget mode work
               TextBlockWidget(
                   sprintf
                       "社团: %s"
                       (if String.IsNullOrWhiteSpace work.circle then
                            "未知"
                        else
                            work.circle)
               )
                   .Fill()
               TextBlockWidget(sprintf "声优: %s" (joinItems work.vas)).Fill()
               TextBlockWidget(sprintf "Tag: %s" (joinItems work.tags)).Fill() |]
        )
    )
    :> Hex1bWidget
