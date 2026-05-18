module kikoeru.tui.WebApiType

open System.Text.Json

(*
// 命名原则：
// 1. Get和Post开头的是请求方法
// 2. Payload是请求载荷
// 2. Dto是返回的数据的类型，一般是json
// 3. 其它type都是用在Payload或者Dto当中的type，采用小驼峰
// 4. 由于传回的json里面同时有大小驼峰和下划线，不便于统一转换，故直接维持其原样为字段名
 *)

(* // 保活及对路由的基本约束
// 当前活跃实现
// https://github.com/Number178/kikoeru-express/blob/alpha/routes/index.js
// 除了/health以外没有其他接口，且/health只是固定返回OK，直接用.Net的Result就能处理
// 故本部分不必添加任何Type
// router.get('/health', (req, res) => {
//   res.send('OK');
// })
*)

(* // 第一部分 元数据相关接口
// 当前活跃实现：
// https://github.com/Number178/kikoeru-express/blob/alpha/routes/metadata.js
// 已经废弃的实现：
// https://github.com/kikoeru-project/kikoeru-express/blob/unstable/routes/metadata.js
*)

// ==== 载荷部分 ====
type WorkPayload = { id: int }

// GET list of work ids
// router.get('/works',
type WorksPayload =
    { order: string // "id" | "release" | "create_date" | "dl_count" | "review_count" | "price" | "rate_average_2dp" | "nsfw" | "random"
      sort: string option // "desc" | "asc"
      page: int option
      seed: int option // 当order为random的时候才能seed
      pageSize: int // order为random是用于支持随心听功能 pageSize只能为1
      subtitle: bool }

// GET list of works by searching the keyword
// router.get('/search/:keyword?',
type WorksByKeywordPayload =
    { keyword: string
      order: string // 同 WorksPayload
      sort: string option // 同 WorksPayload
      page: int option
      pageSize: int
      subtitle: bool }

type propertyID =
    | CircleAndTagID of int
    | VaID of string

// GET list of circles/tags/VAs
// router.get('/:field(circle|tag|va)s/',
type PropertiesPayload = { field: string } // "circle" | "tag" | "va"

// GET name of a circle/tag/VA
// router.get('/:field(circle|tag|va)s/:id',
type PropertyPayload =
    { field: string // "circle" | "tag" | "va"
      id: int }

// GET list of work ids, restricted by circle/tag/VA
// router.get('/:field(circle|tag|va)s/:id/works',
type WorksByPropertyPayload =
    { field: string // "circle" | "tag" | "va"
      id: propertyID
      order: string // 同 WorksPayload
      sort: string option // 同 WorksPayload
      page: int option
      pageSize: int
      subtitle: bool }

// ==== 响应部分 ====

type I18n = Map<string, {| name: string option |}>

type CircleDto =
    { id: int
      name: string
      source_id: string
      i18n: I18n option
      source_type: string }

type TagDto =
    { id: int
      name: string option
      i18n: I18n option }

type va = { id: string; name: string }

type PropertiesDto =
    | CircleDtos of CircleDto array
    | TagDtos of TagDto array
    | VaDtos of va array

type PropertyDto =
    | CircleDto of CircleDto
    | TagDto of TagDto


type pagination =
    { currentPage: int
      pageSize: int
      totalCount: int }

type rateDetailItem =
    { review_point: int
      count: int
      ratio: int }

type rankItem =
    { term: string
      category: string
      rank: int
      rank_date: string }

type translationInfo =
    { lang: string option
      is_child: bool
      is_parent: bool
      is_original: bool
      is_volunteer: bool
      //   child_worknos: string array // array内部具体类型不确定
      //   parent_workno: string option
      //   original_workno: string option
      //   translation_status_for_translator: string array // array内部具体类型不确定
      //   translation_bonus_langs: string array
      is_translation_agree: bool
      is_translation_bonus_child: bool }

type languageEdition =
    { lang: string
      label: string
      workno: string
      edition_id: int
      edition_type: string
      display_order: int }

type languageEditionInDB =
    { id: int
      is_original: bool
      lang: string
      source_id: string
      source_type: string
      title: string }

type WorkDto =
    { id: int
      title: string
      circle_id: int
      name: string
      nsfw: bool
      release: string
      dl_count: int
      price: int
      review_count: int
      rate_count: int
      rate_average_2dp: float
      rate_count_detail: rateDetailItem array
      rank: rankItem array option
      has_subtitle: bool
      create_date: string
      vas: va array
      tags: TagDto array
      language_editions: JsonElement
      //   original_workno: string option
      other_language_editions_in_db: languageEditionInDB array
      translation_info: translationInfo option
      work_attributes: string
      age_category_string: string
      duration: int
      source_type: string
      source_id: string
      source_url: string
      playlistStatus: Map<string, bool> option // 登陆之后才有
      circle: CircleDto
      samCoverUrl: string
      thumbnailCoverUrl: string
      mainCoverUrl: string }


type WorksDto =
    { pagination: pagination
      works: WorkDto array }


type fileSource =
    { id: int
      source_id: string
      source_type: string }

type ItemDto =
    | Folder of Folder
    | Audio of File
    | Image of File
    | Text of File
    | Other of File

and Folder = // 递归的类型必须以大写字母开头
    { ``type``: string // "folder"
      title: string
      children: ItemDto array }

and File =
    { ``type``: string // "audio" | "image" | "text" | "other"
      hash: string
      title: string
      work: fileSource
      workTitle: string
      duration: float option
      mediaStreamUrl: string
      mediaDownloadUrl: string
      streamLowQualityUrl: string option
      size: int }

type TrackDto = ItemDto array
