// Urgh! Sorry. This is maybe the only way to reference nuget packages restored using the "new" way...
#r @"C:\Users\Isaac\.nuget\packages\octokit\0.24.0\lib\net45\Octokit.dll"

open Octokit
open Octokit.Internal
open System
open System.Text.RegularExpressions

[<AutoOpen>]
module Api =
    let createGitHubClient args =
        let information = ProductHeaderValue "github-contributions"
        match args with
        | None -> GitHubClient information
        | Some creds ->
            let store = creds |> Credentials |> InMemoryCredentialStore
            GitHubClient(information, store)

    let search searchTerm (client:ISearchClient) =
        let rec getRequests page allResults = async {
            printfn "Searching %s %d" searchTerm page
            let! results =
                let request = SearchIssuesRequest(searchTerm, Page = page)
                client.SearchIssues request |> Async.AwaitTask
            let allResults = (results.Items |> List.ofSeq) :: allResults
            if allResults |> List.sumBy List.length = results.TotalCount then return allResults |> List.rev |> List.concat
            else return! getRequests (page + 1) allResults }
        getRequests 1 []
    
    // Same as above but using lazy sequences, not recursion. Also, not async.
    let searchSimple searchTerm (client:ISearchClient) =
        Seq.initInfinite ((+) 1)
        |> Seq.map(fun page ->
            SearchIssuesRequest(searchTerm, Page = page)
            |> client.SearchIssues
            |> Async.AwaitTask
            |> Async.RunSynchronously)
        |> Seq.takeWhile(fun a -> a.Items.Count <> 0)
        |> Seq.collect(fun a -> a.Items)
        |> Seq.toList

    let getRepositoryName =
        let repoNameRegex = Regex @"^https://api\.github\.com/repos/(?<repo>[a-zA-Z0-9.-]+/[a-zA-Z0-9.-]+)/issues/\d+$"
        fun (issue:Issue) -> repoNameRegex.Match(string issue.Url).Groups.["repo"].Value

let client = createGitHubClient (Some "<Enter PAT here>")
let author = "<Enter author here>"
let searchTerm = sprintf "is:pr author:%s" author
let pullRequests = client.Search |> search searchTerm |> Async.RunSynchronously

// Calculate the stats - just a list of (repo name * PRs)
let lookup =
    pullRequests
    |> List.filter(fun pr -> pr.CreatedAt.DateTime > DateTime.UtcNow.AddYears -1)
    |> List.countBy getRepositoryName
    |> List.filter(fun (repo, _) -> repo.StartsWith author |> not)
    |> List.sortBy(fun (repo, prCount) -> -prCount, repo)

// Print out summary to FSI
printfn "Number of repositories: %d" lookup.Length
printfn "Number of pull requests: %A" (lookup |> List.sumBy snd)
printfn ""
for (repo, prCount) in lookup do
    printfn "%s: %d" repo prCount