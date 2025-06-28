# 📋 ClipboardServer

一个轻量级的 HTTP 剪贴板服务，支持通过网页、iOS 快捷指令或其他设备程序访问你的 Windows 剪贴板。

> 让剪贴板在设备之间自由流动，像空气一样轻盈。

[![image](https://img.shields.io/github/v/release/hehang0/ClipboardServer.svg?label=latest)](https://github.com/HeHang0/ClipboardServer/releases)
[![GitHub license](https://img.shields.io/github/license/hehang0/ClipboardServer.svg)](https://github.com/hehang0/ClipboardServer/blob/master/LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/hehang0/ClipboardServer.svg)](https://github.com/hehang0/ClipboardServer/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/hehang0/ClipboardServer.svg)](https://github.com/hehang0/ClipboardServer/network)
[![GitHub issues](https://img.shields.io/github/issues/hehang0/ClipboardServer.svg)](https://github.com/hehang0/ClipboardServer/issues)

简体中文 | [English](./README.en.md)

---

## 🚀 特性

* 支持通过 HTTP 获取和设置剪贴板内容
* 支持三种常见数据类型：文本、图像、文件
* 内置简易网页 UI，可直接在浏览器中操作
* 可在 iOS 快捷指令中调用，轻松实现跨平台剪贴板同步

---

## 🛠️ 使用方法

### 运行项目

默认启动地址为：

```
http://localhost:37259/
```

你可以用手机或局域网内设备访问：

```
http://<你的电脑IP>:37259/
```

---

## 🌐 API 接口文档

### `GET /`

返回主页 HTML 页面，提供基础的剪贴板查看和设置功能。

---

### `GET /favicon.ico`

返回网站图标，用于网页显示。

---

### `GET /clipboard/type`

返回当前剪贴板中的数据类型。可能的值包括：

* `"Text"`
* `"Image"`
* `"FileDrop"`
* `"Unknown"`

---

### `GET /clipboard/text`

返回剪贴板中的文本内容（纯文本）。

---

### `GET /clipboard/image`

返回剪贴板中的图像内容（以 `image/png` 格式输出）。

---

### `GET /clipboard/file`

返回剪贴板中包含的文件名列表（JSON 数组格式）。

---

### `PUT /clipboard`

将请求中的内容设置为剪贴板数据，支持以下格式：

* `Content-Type: text/plain` → 设置为纯文本
* `Content-Type: image/png` → 设置为图像
* `Content-Type: application/json` → 可扩展支持结构化数据

示例：

```bash
curl -X PUT http://localhost:37259/clipboard \
     -H "Content-Type: text/plain" \
     --data "Hello from clipboard!"
```

---

## 📱 iOS 快捷指令使用

你可以创建快捷指令，通过 `GET` 和 `PUT` 请求与服务通信，实现 iOS 与 Windows 之间的剪贴板互通。

示例：

* 获取剪贴板文本：

  ```
  GET http://<你的电脑IP>:37259/clipboard/text
  ```
* 设置剪贴板文本：

  ```
  PUT http://<你的电脑IP>:37259/clipboard
  Content-Type: text/plain
  Body: 你要设置的文本
  ```

---

## ✨ 使用场景

* 在电脑与手机之间同步文本链接
* 将 iOS 剪贴板中的图像快速粘贴到 Windows
* 使用网页跨系统复制粘贴内容
* 自动化工具与剪贴板联动处理数据

---

## 📜 License

MIT License

---

## ✍️ 作者

[HeHang](https://github.com/HeHang0)
