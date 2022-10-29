# OKP
One-Key-Publish，一键发布Torrent到常见BT站。

## 准备文件
1. Torrent种子文件
    - 注意Torrent V2种子不受支持。
2. Setting.toml配置文件
    - Setting.toml需要与Torrent文件同目录，或在第二个参数中指定
    - 自动匹配修改集数与分辨率
        - 如果需要匹配集数，需要在`display_name`中包含`<ep>`标签，同时在`filename_regex`中填写一个合法的正则表达式，并将集数放入名为`<?ep>`的命名分组中。程序会使用表达式对Torrent文件名进行匹配。
        - 匹配分辨率方法同上，需要`display_name`中包含`<res>`标签和toml文件中添加`resolution_regex`（将分辨率放入名为`<?res>`的命名分组中）
    - intro_template中填写各发布站的发布模板。site为发布站名称，content为发布正文。content可以指定一个文件名或raw string。
3. OKP_userprop.toml 文件（非必须）
    - 需要放在使用的应用程序同目录下
    - 一些不方便写在setting.toml中的敏感信息可以使用它来指定，现在支持cookie、proxy、user_agent
    - 想要替换/指定的配置需要与setting.toml中的site和name相同

## 运行
1. 将Torrent作为第一个参数输入，你可以将Torrent拖放到OKP.Core.exe上。
2. OKP将首先检查Torrent与相关信息设置。
3. 检查通过后会尝试登录你设定的发布站。如果你的Cookie或者User-Agent设置有误，这里会登录失败。
4. 没有问题的话就可以按任意键继续发布了。
