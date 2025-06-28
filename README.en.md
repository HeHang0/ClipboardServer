# 📋 ClipboardServer

A lightweight HTTP clipboard server that allows you to access and manipulate your Windows clipboard through a web page, iOS Shortcuts, or other devices and programs.

> Let your clipboard flow freely between devices, as effortlessly as air.

[![image](https://img.shields.io/github/v/release/hehang0/ClipboardServer.svg?label=latest)](https://github.com/HeHang0/ClipboardServer/releases)
[![GitHub license](https://img.shields.io/github/license/hehang0/ClipboardServer.svg)](https://github.com/hehang0/ClipboardServer/blob/master/LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/hehang0/ClipboardServer.svg)](https://github.com/hehang0/ClipboardServer/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/hehang0/ClipboardServer.svg)](https://github.com/hehang0/ClipboardServer/network)
[![GitHub issues](https://img.shields.io/github/issues/hehang0/ClipboardServer.svg)](https://github.com/hehang0/ClipboardServer/issues)

[简体中文](./README.md) | English

---

## 🚀 Features

* Access and set clipboard content via HTTP
* Supports three common data types: Text, Image, File
* Built-in simple web UI for direct browser interaction
* Compatible with iOS Shortcuts for cross-platform sync

---

## 🛠️ Getting Started

### Run the Project

Default server address:

```
http://localhost:37259/
```

You can access it from mobile or LAN devices via:

```
http://<your-computer-IP>:37259/
```

---

## 🌐 API Endpoints

### `GET /`

Returns the homepage HTML with basic clipboard viewing and editing functionality.

---

### `GET /favicon.ico`

Returns the website's favicon.

---

### `GET /clipboard/type`

Returns the data type currently in the clipboard. Possible values:

* `"Text"`
* `"Image"`
* `"FileDrop"`
* `"Unknown"`

---

### `GET /clipboard/text`

Returns the plain text content in the clipboard.

---

### `GET /clipboard/image`

Returns the image content from the clipboard in `image/png` format.

---

### `GET /clipboard/file`

Returns a JSON array of file names currently in the clipboard.

---

### `PUT /clipboard`

Sets the clipboard data with the content from the request body. Supported content types:

* `Content-Type: text/plain` → Sets plain text
* `Content-Type: image/png` → Sets an image
* `Content-Type: application/json` → Extendable for structured data

Example:

```bash
curl -X PUT http://localhost:37259/clipboard \
     -H "Content-Type: text/plain" \
     --data "Hello from clipboard!"
```

---

## 📱 iOS Shortcuts Integration

You can create iOS Shortcuts to send `GET` and `PUT` requests for clipboard interaction between iOS and Windows.

Example:

* Get clipboard text:

  ```
  GET http://<your-computer-IP>:37259/clipboard/text
  ```
* Set clipboard text:

  ```
  PUT http://<your-computer-IP>:37259/clipboard
  Content-Type: text/plain
  Body: your text here
  ```

---

## ✨ Use Cases

* Sync text or links between PC and mobile
* Quickly paste images from iOS clipboard to Windows
* Cross-platform clipboard copy/paste via browser
* Automate tools that interact with the clipboard

---

## 📜 License

MIT License

---

## ✍️ Author

[HeHang](https://github.com/HeHang0)
