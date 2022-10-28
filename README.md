# OKP
One-Key-Publish，一键发布Torrent到常见BT站。

## 准备文件
1. Torrent种子文件
    - 注意Torrent V2种子不受支持。
2. Setting.toml配置文件
    - Setting.toml需要与Torrent文件同目录，或在第二个参数中指定
    - 如果需要匹配集数，需要在display_name中包含<ep>标签，同时在filename_regex中填写一个合法的正则表达式，并将集数放入名为<?ep>的命名分组中。程序会使用表达式对Torrent文件名进行匹配。
