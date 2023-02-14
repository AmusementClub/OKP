# OKP

One-Key-Publish，一键发布 Torrent 到常见 BT 站。

## Quick Start

1. 导出并添加 Cookie [参考这里](#导入-cookie-到-okp)，默认的 cookie 文件将保存在程序目录的`config\cookies`目录下。
2. 编写一个配置文件 [示例](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/setting.toml)，将配置文件置于种子文件同目录下。
3. 拖动你的种子文件到 okp.exe 上以发布你的资源，okp 将自动寻找 cookie 文件与配置文件。

## 选项及参数

`OKP.Core yourTorrent.torrent {-y yourSetting.toml} {-cookie yourCookieFile.txt}`

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

- 你需要使用 [Get Cookies.txt](https://chrome.google.com/webstore/detail/get-cookiestxt/bgaddhkoddajcdgocldbbfleckgcbcid) 这个 Chrome 扩展来导出 txt。

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

1. `tags`: 一个用于描述资源分类的 List。全部可用的类型请参照 [OKP.Core.Interface.TorrentContent.ContentTypes](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/Interface/TorrentContent.cs#L72)。你可以添加任意数量的 tag，这些 tag 会根据各发布站的规则自动映射到对应分类。

#### 发布站点与正文

都在 `[[intro_template]]` 中

1. `site`：发布站名，具体见 #支持站点
2. `name`：发布用账户名称或发布用组织名称
   - acgnx 站点使用 api 的 uid
3. `proxy`：连接站点使用的代理
4. `content`：发布正文，可以指定一个文件名或 raw string
   - 指定文件名的后缀同见 #支持站点

`proxy` 详细介绍放在 #userprop

### publish template

同见 [支持站点](#支持站点)

### 导入 Cookie 到 OKP

1. 安装[Get Cookies.txt](https://chrome.google.com/webstore/detail/get-cookiestxt/bgaddhkoddajcdgocldbbfleckgcbcid)
2. 正常登录对应的发布站。目前已经支持的站站点同见 #支持站点
3. 点击扩展中的`Export`或`Export as`导出同站点下的全部 Cookie。由于 C#中 Cookie 容器最大仅支持 300 条 Cookie 同时存在，不推荐一次性导出浏览器中的全部 Cookie。
4. 如果你导出了[動漫花園](https://share.dmhy.org/)的 Cookie，你需要在 txt 中删掉多余记录，仅保留`pass; rsspass; tid; uname; uid`共 5 行记录。如果你忘记在 txt 中删除多余记录，你可以在导出完成的`cookie.txt`文件中删除多余记录。
5. OKP 支持一次添加多个 Cookie 文件，所有的 Cookie 都导出完成后，多选全部 txt 文件，并拖拽到`OKP.exe`上。
6. OKP 会自动添加 Cookie，当一个文件添加完成时，你需要回车确认并继续。
7. 添加完成后，具有 Cookie 记录的文件将会保存在`OKP.exe`同目录下，文件名为`cookie.txt`

### 使用 OKP 中的 Cookie 信息

- cookie 文件默认保存在`OKP.exe`同目录下，文件名为`cookie.txt`。
- 你可以在`publish template`中指定任意的 Cookie 文件。
- 正常情况下所有 Cookie 均会自动更新并保存。当 Cookie 失效并且无法自动刷新时，你可以直接添加对应 Cookie，OKP 会自动处理并管理这些 Cookie。

### userprop （实验性功能，将会在未来版本被移除）

需要放在使用的应用程序同目录下，文件名为 `OKP_userprop.toml`，[示例](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/OKP_userprop.toml)

可以使用它来指定一些不方便写在 setting 中的敏感信息，现在支持 `proxy`。二者同时指定时，userprop 中的数据会覆盖 setting 的数据。

想要替换 / 指定的配置需要与 setting 中的 `site` 和 `name` 相同

## 支持站点

_以下排名无先后_

| 站点                                          | 配置名称     | 模板格式与示例                                                                             |
| --------------------------------------------- | ------------ | ------------------------------------------------------------------------------------------ |
| [Nyaa](https://nyaa.si/)                      | nyaa         | [.md](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/nyaa.md)           |
| [動漫花園](https://share.dmhy.org/)           | dmhy         | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)       |
| [ACG.RIP](https://share.dmhy.org/)            | acgrip       | [.bbcode](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/acgrip.bbcode) |
| [末日動漫資源庫](https://share.acgnx.se/)     | acgnx_asia   | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)       |
| [AcgnX Torrent Global](https://www.acgnx.se/) | acgnx_global | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)       |
| [萌番组](https://bangumi.moe/)                | bangumi      | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)       |

最新支持站点请看 [这](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/Utils/Constants.cs#L8)

注：

1. acgrip cookie 失效后会刷新，退出登录疑似会直接失效，ua 不同也会登录失败
2. acgnx 站点登录可能会被 Cloudflare 风控，鉴于其站点会同步 nyaa、dmhy、acgrip 的种子，可以选择不使用其上传
3. 萌番组暂不支持自定义 TAG，目前仅支持 _Team ID_ 和 _动画_ 两个 TAG
4. 動漫花園必须删除多余的 Cookie，否则无法登录。

## 常见问题

### 导入了 Cookie 后无法访问某个发布站

删除 cookie 文件重新添加。

### 已经重新添加了，还是无法访问某个发布站（例如 acg.rip）

删除 cookie 文件，使用浏览器登录后，访问发布页面（[例如](https://acg.rip/cp/posts/upload)）,在发布页面重新导出 cookie 后再导入。

### 某个发布站发布失败

重新进行发布。由于绝大多数发布站都具有检测重复资源的机制，okp 将会尝试重新在所有已配置的发布站进行发布。

### 其他问题

去提个 [issue](https://github.com/AmusementClub/OKP/issues)，不出意外你的 issue 会出现在这里。
