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
    GetTracks baseUrl { id = 1531826 } |> assertOk

[<Fact>]
let ``GetProperty returns successfully`` () =
    GetProperty baseUrl { field = "circle"; id = 61282 } |> assertOk

[<Fact>]
let ``GetWork returns successfully`` () =
    GetWork baseUrl { id = 1531826 } |> assertOk

[<Fact>]
let ``GetProperties returns successfully`` () =
    GetProperties baseUrl { field = "tag" } |> assertOk

[<Fact>]
let ``GetWorks returns successfully`` () =
    GetWorks
        baseUrl
        { order = "release"
          sort = None
          page = None
          seed = None
          pageSize = 20
          subtitle = false }
    |> assertOk

[<Fact>]
let ``GetWorksByProperty returns successfully`` () =
    GetWorksByProperty
        baseUrl
        { field = "tag"
          id = CircleAndTagID 4
          order = "release"
          sort = None
          page = None
          pageSize = 20
          subtitle = false }
    |> assertOk

[<Fact>]
let ``GetSearch returns successfully`` () =
    GetSearch
        baseUrl
        { keyword = "みなせ"
          order = "release"
          sort = None
          page = None
          pageSize = 20
          subtitle = false }
    |> assertOk

[<Fact>]
let ``GetSearch returns correctly`` () =
    // "keyword which doesnt exist"不会返回任何查询结果
    // {"works":[],"pagination":{"currentPage":1,"pageSize":20,"totalCount":0}}
    let result =
        GetSearch
            baseUrl
            { keyword = "keyword which doesnt exist"
              order = "nsfw"
              sort = None
              page = None
              pageSize = 20
              subtitle = false }

    match result with
    | Error message -> Assert.True(false, message)
    | Ok resp ->
        let isCorrect =
            resp = { works = [||]
                     pagination =
                       { currentPage = 1
                         pageSize = 20
                         totalCount = 0 } }

        Assert.True(isCorrect, "未能正确使用keyword或后端有变化")
