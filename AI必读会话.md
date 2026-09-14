# 会话上下文(新对话必读)
## 项目
- 服务端源码: C:\yingwen-mir2\传奇2水晶服务端-源码\Crystal-master (C#/.NET8 Crystal fork)
- 运行目录: C:\yingwen-mir2\fuwu (Server.exe + Server.MirDB + Envir)
- 客户端: C:\yingwen-mir2\kehux (Client.csproj 编译直出此目录)
- 维护工具: C:\yingwen-mir2\tools\DbTool (命令行, 自动定位fuwu, 命令: shop/items/quests/npcs/delitem/setshape/setmagic/addnpc/gmnpc/patch/dump/genai)
- 更新日志: Crystal-master\更新日志.md

## 编译部署流程
- 服务端: dotnet build Server.MirForms\Server.csproj -c Release → 产物在 Build\Server\Release → 复制到fuwu
- 客户端: dotnet build Client\Client.csproj -c Release → 直出kehux(需先关闭游戏进程)
- DbTool: dotnet build tools\DbTool\DbTool.csproj -c Release → 从fuwu目录运行


## 任务完成 ，写更新日志错误及原因 及修复路径 ，方便后续AI，人工操作，重复搜索等。2026年9月16日