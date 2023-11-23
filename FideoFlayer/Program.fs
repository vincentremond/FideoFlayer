open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.ViewEngine
open Microsoft.Net.Http.Headers
open Polly

[<RequireQualifiedAccess>]
module Path =
    let combine b a = Path.Combine(a, b)

let videoDirectory =
    Environment.SpecialFolder.UserProfile
    |> Environment.GetFolderPath
    |> Path.combine "stream"
    |> DirectoryInfo

let getVideoFiles () = videoDirectory.GetFiles("*.mp4")


let template (pageTitle: string) (bodyContent: XmlNode list) =
    html [] [
        head [] [
            title [] [ str pageTitle ]
            style [] [
                str
                    """
                    html, body {
                        font-family: sans-serif;
                        background-color: #333;
                        color: #eee;
                    }

                    a {
                        color: #fff;
                        font-weight: bold;
                        text-decoration: none;
                    }
                    
                    a:hover {
                        text-decoration: underline;
                    }

                    .video {
                        max-width: 100%;
                    }
                """
            ]
            script [
                _src "https://kit.fontawesome.com/86c9a3fb46.js"
                _crossorigin "anonymous"
            ] []
        ]
        body [] bodyContent
    ]

let index context =
    htmlView (
        template "FideoFlayer" [
            p [] [ str "Videos" ]
            ul [] [
                for video in getVideoFiles () do
                    li [] [
                        a [ _href $"/watch/%s{video.Name}" ] [ str video.Name ]
                        str " "
                        a [ _href $"/download/%s{video.Name}" ] [ i [ _class "fas fa-download" ] [] ]
                    ]
            ]
        ]
    )

let watch videoName =
    htmlView (
        template $"Video: {videoName}" [
            h1 [] [ str videoName ]
            p [] [
                video [
                    _src $"/download/%s{videoName}"
                    _controls
                    _preload "auto"
                    _class "video"
                ] []
            ]
            p [] [
                a [ _href "/" ] [ str "Back to list" ]
                str " | "
                a [ _href $"/download/%s{videoName}" ] [ str "Download" ]
                str " | "
                a [
                    _href $"/delete/%s{videoName}"
                    _onclick "return confirm('Are you sure you want to delete this video?')"
                ] [ str "Delete" ]
            ]
            p [] [
                h3 [] [ str "Metadata" ]
                let metadataFile = Path.Join(videoDirectory.FullName, videoName + ".json")

                let metadata =
                    if File.Exists(metadataFile) then
                        File.ReadAllText(metadataFile)
                    else
                        "No metadata available"

                pre [] [ str metadata ]
            ]
        ]
    )

let download videoName =
    let video = Path.Join(videoDirectory.FullName, videoName) |> FileInfo
    let enableRangeProcessing = true
    let lastModified = DateTimeOffset(video.LastWriteTimeUtc, TimeSpan.Zero) |> Some

    let etag =
        video.LastWriteTimeUtc.Ticks
        |> (sprintf "\"%i\"")
        |> EntityTagHeaderValue
        |> Some

    setContentType "video/mp4"
    >=> streamFile enableRangeProcessing video.FullName etag lastModified

let delete videoName =
    ResiliencePipelineBuilder()
        .Build()
        .Execute(fun () ->
            let video = Path.Join(videoDirectory.FullName, videoName) |> FileInfo
            video.Delete()
        )

    let permanent = false
    redirectTo permanent "/"

let webApp =
    choose [
        GET >=> route "/" >=> (warbler index)
        GET >=> routef "/watch/%s" watch
        GET >=> routef "/download/%s" download
        GET >=> routef "/delete/%s" delete
    ]

type Startup() =
    member _.ConfigureServices(services: IServiceCollection) =
        // Register default Giraffe dependencies
        services.AddGiraffe() |> ignore

    member _.Configure (app: IApplicationBuilder) (_: IHostEnvironment) (_: ILoggerFactory) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffe webApp

Host
    .CreateDefaultBuilder()
    .ConfigureWebHostDefaults(fun webHostBuilder -> webHostBuilder.UseStartup<Startup>() |> ignore)
    .Build()
    .Run()
