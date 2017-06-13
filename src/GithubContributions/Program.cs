using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octokit;

namespace GithubContributions
{
    public static class Program
    {
        private static readonly Regex RepoNameRegex = new Regex(
            @"^https://api\.github\.com/repos/(?<repo>[a-zA-Z0-9.-]+/[a-zA-Z0-9.-]+)/issues/\d+$");

        public static int Main(string[] args)
        {
            return MainAsync(args).Result;
        }

        private static async Task<int> MainAsync(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: github-contributions <author>");
                return 1;
            }

            var information = new ProductHeaderValue("github-contributions");

            var client = new GitHubClient(information);

            var searchTerm = string.Format("is:pr author:{0}", args[0]);

            var pullRequests = await FetchPullRequests(client.Search, searchTerm);

            var lookup = pullRequests
                .GroupBy(GetRepositoryName)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key);

            WriteResults(lookup);

            return 0;
        }

        private static void WriteResults(IOrderedEnumerable<IGrouping<string, Issue>> lookup)
        {
            Console.WriteLine("Number of repositories: {0}", lookup.Count());
            Console.WriteLine("Number of pull requests: {0}", lookup.Sum(x => x.Count()));

            Console.WriteLine();

            foreach (var group in lookup)
            {
                Console.WriteLine("{0}: {1}", @group.Key, @group.Count());
            }
        }

        private static async Task<List<Issue>> FetchPullRequests(ISearchClient client, string searchTerm)
        {
            var pullRequests = new List<Issue>();

            var page = 1;

            SearchIssuesResult result;

            do
            {
                var request = new SearchIssuesRequest(searchTerm) { Page = page++ };

                result = await client.SearchIssues(request);

                pullRequests.AddRange(result.Items);
            } while (pullRequests.Count < result.TotalCount);

            return pullRequests;
        }

        private static string GetRepositoryName(Issue issue)
        {
            return RepoNameRegex.Match(issue.Url.ToString()).Groups["repo"].Value;
        }
    }
}
