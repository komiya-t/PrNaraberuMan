# PR並べるマン

今回のビルドまでに新たにマージされたプルリクエストを並べてくれる良いやつ  
https://techblog.kayac.com/unity_advent_calendar_2018_03

## 準備

.NET Core SDK (v2.1以上）を入れる  
https://www.microsoft.com/net/download

## 使い方

`dotnet run <リポジトリOwner> <リポジトリ名> <GitHubメールアドレス> <GitHubパスワード>`

※たまにdotnet runのバグかなんかでdotnetの子プロセスが残りっぱなしになることがあったらアクティビティモニタなり`pkill dotnet`なりで消してあげよう

---
### 忙しい人のための使い方

*prnaraberuman.sh*
```
#!/bin/sh

dotnet run --project <置き場所までのパス>/PrNaraberuMan <リポジトリOwner> <リポジトリ名> <GitHubメールアドレス> $1
```

デスクトップとか適当な場所に↑のファイルを作って  
`sh prnaraberuman.sh <GitHubパスワード>`

### もっと忙しい人のための使い方（非推奨）

*prn.sh*
```
#!/bin/sh

dotnet run --project <置き場所までのパス>/tools/PrNaraberuMan <リポジトリOwner> <リポジトリ名> <GitHubメールアドレス> <GitHubパスワード>
```

デスクトップとか適当な場所に↑のファイルを作って  
`sh prn.sh`

---
## License

The MIT License (MIT)

(C) 2018 KAYAC Inc.
