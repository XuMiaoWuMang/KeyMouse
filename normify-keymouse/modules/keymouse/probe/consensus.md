---
uid: fc196de5
id: keymouse.probe.consensus
parent: keymouse.probe
tags: [perception, ocr]
name: {zh: "共识判定与置信度地板", en: "Consensus and confidence floor"}
description:
  zh: >
      N 次重新采集必须**逐字节一致**才算“看清”，置信度还需过 30 的地板。地板是实测定的：合成图上 60 以下全是垃圾，但真实屏幕上一次读对的只有 55.8，所以降到只抓彻底失败。比较本身不做任何加工——不折叠空白、不剔光标：工具读到的文字原样回报，两次不一致就是退出码 6（可重试），由调用方决定怎么办。
      
  en: >
      N re-captures must agree byte for byte before a read counts as seen, and confidence must clear a floor of 30. The floor is measured: everything under 60 was garbage on synthetic images while a correct real read scored 55.8, so it only catches outright failures. The comparison itself normalises nothing - no whitespace folding, no caret stripping: the text the engine produced is the text that is compared, and a disagreement is exit 6 for the caller to retry.
      
revision: 89f30aa8db66e03f9c60253d85abc64d7760319e
updated_at: "2026-10-03T10:20:29.618Z"
fingerprint: 4db3095a35b63686b846dcf1cccf8494415ec81c433617e95b27c91cd9d85ab8
source:
  - path: "Probe.cs"
    line: 265
    end_line: 292
apis:
  - protocol: rpc
    path: "Probe.IsSeen"
    description:
      zh: >
          把“到底看清了没”的策略集中在一处可单测的地方：多次读取必须逐字节一致，置信度需过地板。
          
      en: >
          The whole did-we-see-it policy in one testable place: reads must agree byte for byte and confidence must clear the floor.
          
---
