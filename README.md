# AiSign · AI 签到助手

轻量 Windows 多账号自动签到工具：**Trae CN / WorkBuddy / CodeBuddy / Qoder CN**。

单文件 `AiSign.exe`，依赖 Windows 自带的 .NET Framework 4.8，**无需安装运行环境**。

> 本工具仅供学习研究，与各平台官方无任何关联。自动化签到可能违反平台服务条款，账号风险由使用者自行承担。请仅管理本人合法持有的账号。

## 功能

- **多账号管理**：扫描本机登录态、粘贴 Token、浏览器 OAuth（WorkBuddy / Qoder）三种方式录入；账号可启用/停用、重命名、删除
- **自动签到**：按平台目标时刻自动执行，失败按 5/15/30/60/120 分钟退避重试，23:00 后放弃；支持手动立即签到与只预检
- **自启管理**：HKCU Run 开机自启、启动时最小化、关闭到托盘、路径移动后自动修复
- **签到记录**：今日汇总、逐账号结果、最近 7 天状态条、托盘状态聚合
- **凭证安全**：账号元数据与凭证分离，凭证用 DPAPI（当前 Windows 用户）加密，日志脱敏
- **深浅主题**：深色/浅色/跟随系统

## 快速开始

1. 双击 `AiSign.exe`（首次运行若被 Defender 拦截，选择“仍要运行”——程序无数字签名）
2. 打开「账号管理」→「添加账号」
   - **Trae CN**：扫描本机 `storage.json`，或粘贴 `Cloud-IDE-JWT` / iCube 密文。**只支持单账号**
   - **WorkBuddy / CodeBuddy**：推荐「浏览器授权」逐账号授权；也可扫描本机 auth 文件或粘贴 Token。5.6+ 客户端本机凭证已加密，扫描时会提示改用浏览器授权
   - **Qoder CN**：推荐「浏览器授权」走阿里云 SSO 设备流；也可扫描本机 `auth.v1.dat`，或粘贴 Access Token / PAT（`pt-`）/ Refresh Token
3. 到「设置」开启「开机自动启动」
4. 程序常驻托盘；关闭窗口默认收进托盘，托盘右键可退出

## 自动签到时刻

| 平台 | 默认时刻 | 说明 |
|---|---|---|
| Trae CN | 00:05 | 每日签到；同设备每天仅一个账号能成功（服务端 9095） |
| WorkBuddy | 00:05 | 每日签到；活动未开时按退避重试 |
| Qoder CN | 10:05 | 每日活动 10:00（UTC+8）刷新，留 5 分钟余量 |

设置页可修改每个平台的目标时刻。账号间默认间隔 1.5 秒，避免并发触发风控。

## 数据

全部保存在本机：

```
%APPDATA%\AiSign\
├─ settings.json        # 设置
├─ accounts.json        # 账号元数据（不含 token）
├─ secrets.json         # DPAPI 加密的账号凭证
├─ results.json         # 签到结果（保留 180 天）
├─ state.json           # 当日调度/退避状态
├─ logs\                # 运行日志（脱敏，保留 7 天）
└─ startup_error.txt    # 仅在启动失败时生成
```

换 Windows 用户、重装系统或更换电脑后，DPAPI 无法解密旧凭证，需要重新添加账号。

## 构建

本机只需 Windows 自带的 .NET Framework 4.8：

```
build.bat
```

编译产物为当前目录的 `AiSign.exe`（约 180KB），无 NuGet、无 MSBuild、无外部 DLL。

## 自测与诊断

```
AiSign.exe --selftest          # 64 项纯逻辑自测，结果写 %TEMP%\aisign_selftest.log
AiSign.exe --uicheck           # 构建主窗口与三个页面，结果写 %TEMP%\aisign_uicheck.log
AiSign.exe --scan              # 只读扫描本机登录态，结果写 %TEMP%\aisign_scan.log
AiSign.exe --dryrun            # 只读预检本机账号（不领取），结果写 %TEMP%\aisign_dryrun.log
AiSign.exe --data-dir <目录>   # 使用指定数据目录（便于测试/便携）
```

## 已知限制

- **Trae**：同机多个 Trae 客户端共享设备 ID，服务端规定每天只有一个账号能签到成功；本工具按需求只做单账号，第二条会替换现有 Trae 账号
- **WorkBuddy 5.6+**：本机登录态为 `$wbEncrypted` 加密信封，密钥在客户端进程内，无法离线读取；请使用浏览器授权
- **Qoder**：每个账号生成独立且固定的 `Cosy-MachineId`，不轮换；仍属平台风控的灰色地带
- **Qoder 浏览器授权**：使用官方固定 client_id 的 PKCE 设备流；若 Qoder 轮换 client_id 导致授权页提示参数无效，可改用扫描本机或粘贴 PAT
- 接口均为平台私有接口，平台改版可能导致签到失败；错误信息会写入日志
- 未做数字签名，部分杀毒软件可能误报；可自行用 `build.bat` 编译

## 代码来源与许可

- 签到算法（Trae iCube 解密、WorkBuddy OAuth/续期、Qoder auth.v1.dat 解密、调度退避）参考同工作区的 **TraeSign** 项目（MIT）
- 多账号模型、凭证隔离、结果表设计参考同工作区的 **AI Work 助手 / TraeWorkAssistant** 项目（MIT）

本项目采用 MIT License。

## 明确不做

- ZCode、豆包、Buddy AI
- API 网关、MITM 代理、登录态切换/快照
- 积分大盘、自动更新、云同步
