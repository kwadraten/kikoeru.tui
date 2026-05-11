module kikoeru.tui.Program

open Hex1b
open Hex1b.Widgets
open System
open System.Threading
open kikoeru.tui.Audio
open kikoeru.tui.View

[<EntryPoint>]
let main (argv: string array) =
    let cts = new CancellationTokenSource()

    Console.CancelKeyPress.Add(fun e ->
        e.Cancel <- true
        StopPlayback()
        cts.Cancel())

    InitializeFromArgs argv cts.Token


    let terminal =
        Hex1bTerminal
            .CreateBuilder()
            .WithDiagnostics("kikoeru.tui", false)
            .WithHex1bApp(fun app option -> Func<RootContext, Hex1bWidget>(fun ctx -> WidgetsTree() :> Hex1bWidget))
            .Build()


    try
        // 也可以 Task |> Async.AwaitTask |> Async.RunSynchronously
        // C# Task -> F# Async -> run async`
        // 这样需要多转换一步
        terminal.RunAsync(cts.Token).GetAwaiter().GetResult() |> ignore
    finally
        StopPlayback()

    0 // 所以还得自己返回0
