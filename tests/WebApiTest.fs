module kikoeru.tui.Tests.WebApiTests

open Xunit
open kikoeru.tui.WebApi
open kikoeru.tui.WebApiType

let private baseUrl = "https://api.asmr-200.com/api"

let private assertOk result =
    match result with
    | Ok _ -> ()
    | Error message -> Assert.True(false, message)

[<Fact>]
let ``GetTracks returns successfully`` () =
    GetTracks baseUrl { id = 1531826 }
    |> assertOk

[<Fact>]
let ``GetProperty returns successfully`` () =
    GetProperty baseUrl { field = "circle"; id = 61282 }
    |> assertOk

[<Fact>]
let ``GetWork returns successfully`` () =
    GetWork baseUrl { id = 1531826 }
    |> assertOk

[<Fact>]
let ``GetProperties returns successfully`` () =
    GetProperties baseUrl { field = "tag" }
    |> assertOk

[<Fact>]
let ``GetWorks returns successfully`` () =
    GetWorks baseUrl { order = "nsfw"; sort = None; page = None; seed = None; pageSize = 20; subtitle = false }
    |> assertOk

[<Fact>]
let ``GetWorksByProperty returns successfully`` () =
    GetWorksByProperty
        baseUrl
        { field = "tag"
          id = CircleAndTagID 4
          order = "nsfw"
          sort = None
          page = None
          pageSize = 20
          subtitle = false }
    |> assertOk

[<Fact>]
let ``GetSearch returns successfully`` () =
    GetSearch baseUrl { keyword = "みなせ"; order = "nsfw"; sort = None; page = None; pageSize = 20; subtitle = false }
    |> assertOk
