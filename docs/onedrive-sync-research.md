# OneDrive 文件夹同步方案

本文记录 Microsoft Store 分支的 OneDrive 同步方案。结论：啊拼不租云服务器，不通过 Microsoft Graph 上传用户数据，不接入 MSAL 登录。数据文件保存在用户本地，由用户已登录的 OneDrive 客户端负责跨设备同步。

## 目标

- 默认保持本地保存：主数据库继续使用本机 `aipin.db`。
- 用户主动启用：设置页提供 `启用 OneDrive 文件夹同步` 开关。
- 用户确认路径：显示或选择类似 `C:\Users\用户名\OneDrive\啊拼` 的本地目录。
- 手动同步：只有用户点击同步按钮时才写入同步目录。
- 端到端加密：历史记录写入同步目录前必须加密。

## 非目标

- 不把 SQLite 主库直接放进 OneDrive。
- 不使用 Microsoft Graph / OneDrive App Folder。
- 不使用 Microsoft Entra Client ID / MSAL / WAM。
- 不把 API Key、OCR 诊断、截图、模型发送审计或明文历史写入 OneDrive。
- 不在 GitHub 社区版展示灰色同步按钮。

## 存储模型

本地数据目录仍然是运行时主存储。同步目录只保存导出的快照：

```text
啊拼/
  sync/
    manifest.json
    crypto/
      vault.json
    history/
      <history-id>.json
    history-tombstones/
    backups/
      <utc-timestamp>/
        ...
```

`vault.json` 包裹随机生成的 `vaultKey`。用户输入同步口令后，应用用 PBKDF2-HMAC-SHA256 派生 key-encryption-key，再用 AES-256-GCM 解开 `vaultKey`。历史正文使用 `vaultKey` 派生的数据密钥加密，OneDrive 目录中只出现密文和元数据。

## 用户流程

1. 默认不开启 OneDrive 文件夹同步。
2. 用户进入设置页，勾选 `启用 OneDrive 文件夹同步`。
3. 用户点击 `自动检测路径` 或 `选择文件夹`。这一步只保存路径，不创建同步文件。
4. 用户输入同步口令，点击 `同步历史`。
5. 应用先导入目录中较新的加密快照，再导出本机历史快照。
6. 同步写入完成后，应用尝试启动本机 `OneDrive.exe /background`。如果 OneDrive 已在运行则不重复启动；如果找不到客户端，只提示用户，不回滚已经写好的本地加密快照。
7. OneDrive 客户端自行把这些本地文件同步到用户自己的 OneDrive。
8. 下次启动时，应用只读检测 `manifest.json` 是否比本机最后同步时间更新；若更新，只提示用户打开设置页导入，不自动导入。

## 冲突和可靠性

- 写入前备份被替换的旧文件到 `sync/backups/<timestamp>/`。
- 写入时先写临时文件，再原子替换目标文件。
- 文件被 OneDrive 客户端短暂锁定时进行轻量重试。
- 检测到 OneDrive 冲突副本文件名时停止同步，提示用户手动合并。
- 历史导入按记录更新时间合并，较旧的同步快照不能覆盖本机较新的记录。

## GitHub 社区版边界

GitHub 社区版不包含 OneDrive 文件夹同步入口；不要保留灰色按钮或不可用菜单。README 可以说明该入口属于 Microsoft Store 分支。社区版仍然保留本地保存、模板、Skill、OCR、模型调用和 GPL 源码能力。
