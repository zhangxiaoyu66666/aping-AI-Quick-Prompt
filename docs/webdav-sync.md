# WebDAV 同步方案

啊拼支持通用 WebDAV 同步，可用于坚果云、Nextcloud 等兼容服务。该功能默认关闭，只有用户在设置页显式启用并点击同步按钮时才会访问远端。

## 坚果云示例

- WebDAV 服务器地址：`https://dav.jianguoyun.com/dav/`
- 用户名：坚果云账号
- 应用密码：坚果云为第三方应用生成的 WebDAV 应用密码
- 远端目录：默认 `啊拼`

## 密码边界

- `应用密码` 只用于登录 WebDAV 服务，可保存到 Windows Credential Manager。
- `同步口令` 用于解锁端到端加密 vault，不明文保存。
- 如果用户勾选记住同步密钥，应用只在本机 Credential Manager 保存派生后的 vault key，并按 WebDAV 远端地址和用户名隔离。

## 同步数据

远端目录只保存导出的同步快照：

```text
sync/
  manifest.json
  crypto/vault.json
  history/<id>.json
  history-tombstones/<id>.json
  backups/<timestamp>/...
```

历史正文在写入 WebDAV 前已加密。API Key、OCR 诊断、截图、模型发送审计、临时文件和 SQLite 主数据库不会上传到 WebDAV。

## 冲突处理

- 每次覆盖远端文件前会先写入 `sync/backups/`。
- 同步前会通过 WebDAV `PROPFIND` 尝试检测冲突副本文件名。
- 历史导入按记录更新时间合并，远端旧记录不会覆盖本机较新的历史。
- 不做后台自动上传；网络断开、服务限流或认证失败时只提示错误。
