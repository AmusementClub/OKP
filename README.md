# OKP
One-Key-Publish，一键发布 Torrent 到常见 BT 站。

## 选项及参数

`OKP.Core yourTorrent.torrent {-y yourSetting.toml}`

```
  -s, --setting       (Default: setting.toml) (Not required) Specific setting file.

  -l, --log_level     (Default: Debug) Log level.

  --log_file          (Default: log.txt) Log file.

  -y                  Skip reaction.

  --help              Display this help screen.

  --version           Display version information.

  torrent (pos. 0)    Required. Torrents to be published.
```

### 必选项

你必须输入一个 Torrent 种子文件  
  - 注意 Torrent V2 不受支持

### 可选项

- `-s, --setting`，输入一个配置文件，若不指定则为 Torrent 同目录下的 setting.toml
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

目前仅支持动画的常规部分，具体分类规则待完善

1. `has_subtitle`：是否有字幕
2. `is_finished`：是否合集

#### 发布站点与正文

都在 `[[intro_template]]` 中

1. `site`：发布站名，具体见 #支持站点
2. `name`：发布用账户名称或发布用组织名称
    - acgnx 站点使用 api 的 uid
3. `cookie`
4. `user_agent`：UA
5. `proxy`：连接站点使用的代理
5. `content`：发布正文，可以指定一个文件名或 raw string
    - 指定文件名的后缀同见 #支持站点

`cookie`、`user_agent`、`proxy` 详细介绍放在 #userprop

### publish template

同见 #支持站点

### userprop

需要放在使用的应用程序同目录下，文件名为 `OKP_userprop.toml`，[示例](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/OKP_userprop.toml)

可以使用它来指定一些不方便写在 setting 中的敏感信息，现在支持 `cookie`、`user_agent`、`proxy`。二者同时指定时，userprop 中的数据会覆盖 setting 的数据。

1. `cookie`
    - dmhy: `"pass=xxx; rsspass=xxx; tid=x; uname=xxx; uid=xxx"`
    - nyaa: `session=xxx`
    - acgrip: `remember_user_token=xxx`
    - acgnx 站点使用 api 的 token
2. 想要替换 / 指定的配置需要与 setting 中的 `site` 和 `name` 相同

## 支持站点

*以下排名无先后*

站点 | 配置名称 | 模板格式与示例
--- | --- | ---
[Nyaa](https://nyaa.si/) | nyaa | [.md](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/nyaa.md)
[動漫花園](https://share.dmhy.org/) | dmhy | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)
[ACG.RIP](https://share.dmhy.org/) | acgrip | [.bbcode](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/acgrip.bbcode)
[末日動漫資源庫](https://share.acgnx.se/) | acgnx_asia | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)
[AcgnX Torrent Global](https://www.acgnx.se/) | acgnx_global | [.html](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/example/dmhy.html)
*[萌番组](https://bangumi.moe/)*  *TODO*

最新支持站点请看 [这](https://github.com/AmusementClub/OKP/blob/master/OKP.Core/Utils/Constants.cs#L8)

注：
1. acgrip cookie 失效后会刷新，退出登录疑似会直接失效，ua 不同也会登录失败
2. acgnx 站点登录可能会被 Cloudflare 风控，鉴于其站点会同步 nyaa、dmhy、acgrip 的种子，可以选择不使用其上传