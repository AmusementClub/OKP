# OKP

One-Key-Publish，一键发布 Torrent 到常见 BT 站。

如果需要图形界面，可以尝试使用 [OKPGUI](https://github.com/AmusementClub/OKPGUI)。

## Quick Start

依赖：[.NET 6 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

1. 导出并添加 Cookie [参考这里](#导入-Cookie-到-OKP)，默认的 cookie 文件将保存在程序目录的 `config\cookies` 目录下。
2. 编写一个配置文件 [示例](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/setting.toml)，将配置文件置于种子文件同目录下。
3. 拖动你的种子文件到 OKP.exe 上以发布你的资源，OKP 将自动寻找 cookie 文件与配置文件。

## 选项及参数

`OKP.Core yourTorrent.torrent {-s yourSetting.toml} {--cookies yourCookieFile.txt}`

```
  --cookies           (Default: cookies.txt) (Not required) Specific Cookie file.

  -s, --setting       (Default: setting.toml) (Not required) Specific setting file.

  -l, --log_level     (Default: Debug) Log level.

  --log_file          (Default: log.txt) Log file.

  -y                  Skip reaction.

  --help              Display this help screen.

  --version           Display version information.

  torrent (pos. 0)    Required. Torrents to be published.(Or Cookie file exported by Get Cookies.txt.)
```

### 必选项

你必须输入一个 Torrent 种子文件

- 注意 Torrent V2 不受支持

或者

一个包含 cookie 信息的 txt 文件

- 你需要使用 [Get cookies.txt LOCALLY](https://chrome.google.com/webstore/detail/get-cookiestxt-locally/cclelndahbckbenkjhflpdbgdldlbecc) 这个 Chrome 扩展来导出 txt。

### 可选项

- `--cookies`，输入一个 cookie，若不指定则为 `config\cookies` 目录下的 `cookies.txt`
- `-s, --setting`，输入一个配置文件，若不指定则为 Torrent 同目录下的 `setting.toml`
- `-l, --log_level`，指定输出的 log level，默认是 `Debug`，可以指定 `Verbose, Debug, Info`（不区分大小写）
- `--log_file`，指定输出的 log 文件，默认命名为 `log{当前时间的4位年份}{当前时间的2位月份}.txt`，默认输出位置为执行目录
- `-y`，跳过所有需要回车的地方

## 配置文件

### setting

首先是个 `.toml` 配置文件，[示例](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/setting.toml)

#### 发布标题相关

1. `display_name`：BT 站发布标题，有 `<ep>` 与 `<res>` 两个可配置标签，可用正则表达式从 Torrent 文件名中提取出集数与分辨率并自动填充

- `<ep>`：使用 `filename_regex` 中指定的正则表达式提取集数
- `<res>`：使用 `resolution_regex` 中指定的正则表达式提取分辨率

2. `filename_regex`：一个合法的正则表达式，并将集数放入名为 `<?ep>` 的命名分组中
3. `resolution_regex`：一个合法的正则表达式，并将集数放入名为 `<?res>` 的命名分组中

#### 发布用其他信息

1. `group_name`：不知道有啥用
2. `poster`：海报链接，会用在 BT 发布中需要提供图片的非正文部分
3. `about`：关于 / 联系方式 / 报错链接等，会用在 BT 发布中相应的非正文部分

#### 发布分类相关

1. `tags`: 一个用于描述资源分类的 List。全部可用的类型请参照 [OKP.Core.Interface.TorrentContent.ContentTypes](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/Interface/TorrentContent.cs#L72)。你可以添加任意数量的 tag，这些 tag 会根据各发布站的规则自动映射到对应分类，详见 [tags 映射](https://github.com/AmusementClub/OKP/wiki/TagsConvert)。

#### 发布站点与正文

都在 `[[intro_template]]` 中

1. `site`：发布站名，具体见 [支持站点](#支持站点)
2. `name`：发布用账户名称或发布用组织名称
   - acgnx 站点使用 api 的 uid
3. `proxy`：连接站点使用的代理
4. `content`：发布正文，可以指定一个文件名或 raw string
   - 指定文件名的后缀同见 #支持站点

### publish template

同见 [支持站点](#支持站点)

### 导入 Cookie 到 OKP

1. 安装[Get Cookies.txt](https://chrome.google.com/webstore/detail/get-cookiestxt/bgaddhkoddajcdgocldbbfleckgcbcid)
2. 正常登录对应的发布站。目前已经支持的站点同见 [支持站点](#支持站点)
3. 点击扩展中的`Export`或`Export as`导出同站点下的全部 Cookie。由于 C# 中 Cookie 容器最大仅支持 300 条 Cookie 同时存在，不推荐一次性导出浏览器中的全部 Cookie。
4. ~~如果你导出了[動漫花園](https://share.dmhy.org/)的 Cookie，你需要在 txt 中删掉多余记录，仅保留 `pass; rsspass; tid; uname; uid` 共 5 行记录。如果你忘记在 txt 中删除多余记录，你可以在导出完成的 `cookie.txt` 文件中删除多余记录。~~
5. OKP 支持一次添加多个 Cookie 文件，所有的 Cookie 都导出完成后，多选全部 txt 文件，并拖拽到 `OKP.exe` 上。
6. 你需要输入一个文件名来保存的准备导入的 Cookie，默认文件名为 `cookie`。
7. 你需要输入你的浏览器 User-Agent 来确保你的 Cookie 可以正常工作。如果你不知道如何获取你的 User-Agent，你可以访问 [这里](http://my-user-agent.com/)。
8. OKP 会自动添加 Cookie，当一个文件添加完成时，你需要回车确认并继续。
9. 添加完成后，具有 Cookie 记录的文件将会保存在程序目录中的 `config\cookies` 目录下，默认文件名为 `cookie.txt` 或你指定的文件名。

### 使用 OKP 中的 Cookie 信息

- cookie 文件默认保存在 `OKP.exe` 同目录下，文件名为 `cookie.txt`。
- 你可以在 `publish template` 中指定任意的 Cookie 文件。
- 正常情况下所有 Cookie 均会自动更新并保存。当 Cookie 失效并且无法自动刷新时，你可以直接添加对应 Cookie，OKP 会自动处理并管理这些 Cookie。

### userprop （实验性功能，将会在未来版本被移除）

需要放在使用的应用程序同级的 `config` 目录下，文件名为 `userprop.toml`，[示例](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/userprop.toml)

可以使用它来指定一些不方便写在 setting 中的敏感信息，现在支持 `proxy` 和 `cookie`（可以用来指定 acgnx 的 api token）。二者同时指定时，userprop 中的数据会覆盖 setting 的数据。

想要替换 / 指定的配置需要与 setting 中的 `site` 和 `name` 相同

## 支持站点

_以下排名无先后_

| 站点                                          | 配置名称     | 模板格式与示例                                                                             |
| --------------------------------------------- | ------------ | ------------------------------------------------------------------------------------------ |
| [Nyaa](https://nyaa.si/)                      | nyaa         | [.md](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/nyaa.md)           |
| [動漫花園](https://share.dmhy.org/)           | dmhy         | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)       |
| [ACG.RIP](https://acg.rip/)                   | acgrip       | [.bbcode](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/acgrip.bbcode) |
| [末日動漫資源庫](https://share.acgnx.se/)     | acgnx_asia   | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)       |
| [AcgnX Torrent Global](https://www.acgnx.se/) | acgnx_global | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)       |
| [萌番组](https://bangumi.moe/)                | bangumi      | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)       |

最新支持站点请看 [这](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/Utils/Constants.cs#L8)

注：

1. acgrip cookie 失效后会刷新，退出登录疑似会直接失效，ua 不同也会登录失败。
2. acgnx 站点登录可能会被 Cloudflare 风控，鉴于其站点会同步 nyaa、dmhy、acgrip 的种子，可以选择不使用其上传。
3. 萌番组暂不支持自定义 TAG，目前仅支持 _Team ID_ 和 setting 中 tags 映射的分类两个 TAG。
4. ~~動漫花園必须删除多余的 Cookie，否则无法登录。~~

## 最佳实践

- 如果你只有一个发布身份 / 账号，将所有账号的 Cookie 保存在同一个 Cookie 文件中。
- 如果你有多个发布身份 / 账号，将同一个身份的 Cookie 保存在同一个 Cookie 文件中，并以账户名命名你的 Cookie 文件。
- 尽可能全面的设置你的 Tags，以便在各发布站准确映射到对应分类。
- 如果发布站的模板格式相同，例如 dmhy 和 acgnx，使用同一个模板文件。
- 将配置文件与种子放置在同一个目录，或者自行编写一个批处理文件处理输入参数，以方便你拖动种子文件进行发布。
- **将 Cookie 视为你的账户密码并妥善保护，任何获取到 Cookie 文件的人都可以轻易登录你的账户。**
- 在配置文件中尽可能的使用相对目录，来避免潜在的信息泄露。
- 由于 OKP 将直接使用你的账户进行敏感操作，如果你不信任 OKP，请自行审计源码，并从 Github Action 下载最新的 Build。

## 常见问题

### 导入了 Cookie 后无法访问某个发布站

删除 cookie 文件重新添加。

### 已经重新添加了，还是无法访问某个发布站（例如 acg.rip）

删除 cookie 文件，使用浏览器登录后，访问发布页面（[例如](https://acg.rip/cp/posts/upload)）,在发布页面重新导出 cookie 后再导入。

### 某个发布站发布失败

重新进行发布。由于绝大多数发布站都具有检测重复资源的机制，OKP 将会尝试重新在所有已配置的发布站进行发布。

### 其他问题

去提个 [issue](https://github.com/AmusementClub/OKP/issues)，不出意外你的 issue 会出现在这里。
