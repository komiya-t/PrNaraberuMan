using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Octokit;

namespace PrNaraberuMan
{
	internal static class Program
	{
		private static async Task Main(string[] args)
		{
			if (args.Length != 4)
			{
				Console.WriteLine("以下の4つの引数をください。 ");
				Console.WriteLine("<repository owner> <repository name> <email address> <password>");

				return;
			}

			var (client, repository) = await Authenticate(
				emailAddress: args[2],
				password: args[3],
				repositoryOwner: args[0],
				repositoryName: args[1]
			);

			var pullRequests = await GetNewlyMergedPullRequests(client, repository);

			Output(pullRequests);
		}

		private static async Task<(GitHubClient, Repository)> Authenticate(
			string emailAddress,
			string password,
			string repositoryOwner,
			string repositoryName)
		{
			var client = new GitHubClient(new ProductHeaderValue("PrNaraberuMan"))
			{
				Credentials = new Credentials(emailAddress, password)
			};

			var repository = await client.Repository.Get(repositoryOwner, repositoryName);

			Console.WriteLine($"リポジトリ: {repository.FullName}");
			Console.WriteLine();

			return (client, repository);
		}

		private static async Task<IEnumerable<PullRequest>> GetNewlyMergedPullRequests(IGitHubClient client, Repository repository)
		{
			Console.Write((await client.User.Current()).Login + "、");

			PullRequest lastMergedPrOfPreviousBuild = null;
			while (lastMergedPrOfPreviousBuild == null
				|| !lastMergedPrOfPreviousBuild.Merged)
			{
				Console.WriteLine("前回のビルドで最後にマージされたPR番号を入れてください。");
				Console.Write("#");

				if (int.TryParse(Console.ReadLine(), out int lastMergedPrNumber))
				{
					try
					{
						lastMergedPrOfPreviousBuild = await client.PullRequest.Get(repository.Id, lastMergedPrNumber);
					}
					catch
					{
						Console.WriteLine("PRが見つかりません。");
						Console.WriteLine();

						continue;
					}

					if (lastMergedPrOfPreviousBuild.Merged)
					{
						Console.WriteLine($"『{lastMergedPrOfPreviousBuild.Title}』（-> {lastMergedPrOfPreviousBuild.Base.Ref}）ですね。");
					}
					else
					{
						Console.WriteLine("このPRはマージされていません。");
					}
				}

				Console.WriteLine();
			}

			DateTimeOffset? startedBuildDateTime = null;
			while (startedBuildDateTime == null)
			{
				Console.WriteLine("今回のビルドの開始日時を入れてください（例: \"12:30\", \"2019/1/23 9:30\"）。何も入力しなければ現在の日時が使われます。");

				string input = Console.ReadLine();

				if (string.IsNullOrEmpty(input))
				{
					startedBuildDateTime = DateTimeOffset.Now;
				}
				else if (DateTimeOffset.TryParse(input, out var dateTime))
				{
					startedBuildDateTime = dateTime;
				}
			}
			Console.WriteLine("ビルド開始時刻: " + startedBuildDateTime);
			Console.WriteLine();

			var pullRequestRequest = new PullRequestRequest
			{
				State = ItemStateFilter.Closed,
				SortProperty = PullRequestSort.Updated,
				SortDirection = SortDirection.Descending,
				Base = lastMergedPrOfPreviousBuild.Base.Ref,
			};

			var apiOptions = new ApiOptions
			{
				PageCount = 1,
				StartPage = 1,
			};

			var pullRequests = new List<PullRequest>();

			while (pullRequests.All(pr => pr.Number != lastMergedPrOfPreviousBuild.Number))
			{
				Console.WriteLine(apiOptions.StartPage + "ページ目を見ている。。。");

				pullRequests.AddRange(await client.PullRequest.GetAllForRepository(repository.Id, pullRequestRequest, apiOptions));
				apiOptions.StartPage++;
			}

			Console.WriteLine("見終わりました。");
			Console.WriteLine();

			return pullRequests
				.Where((pr) =>
				{
					return pr.Merged
						&& lastMergedPrOfPreviousBuild.MergedAt < pr.MergedAt
						&& pr.MergedAt < startedBuildDateTime;
				});
		}

		// ここの列挙子名がそのまま出力される
		private enum PullRequestTagType
		{
			ゲーム全体,
			インゲーム,
			アウトゲーム,
			アセット,
			その他,
			不正PRタグ,
		}

		private static void Output(IEnumerable<PullRequest> newlyMergedPullRequests)
		{
			if (newlyMergedPullRequests.Any())
			{
				Console.WriteLine($"以下が今回のビルドまでに新たに{newlyMergedPullRequests.First().Base.Ref}にマージされたPRです:");
				Console.WriteLine();
				Console.WriteLine("```");

				var pullRequestGroups = newlyMergedPullRequests
					.OrderBy(pr => pr.Title)
					.GroupBy(
						(pr) =>
						{
							// ここでPRを振り分ける
							switch (pr.Title)
							{
							case var title when title.StartsWith("[IN&OUT:"):
								return PullRequestTagType.ゲーム全体;

							case var title when new Regex(@"^\[(IN|IN-AI|IN-STAGE):").IsMatch(title):
								return PullRequestTagType.インゲーム;

							case var title when title.StartsWith("[OUT:"):
								return PullRequestTagType.アウトゲーム;

							case var title when title.StartsWith("[ASSET:"):
								return PullRequestTagType.アセット;

							case var title
								when new Regex(@"^\[(TOOL:|DEBUG:|META|OTHER|DO_NOT_MERGE)").IsMatch(title):
								return PullRequestTagType.その他;

							default:
								return PullRequestTagType.不正PRタグ;
							}
						}
					)
					.OrderBy(group => group.Key);

				foreach (var group in pullRequestGroups)
				{
					Console.WriteLine(group.Key);

					foreach (var pullRequest in group)
					{
						Console.WriteLine($"{pullRequest.Title} (#{pullRequest.Number}|{pullRequest.User.Login})");
					}

					Console.WriteLine();
				}

				var lastMergedPullRequest = newlyMergedPullRequests.OrderByDescending(pr => pr.MergedAt.Value).First();
				var jstDateTime = lastMergedPullRequest.MergedAt.Value.ToOffset(new TimeSpan(9, 0, 0)).DateTime;

				Console.WriteLine(
					"メモ: 最後にマージされたPRは #{0} ({1} {2} JST)",
					lastMergedPullRequest.Number,
					jstDateTime.ToShortDateString(),
					jstDateTime.ToShortTimeString()
				);

				Console.WriteLine("```");
			}
			else
			{
				Console.WriteLine("新たにマージされたPRはひとつもありません。");
			}

			Console.WriteLine();
		}
	}
}
