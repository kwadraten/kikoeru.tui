module kikoeru.tui.WebApi

open System
open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open System.Web
open kikoeru.tui.WebApiType

// ==== 前置配置与工具函数 ====

let private jsonOptions =
    // 具体含义参见文档：
    // https://github.com/Tarmil/FSharp.SystemTextJson/blob/master/docs/Customizing.md
    JsonFSharpOptions()
        .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
        .WithUnionInternalTag()
        .WithUnionUnwrapRecordCases()
        .WithUnionTagName("type")
        .WithSkippableOptionFields(SkippableOptionFields.Always, deserializeNullAsNone = true) // AllowNullFields = true 允许 JSON 中缺少某个字段时将其解析为 None
        .ToJsonSerializerOptions()

let private httpClient = new HttpClient()

let private fetch<'T> (url: string) : 'T =
    let response = httpClient.GetAsync(url).Result
    response.EnsureSuccessStatusCode() |> ignore
    let content = response.Content.ReadAsStringAsync().Result
    JsonSerializer.Deserialize<'T>(content, jsonOptions)

let private connect (url: string) : string =
    let response = httpClient.GetAsync(url).Result
    response.EnsureSuccessStatusCode() |> ignore
    response.Content.ReadAsStringAsync().Result

// ==== 提取辅助函数 ====

let private getPropertyIdString (id: propertyID) =
    match id with
    | CircleAndTagID i -> string i
    | VaID s -> s

// ==== 组装 URL 参数 ====

let private buildQueryParams
    (order: string)
    (sort: string option)
    (page: int option)
    (seed: int option)
    (pageSize: int)
    (subtitle: bool)
    =
    let query = HttpUtility.ParseQueryString(String.Empty)
    query.["order"] <- order

    match sort with
    | Some s -> query.["sort"] <- s
    | None -> ()

    match page with
    | Some p -> query.["page"] <- string p
    | None -> ()

    match seed with
    | Some s -> query.["seed"] <- string s
    | None -> ()

    query.["pageSize"] <- string pageSize
    // 后端不接受bool，而是代之以0和1
    query.["subtitle"] <- if subtitle then "1" else "0"

    "?" + query.ToString()


// ==== 请求方法部分具体实现 ====

// GET health status
let GetHealth (baseUrl: string) : Result<string, string> =
    try
        let url = sprintf "%s/health" baseUrl
        Ok(connect url)
    with ex ->
        Error ex.Message

// GET work metadata
let GetWork (baseUrl: string) (payload: WorkPayload) : Result<WorkDto, string> =
    try
        let url = sprintf "%s/work/%d" baseUrl payload.id
        Ok(fetch<WorkDto> url)
    with ex ->
        Error ex.Message

// GET list of work ids
let GetWorks (baseUrl: string) (payload: WorksPayload) : Result<WorksDto, string> =
    try
        let queryArgs =
            buildQueryParams payload.order payload.sort payload.page payload.seed payload.pageSize payload.subtitle

        let url = sprintf "%s/works%s" baseUrl queryArgs
        Ok(fetch<WorksDto> url)
    with ex ->
        Error ex.Message

// GET list of works by searching the keyword
let GetSearch (baseUrl: string) (payload: WorksByKeywordPayload) : Result<WorksDto, string> =
    try
        let queryArgs =
            buildQueryParams payload.order payload.sort payload.page None payload.pageSize payload.subtitle

        let encodedKeyword = Uri.EscapeDataString(payload.keyword)
        let url = sprintf "%s/search/%s%s" baseUrl encodedKeyword queryArgs
        Ok(fetch<WorksDto> url)
    with ex ->
        Error ex.Message

// GET list of circles/tags/VAs
let GetProperties (baseUrl: string) (payload: PropertiesPayload) : Result<PropertiesDto, string> =
    try
        let url = sprintf "%s/%ss/" baseUrl payload.field

        match payload.field with
        | "circle" -> Ok(PropertiesDto.CircleDtos(fetch<CircleDto array> url))
        | "tag" -> Ok(PropertiesDto.TagDtos(fetch<TagDto array> url))
        | "va" -> Ok(PropertiesDto.VaDtos(fetch<va array> url))
        | _ -> Error "Unknown field type"
    with ex ->
        Error ex.Message

// GET name of a circle/tag/VA
let GetProperty (baseUrl: string) (payload: PropertyPayload) : Result<PropertyDto, string> =
    try
        let url = sprintf "%s/%ss/%d" baseUrl payload.field payload.id

        match payload.field with
        | "circle" -> Ok(PropertyDto.CircleDto(fetch<CircleDto> url))
        | "tag" -> Ok(PropertyDto.TagDto(fetch<TagDto> url))
        | "va" -> Error "VA endpoint is not supported by id"
        | _ -> Error "Unknown field type"
    with ex ->
        Error ex.Message

// GET list of work ids, restricted by circle/tag/VA
let GetWorksByProperty (baseUrl: string) (payload: WorksByPropertyPayload) : Result<WorksDto, string> =
    try
        let idStr = getPropertyIdString payload.id

        let queryArgs =
            buildQueryParams payload.order payload.sort payload.page None payload.pageSize payload.subtitle

        let url = sprintf "%s/%ss/%s/works%s" baseUrl payload.field idStr queryArgs
        Ok(fetch<WorksDto> url)
    with ex ->
        Error ex.Message

// GET list of tracks
let GetTracks (baseUrl: string) (payload: WorkPayload) : Result<TrackDto, string> =
    try
        let url = sprintf "%s/tracks/%d" baseUrl payload.id
        Ok(fetch<TrackDto> url)
    with ex ->
        Error ex.Message
