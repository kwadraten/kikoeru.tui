module kikoeru.tui.Views.PlayerView

open System.Threading.Tasks
open Hex1b
open Hex1b.Widgets
open kikoeru.tui.Audio
open kikoeru.tui.Utils
open kikoeru.tui.ViewState

let render () =
    // 注意button和tab类对汉字的支持不好
    // 汉字数 * 2 + 4 这四个字符的宽度来源于`[  ]`
    let btnFactory (text: string) onClick =
        let btn =
            ButtonWidget(text)
                .FixedWidth(12)
                .OnClick(fun ctx ->
                    onClick ()
                    Task.CompletedTask)

        AlignWidget(btn, Alignment.Center).Fill()

    let playButtonText () =
        match globalState.PlaybackStatus with
        | PlaybackStatus.Playing _ -> "暂停"
        | PlaybackStatus.Paused -> "播放"
        | _ -> "播放"

    let progress =
        ProgressWidget(
            Minimum = 0.0,
            Maximum =
                (if globalState.PlayingDuration > 0.0 then
                     globalState.PlayingDuration
                 else
                     1.0),
            Value = globalState.PlayingTime
        )

    let statusBar =
        InfoBarWidget
            [| InfoBarSectionWidget(globalState.IsOnline |> serverStatusBarText |> TextBlockWidget)
               InfoBarDividerWidget " | "
               InfoBarSectionWidget(playbackStatusText globalState.PlaybackStatus |> TextBlockWidget)
               InfoBarSpacerWidget()
               InfoBarSectionWidget(
                   $"{formatDuration globalState.PlayingTime} / {formatDuration globalState.PlayingDuration}"
                   |> TextBlockWidget
               ) |]

    VStackWidget
        [| progress
           HStackWidget(
               [| btnFactory "上一曲" PlayPreviousTrack
                  btnFactory "-5s" (fun () -> SeekPlaybackBy -5.0)
                  AlignWidget(
                      ButtonWidget(playButtonText ()).OnClick(fun ctx ->
                          TogglePlayPause()
                          Task.CompletedTask),
                      Alignment.Center
                  )
                      .Fill()
                  btnFactory "+5s" (fun () -> SeekPlaybackBy 5.0)
                  btnFactory "下一曲" PlayNextTrack
                  btnFactory (playbackModeText globalState.PlaybackMode) TogglePlaybackMode |]
           )
               .Fill()
           statusBar |]
