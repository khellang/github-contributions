using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octokit;
using Octokit.Internal;

namespace GithubContributions
{
    public static class Program
    {
        private static readonly Regex RepoNameRegex = new Regex(
            @"^https://api\.github\.com/repos/(?<repo>[a-zA-Z0-9.-]+/[a-zA-Z0-9.-]+)/issues/\d+$");

        public static async Task<int> Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: github-contributions <author> [token]");
                return 1;
            }

            var client = CreateGitHubClient(args);

            var author = args[0];
            var searchTerm = $"is:pr author:{author}";

            var pullRequests = await FetchPullRequests(client.Search, searchTerm);

            var lookup = pullRequests
                .GroupBy(GetRepositoryName)
                .Where(x => !x.Key.StartsWith(author))
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key);

            WriteResults(lookup);

            return 0;
        }

        private static void WriteResults(IOrderedEnumerable<IGrouping<string, Issue>> lookup)
        {
            Console.WriteLine($"Number of repositories: {lookup.Count()}");
            Console.WriteLine($"Number of pull requests: {lookup.Sum(x => x.Count())}");

            Console.WriteLine();

            foreach (var group in lookup)
            {
                Console.WriteLine($"{group.Key}: {group.Count()}");
            }
        }

        private static async Task<List<Issue>> FetchPullRequests(ISearchClient client, string searchTerm)
        {
            var pullRequests = new List<Issue>();

            var request = new SearchIssuesRequest(searchTerm) { Page = 1 };

            var result = await client.SearchIssues(request);

            pullRequests.AddRange(result.Items);

            var page = 2;

            while (pullRequests.Count < result.TotalCount)
            {
                request = new SearchIssuesRequest(searchTerm) { Page = page };

                result = await client.SearchIssues(request);

                pullRequests.AddRange(result.Items);

                page++;
            }

            return pullRequests;
        }

        private static string GetRepositoryName(Issue issue)
        {
            return RepoNameRegex.Match(issue.Url.ToString()).Groups["repo"].Value;
        }

        private static IGitHubClient CreateGitHubClient(IReadOnlyList<string> args)
        {
            var information = new ProductHeaderValue("github-contributions");

            if (args.Count <= 1)
            {
                return new GitHubClient(information);
            }

            var credentials = new Credentials(args[1]);

            var store = new InMemoryCredentialStore(credentials);

            return new GitHubClient(information, store);
        }
    }
}
